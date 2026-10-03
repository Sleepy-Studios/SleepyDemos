#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using Core.Runtime;
using Core.Runtime.Inputs;
using Cysharp.Threading.Tasks;
using Hotfix;
using Hotfix.DroneFlight;
using Hotfix.SceneManagement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Tests.Demo
{
    /// 保存态选择页的三端交互、生成失败清理和导航回滚。
    public sealed class DroneVehicleSelectionPlayModeTests
    {
        private Mouse mouse;
        private Keyboard keyboard;
        private Gamepad gamepad;
        private Touchscreen touchscreen;
        private InputSettings originalSettings;
        private InputSettings testSettings;
        private GameSceneNavigator originalNavigator;
        private DeferredFirstLoader delayed;

        [UnityTest, Timeout(180000)]
        public IEnumerator PointerSelectionWaitsForConfirmationAndFailedPreparationCanRetry()
        {
            yield return Prepare();
            var view = UIManager.Instance.Get<DroneFlightVehicleSelectView>();
            var coordinator = Object.FindFirstObjectByType<DroneFlightSceneCoordinator>();
            delayed = new DeferredFirstLoader(ResourceServices.CreateLoader());
            SetField(coordinator, "resourceLoader", delayed);
            yield return Click(Bound<Button>(view, "HarpoonButton"));
            Assert.That(delayed.Requests, Is.Zero, "指针选择只预览，不生成机体。");
            Assert.That(Bound<Image>(view, "Hero").sprite, Is.SameAs(Bound<Image>(view, "HarpoonPreview").sprite));
            yield return Click(Bound<Button>(view, "StartButton"));
            Assert.That(delayed.Requests, Is.EqualTo(1));
            Assert.That(Bound<Button>(view, "BackButton").interactable, Is.False);
            Assert.That(Bound<Button>(view, "HarpoonButton").interactable, Is.False);
            Assert.That(view.State, Is.EqualTo(ViewState.Visible), "准备完成前选择页保留。");
            yield return ClickAt(Position(Bound<Button>(view, "StartButton")));
            Assert.That(delayed.Requests, Is.EqualTo(1), "准备期间不能重复提交。");
            LogAssert.Expect(LogType.Error, new Regex(@"\[DroneFlight\] 机型准备失败："));
            delayed.CompleteInvalidModel();
            yield return Wait(() => Bound<Button>(view, "StartButton").interactable, "生成失败恢复");
            yield return null;
            Assert.That(delayed.ReleasedInvalidModel, Is.True, "部分生成失败必须释放实例。");
            Assert.That(Field<GameObject>(coordinator, "currentDrone"), Is.Null);
            Assert.That(Bound<TMPro.TextMeshProUGUI>(view, "Status").text, Does.Contain("准备失败"));
            Assert.That(Bound<Image>(view, "Hero").sprite, Is.SameAs(Bound<Image>(view, "HarpoonPreview").sprite));
            yield return Click(Bound<Button>(view, "StartButton"));
            yield return Wait(() => UIManager.Instance.Get<DroneFlightHudView>()?.State == ViewState.Visible &&
                view.State == ViewState.Destroyed, "重试进入真实渔叉机型");
            Assert.That(delayed.Requests, Is.EqualTo(2));
            Assert.That(Field<GameObject>(coordinator, "currentDrone").name, Is.EqualTo("DroneHarpoonVariant"));
            Assert.That(Object.FindObjectsByType<DroneFlightController>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator KeyboardGamepadTouchSwitchPreviewAndSubmitWithoutRepeatingHeldInput()
        {
            yield return Prepare();
            var view = UIManager.Instance.Get<DroneFlightVehicleSelectView>();
            int started = 0, backed = 0;
            DroneVehicleKind submitted = default;
            var data = new DroneFlightVehicleSelectionData(kind => { started++; submitted = kind; }, () => backed++);
            view.SetData(data);
            yield return Wait(() => EventSystem.current.sendNavigationEvents, "公共导航松键");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.RightArrow)); yield return null;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null; yield return null;
            Assert.That(data.SelectedKind, Is.EqualTo(DroneVehicleKind.Grapple));
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Enter)); yield return null; yield return null;
            Assert.That(started, Is.EqualTo(1));
            Assert.That(submitted, Is.EqualTo(DroneVehicleKind.Grapple));
            view.SetBusy(false, "准备失败，请重试");
            for(int i=0;i<5;i++) yield return null;
            Assert.That(started, Is.EqualTo(1), "保持确认键不能在恢复时重复进入。");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            yield return Tap(Bound<Button>(view, "HarpoonButton"));
            Assert.That(data.SelectedKind, Is.EqualTo(DroneVehicleKind.Harpoon));
            Assert.That(started, Is.EqualTo(1));
            Assert.That(Bound<TMPro.TextMeshProUGUI>(view, "TouchHint").gameObject.activeSelf, Is.True);
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.DpadLeft)); yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null; yield return null;
            Assert.That(data.SelectedKind, Is.EqualTo(DroneVehicleKind.Grapple));
            Assert.That(Bound<TMPro.TextMeshProUGUI>(view, "TouchHint").gameObject.activeSelf, Is.False);
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.South)); yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
            Assert.That(started, Is.EqualTo(2));
            Assert.That(submitted, Is.EqualTo(DroneVehicleKind.Grapple));
            view.SetBusy(false);
            yield return null; yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState().WithButton(GamepadButton.East)); yield return null;
            InputSystem.QueueStateEvent(gamepad, new GamepadState()); yield return null;
            Assert.That(backed, Is.EqualTo(1), "公共 Cancel 应调用返回操作。");
        }

        [UnityTest, Timeout(180000)]
        public IEnumerator FailedReturnRestoresSelectionAndNewVisitStartsWithPlainDrone()
        {
            yield return Prepare();
            var before = UIManager.Instance.Get<DroneFlightVehicleSelectView>();
            yield return Click(Bound<Button>(before, "GrappleButton"));
            originalNavigator = GameSceneNavigator.Instance;
            var failingNavigator = new GameSceneNavigator(new FailedReturnRuntime(), new GameSceneLoadingPresenter());
            typeof(GameSceneNavigator).GetProperty(nameof(GameSceneNavigator.CurrentScene)).SetValue(failingNavigator, GameSceneId.DroneFlight);
            SetNavigator(failingNavigator);
            LogAssert.Expect(LogType.Error, new Regex(@"\[DroneFlight\] 无法返回主界面：测试返回失败"));
            yield return Click(Bound<Button>(before, "BackButton"));
            yield return Wait(() => !failingNavigator.IsTransitioning &&
                UIManager.Instance.Get<DroneFlightVehicleSelectView>()?.State == ViewState.Visible && before.State == ViewState.Destroyed, "返回失败恢复");
            var restored = UIManager.Instance.Get<DroneFlightVehicleSelectView>();
            yield return null; yield return null;
            Assert.That(Bound<TMPro.TextMeshProUGUI>(restored, "Title").text, Is.EqualTo("四爪抓斗无人机"));
            Assert.That(Bound<TMPro.TextMeshProUGUI>(restored, "Status").text, Does.Contain("返回失败"));
            SetNavigator(originalNavigator); originalNavigator = null;
            yield return Click(Bound<Button>(restored, "BackButton"));
            yield return Wait(() => GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub && !GameSceneNavigator.Instance.IsTransitioning, "返回大厅");
            yield return GameSceneNavigator.Instance.SwitchAsync(GameSceneId.DroneFlight).ToCoroutine();
            yield return Wait(() => UIManager.Instance.Get<DroneFlightVehicleSelectView>()?.State == ViewState.Visible, "重进选择页");
            var fresh = UIManager.Instance.Get<DroneFlightVehicleSelectView>();
            Assert.That(Bound<TMPro.TextMeshProUGUI>(fresh, "Title").text, Is.EqualTo("纯无人机"));
            Assert.That(Bound<Button>(fresh, "StartButton").interactable, Is.True);
            yield return null; yield return null;
            yield return Click(Bound<Button>(fresh, "StartButton"));
            yield return Wait(() => fresh.State == ViewState.Destroyed && UIManager.Instance.Get<DroneFlightHudView>()?.State == ViewState.Visible, "纯无人机进入");
        }

        private IEnumerator Prepare()
        {
            if(GameSceneNavigator.Instance == null)
            {
                var operation=SceneManager.LoadSceneAsync("AppEntrance",LoadSceneMode.Single);
                yield return Wait(()=>operation.isDone,"启动",90);
            }
            yield return Wait(()=>GameSceneNavigator.Instance!=null&&!GameSceneNavigator.Instance.IsTransitioning,"导航稳定",90);
            if(GameSceneNavigator.Instance.CurrentScene!=GameSceneId.Hub)
                yield return GameSceneNavigator.Instance.SwitchAsync(GameSceneId.Hub).ToCoroutine();
            yield return Wait(()=>UIManager.Instance.Get<MainMenuView>()?.State==ViewState.Visible,"大厅",90);
            originalSettings=InputSystem.settings;testSettings=Object.Instantiate(originalSettings);
            testSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            testSettings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings=testSettings;
            mouse=InputSystem.AddDevice<Mouse>();keyboard=InputSystem.AddDevice<Keyboard>();
            gamepad=InputSystem.AddDevice<Gamepad>();touchscreen=InputSystem.AddDevice<Touchscreen>();
            InputSystem.QueueStateEvent(mouse,new MouseState());InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            InputSystem.QueueStateEvent(gamepad,new GamepadState());yield return null;
            yield return GameSceneNavigator.Instance.SwitchAsync(GameSceneId.DroneFlight).ToCoroutine();
            yield return Wait(()=>UIManager.Instance.Get<DroneFlightVehicleSelectView>()?.State==ViewState.Visible,"选择页",90);
            yield return null;yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if(originalNavigator!=null){SetNavigator(originalNavigator);originalNavigator=null;}
            if(delayed?.HasPending==true)
            {
                LogAssert.Expect(LogType.Error,new Regex(@"\[DroneFlight\] 机型准备失败："));
                delayed.CompleteInvalidModel();yield return null;yield return null;
            }
            if(keyboard!=null&&keyboard.added)InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            if(gamepad!=null&&gamepad.added)InputSystem.QueueStateEvent(gamepad,new GamepadState());
            yield return null;
            if(GameSceneNavigator.Instance!=null&&!GameSceneNavigator.Instance.IsTransitioning&&GameSceneNavigator.Instance.CurrentScene!=GameSceneId.Hub)
                yield return GameSceneNavigator.Instance.SwitchAsync(GameSceneId.Hub).ToCoroutine();
            foreach(var device in new InputDevice[]{mouse,keyboard,gamepad,touchscreen})if(device!=null&&device.added)InputSystem.RemoveDevice(device);
            if(originalSettings!=null)InputSystem.settings=originalSettings;
            if(testSettings!=null)Object.Destroy(testSettings);
            delayed=null;
        }

        private IEnumerator Click(Button button)
        {
            Assert.That(button.interactable,Is.True);
            var point=Position(button);
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=point},hits);
            Assert.That(hits,Is.Not.Empty);
            Assert.That(hits[0].gameObject.GetComponentInParent<Button>(),Is.SameAs(button));
            yield return ClickAt(point);
        }
        private IEnumerator ClickAt(Vector2 point)
        {
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point}.WithButton(MouseButton.Left));yield return null;
            InputSystem.QueueStateEvent(mouse,new MouseState{position=point});yield return null;yield return null;
        }
        private IEnumerator Tap(Button button)
        {
            var point=Position(button);
            InputSystem.QueueStateEvent(touchscreen,new TouchState{touchId=1,position=point,phase=UnityEngine.InputSystem.TouchPhase.Began});yield return null;
            InputSystem.QueueStateEvent(touchscreen,new TouchState{touchId=1,position=point,phase=UnityEngine.InputSystem.TouchPhase.Ended});yield return null;yield return null;
        }
        private static Vector2 Position(Button button)
        {
            Canvas.ForceUpdateCanvases();var rect=(RectTransform)button.transform;
            return RectTransformUtility.WorldToScreenPoint(button.GetComponentInParent<Canvas>().worldCamera,rect.TransformPoint(rect.rect.center));
        }
        private static T Bound<T>(View view,string name) where T:Component
        {
            foreach(var component in view.gameObject.GetComponent<ComponentItemIndex>().Components)
                if(component is T typed&&typed.name==name)return typed;
            throw new InvalidOperationException("Missing binding: "+name);
        }
        private static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(owner);
        private static void SetField(object owner,string name,object value)=>owner.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(owner,value);
        private static void SetNavigator(GameSceneNavigator value)=>typeof(GameSceneNavigator).GetProperty(nameof(GameSceneNavigator.Instance)).SetValue(null,value);
        private static IEnumerator Wait(Func<bool> predicate,string reason,float timeout=30)
        {
            double until=Time.realtimeSinceStartupAsDouble+timeout;
            while(!predicate()&&Time.realtimeSinceStartupAsDouble<until)yield return null;
            Assert.That(predicate(),Is.True,reason+"超时");
        }

        private sealed class DeferredFirstLoader:IResourceLoader
        {
            private readonly IResourceLoader inner;
            private UniTaskCompletionSource<GameObject> pending;
            private Transform parent;
            private GameObject invalidModel;
            internal int Requests{get;private set;}
            internal bool ReleasedInvalidModel{get;private set;}
            internal bool HasPending=>pending!=null;
            internal DeferredFirstLoader(IResourceLoader inner)=>this.inner=inner;
            internal void CompleteInvalidModel()
            {
                invalidModel=new GameObject("InvalidTestDrone");invalidModel.transform.SetParent(parent,false);
                var result=pending;pending=null;result.TrySetResult(invalidModel);
            }
            public UniTask<GameObject> InstantiateAsync(string address,Transform target,bool worldPositionStays)
            {
                Requests++;
                if(Requests!=1)return inner.InstantiateAsync(address,target,worldPositionStays);
                parent=target;pending=new UniTaskCompletionSource<GameObject>();return pending.Task;
            }
            public UniTask<GameObject> InstantiateAsync(string address,Transform target)=>InstantiateAsync(address,target,false);
            public GameObject Instantiate(string address,Transform target)=>inner.Instantiate(address,target);
            public GameObject Instantiate(string address,Transform target,bool worldPositionStays)=>inner.Instantiate(address,target,worldPositionStays);
            public T LoadAsset<T>(string address)where T:Object=>inner.LoadAsset<T>(address);
            public UniTask<T> LoadAssetAsync<T>(string address)where T:Object=>inner.LoadAssetAsync<T>(address);
            public void ReleaseAsset(Object asset)=>inner.ReleaseAsset(asset);
            public void ReleaseInstance(GameObject value)
            {
                if(value==invalidModel){ReleasedInvalidModel=true;Object.Destroy(value);}else inner.ReleaseInstance(value);
            }
            public void Dispose()=>inner.Dispose();
        }
        private sealed class FailedReturnRuntime:IGameSceneRuntime
        {
            public UniTask<GameSceneRuntimeResult> LoadAsync(string address,Action<float> progress)=>UniTask.FromResult(GameSceneRuntimeResult.Success());
            public UniTask<GameSceneRuntimeResult> ReturnToHubAsync(Action<float> progress)=>UniTask.FromResult(GameSceneRuntimeResult.Failure("测试返回失败"));
        }
    }
}
#endif
