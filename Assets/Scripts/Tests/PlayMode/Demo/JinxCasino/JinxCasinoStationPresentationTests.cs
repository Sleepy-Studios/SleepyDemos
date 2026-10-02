#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using Hotfix;
using Hotfix.JinxCasino.Adapters;
using Hotfix.JinxCasino.Persistence;
using Hotfix.JinxCasino.Adapters.UI;
using Hotfix.JinxCasino.Rules;
using Hotfix.SceneManagement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.Demo
{
    /// 正式资源的可观察机台动作；从实际练习操作及公开投影验证，不窥探随机数或未揭示密码。
    public sealed class JinxCasinoStationPresentationTests
    {
        private JinxCasinoController controller;
        private JinxCasinoAdventurePresenter presenter;
        private JinxCasinoGameSettings settings;
        private CharacterController body;
        private Camera camera;
        private GameViewResolution resolution;
        private JinxCasinoStation[] stations;
        private string saveDirectory;
        private int request;

        [UnityTest, Timeout(180000)]
        public IEnumerator LeverUiAndSavedLampsFollowActualWindowsAndPreparedWrench()
        {
            yield return EnterSavedDemo();
            yield return StartPracticeFromSavedButton();
            var station = Station(CasinoGameKind.CooperativeLevers);
            var lamps = Enumerable.Range(0, 6).Select(index => Node(station, "CooperativeLevers.TimingFace" + index).GetComponentInChildren<Renderer>()).ToArray();
            var left = Node(station, "CooperativeLevers.Lever0");
            var npc = Node(station, "CooperativeLevers.Lever1");
            Quaternion leftRest = left.localRotation, npcRest = npc.localRotation;
            presenter.ShowStation(CasinoGameKind.CooperativeLevers);
            SetWire(Field<object>(presenter, "stakeInput"), "10");
            SetWire(Field<object>(presenter, "choiceInput"), "0");
            yield return null;
            Click(Field<Button>(presenter, "confirmBetButton")); yield return null;
            SetWire(Field<object>(presenter, "actionInput"), "0");
            Assert.That(controller.Game.GetPresentation().CooperationHelpUsed, Is.False);
            Assert.That(controller.Game.GetPresentation().SelectedValues, Has.Length.EqualTo(2), "只控制0号与单人NPC1，不伪造6名联网玩家。");
            yield return WaitLeverCycle(100);
            var plainDark = lamps[0].sharedMaterials;
            AssertLeverCaption("等待窗口");
            Color darkButton = PullButton().targetGraphic.color;
            yield return WaitLeverCycle(300);
            AssertLeverCaption("窗口开放");
            Assert.That(LampLight(lamps[0]), Is.GreaterThan(MaterialLight(plainDark) + .1f), "真实模型表盘需要亮起。");
            Assert.That(PullButton().targetGraphic.color.g, Is.GreaterThan(darkButton.g));
            yield return Screenshot("P4LeverPlainOpen");
            yield return WaitLeverCycle(500);
            AssertLeverCaption("等待窗口");
            Assert.That(lamps[0].sharedMaterials, Is.EqualTo(plainDark), "普通窗口在500毫秒已关闭。");
            yield return Wait(() => controller.Game.GetPresentation().ElapsedMilliseconds >= 2000 && Cycle() >= 230 && Cycle() <= 380, "下一普通自然周期的开放窗口", 10);
            Click(PullButton()); yield return null;
            Assert.That(controller.Game.GetPresentation().SelectedValues[0], Is.EqualTo(1));
            Assert.That(Quaternion.Angle(left.localRotation, leftRest), Is.GreaterThan(20), "真实按钮应驱动保存的0号拉杆。");
            yield return Wait(() => !controller.Game.HasActiveRound, "NPC1在同周期完成真实协作", 5);
            Assert.That(controller.Game.GetPresentation().Payout, Is.EqualTo(40));
            Assert.That(controller.Game.GetPresentation().SelectedValues[1], Is.EqualTo(1));
            yield return Wait(() => Quaternion.Angle(npc.localRotation, npcRest) > 20, "结算后的实际NPC拉杆演出", 2);
            Assert.That(Quaternion.Angle(npc.localRotation, npcRest), Is.GreaterThan(20));

            yield return null;
            Assert.That(controller.Game.PurchaseItem("duo_wrench", Time.frameCount).Success, Is.True); yield return null;
            Assert.That(controller.UseAdventureItem("duo_wrench").Success, Is.True); yield return null;
            Assert.That(controller.Game.State.CooperationHelpCharges, Is.EqualTo(1));
            Click(Field<Button>(presenter, "confirmBetButton")); yield return null;
            Assert.That(controller.Game.GetPresentation().CooperationHelpUsed, Is.True);
            Assert.That(controller.Game.State.CooperationHelpCharges, Is.Zero);
            Assert.That(controller.Game.State.Inventory.Find(entry => entry.ItemId == "duo_wrench")?.Count ?? 0, Is.Zero);
            SetWire(Field<object>(presenter, "actionInput"), "0");
            yield return WaitLeverCycle(100);
            AssertLeverCaption("窗口开放");
            Assert.That(LampLight(lamps[0]), Is.GreaterThan(MaterialLight(plainDark) + .1f), "真实预备道具使100毫秒边界已亮。");
            var inactiveLamps = lamps.Skip(2).Select(renderer => renderer.sharedMaterials).ToArray();
            yield return Screenshot("P4LeverWrenchEarlyWindow");
            yield return Wait(() => Cycle() >= 1300 && Cycle() <= 1600, "无0号操作的自然周期后半段", 8);
            for (int index = 2; index < 6; index++) Assert.That(lamps[index].sharedMaterials, Is.EqualTo(inactiveLamps[index - 2]), "单人未参加编号" + index + "不能因未来窗口冒充玩家灯。");
            yield return Wait(() => controller.Game.GetPresentation().ElapsedMilliseconds >= 2000 && Cycle() >= 410 && Cycle() <= 500, "扳手扩大的晚端窗口", 10);
            AssertLeverCaption("窗口开放");
            Assert.That(LampLight(lamps[0]), Is.GreaterThan(MaterialLight(plainDark) + .1f));
            Click(PullButton()); yield return null;
            yield return Wait(() => !controller.Game.HasActiveRound, "在扩大窗口晚端实际拉下并完成", 5);
            Assert.That(controller.Game.GetPresentation().Payout, Is.EqualTo(40));
            yield return Screenshot("P4LeverWrenchCompleted");
            yield return Frame(station.InteractionPosition + Vector3.back * .7f, station.transform.position + Vector3.up * 1.9f);
            Assert.That(Quaternion.Angle(left.localRotation, leftRest), Is.GreaterThan(20));
            yield return Screenshot("P4LeverFieldCompletedLamps", true);
            AssertSingleListener();
        }

        private long Cycle() => controller.Game.GetPresentation().ElapsedMilliseconds % 2000;
        private IEnumerator WaitLeverCycle(long value)
        { yield return Wait(() => controller.Game.HasActiveRound && Cycle() >= value && Cycle() < value + 100, "实际拉杆周期" + value, 12); yield return new WaitForEndOfFrame(); }
        private Button PullButton()
        {
            int index = Array.FindIndex(controller.Game.GetActions(), action => action.Kind == CasinoMiniGameAction.PullLever);
            Assert.That(index, Is.GreaterThanOrEqualTo(0)); return Field<Button[]>(presenter, "actionButtons")[index];
        }
        private void AssertLeverCaption(string expected)
        {
            var texts = Field<object[]>(presenter, "actionButtonTexts");
            int index = Array.FindIndex(controller.Game.GetActions(), action => action.Kind == CasinoMiniGameAction.PullLever);
            Assert.That(index, Is.GreaterThanOrEqualTo(0));
            Assert.That((string)texts[index].GetType().GetProperty("text").GetValue(texts[index]), Does.Contain(expected));
        }
        private static float LampLight(Renderer renderer) => MaterialLight(renderer.sharedMaterials);
        private static float MaterialLight(Material[] materials) => materials.Sum(material => { var color = material.GetColor("_BaseColor"); return color.r + color.g + color.b; });
        private static void SetWire(object field, string value) => field.GetType().GetProperty("text").SetValue(field, value);
        private static void Click(Button button)
        {
            Assert.That(button != null && button.isActiveAndEnabled && button.interactable, Is.True);
            Canvas.ForceUpdateCanvases(); var rect = (RectTransform)button.transform; var canvas = button.GetComponentInParent<Canvas>();
            var pointer = new PointerEventData(EventSystem.current) { position = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, rect.TransformPoint(rect.rect.center)), button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty); Assert.That(hits[0].gameObject.GetComponentInParent<Button>(), Is.SameAs(button), "实际按钮不可被覆盖。");
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator PracticeModelsFollowRaceTrackFifteenPlinkoSlotsAndRealVaultObjective()
        {
            yield return EnterSavedDemo();
            yield return StartPracticeFromSavedButton();
            Assert.That(controller.Game.GetAvailableGames(), Has.Length.EqualTo(17));
            foreach (var area in Field<JinxCasinoWorldArea[]>(controller, "areas").OrderBy(value => value.Index))
            {
                Assert.That(area.gameObject.activeInHierarchy, Is.True);
                yield return Frame(area.SafePosition + Vector3.up * 0.02f, area.SafePosition + new Vector3(0,1.3f,13));
                yield return Screenshot("P4Area" + area.Index, true);
            }

            var race = Station(CasinoGameKind.MechanicalRace);
            var racers = Enumerable.Range(0,4).Select(index => Node(race,"MechanicalRace.Racer"+index)).ToArray();
            var starts = racers.Select(node => node.localPosition).ToArray();
            var bed = Node(race,"MechanicalRace.Mesh").GetComponent<MeshFilter>().sharedMesh.bounds;
            yield return Frame(race.InteractionPosition + Vector3.back * 0.6f, race.transform.position + Vector3.up * 1.5f);
            Assert.That(controller.Game.BeginGame(Id(),CasinoGameKind.MechanicalRace,10,0, null, Time.frameCount).Success,Is.True); yield return null;
            for(int step=0;step<3;step++) { Assert.That(controller.Game.Act(Id(),CasinoMiniGameAction.Boost, 0, null, Time.frameCount).Success,Is.True); yield return null; }
            yield return Wait(()=>controller.Game.GetPresentation().NumberValues.Any(value=>value>=350),"赛跑已公开实际进度",15);
            yield return null;
            var progress=controller.Game.GetPresentation().NumberValues;
            var distances=racers.Select((node,index)=>starts[index].x-node.localPosition.x).ToArray();
            Assert.That(distances.All(distance=>distance>0),Is.True,"所有跑者沿模型-X轨道前进，不能沿Z横跨车道。");
            for(int index=0;index<4;index++)
            {
                Assert.That(racers[index].localPosition.y,Is.EqualTo(starts[index].y).Within(.0001f));
                Assert.That(racers[index].localPosition.z,Is.EqualTo(starts[index].z).Within(.0001f));
                Assert.That(racers[index].localPosition.x,Is.InRange(bed.min.x,bed.max.x));
            }
            for(int first=0;first<4;first++) for(int second=0;second<4;second++)
                if(progress[first]>progress[second]+150) Assert.That(distances[first],Is.GreaterThan(distances[second]),"模型进度排序应和已公开的大幅领先相符。");
            yield return Screenshot("P4RaceTrack", true);
            yield return Wait(()=>!controller.Game.HasActiveRound,"赛跑真实tick结束",35);

            var plinko=Station(CasinoGameKind.Plinko); var ball=Node(plinko,"Plinko.Ball");
            Vector3 ballStart=ball.localPosition;
            var slotContract=ReadSlotContract(); Assert.That(slotContract.count,Is.EqualTo(15)); Assert.That(slotContract.dividers,Is.EqualTo(16));
            Assert.That(slotContract.indexDirectionUnity,Is.EqualTo("+X to -X"));
            float[] launches=new float[7];
            for(int choice=0;choice<7;choice++)
            {
                yield return null; Assert.That(controller.Game.RefillPractice(Time.frameCount).Success,Is.True); yield return null;
                yield return Frame(plinko.InteractionPosition + Vector3.back*.55f,plinko.transform.position+Vector3.up*2.0f);
                Assert.That(controller.Game.BeginGame(Id(),CasinoGameKind.Plinko,10,choice, null, Time.frameCount).Success,Is.True); yield return null;
                launches[choice]=ball.localPosition.x;
                Assert.That(controller.Game.GetPresentation().Cursor,Is.EqualTo(choice));
                Assert.That(controller.Game.Act(Id(),CasinoMiniGameAction.DropBall,choice, null, Time.frameCount).Success,Is.True); yield return null;
                if(choice==0 || choice==6) yield return Screenshot("P4PlinkoLaunch"+choice, true);
                yield return Wait(()=>!controller.Game.HasActiveRound,"弹珠八层实际开奖",12); yield return null;
                var result=controller.Game.GetPresentation(); Assert.That(result.Level,Is.EqualTo(8)); Assert.That(result.Cursor,Is.InRange(0,14));
                // 按美术公开的15槽中心检查最终落点；不是复写动画逐层插值或RNG。
                float center=(7-result.Cursor)*slotContract.pitchMetres;
                Assert.That(ball.localPosition.x,Is.EqualTo(center).Within(.002f),"实际终槽和公开Cursor一致。");
                Assert.That(ball.localPosition.y,Is.LessThan(ballStart.y-1.3f));
                Assert.That(ball.localPosition.z,Is.GreaterThan(ballStart.z+.20f),"到达正面托盘，不能留在背板后方。");
                if(choice==6) yield return Screenshot("P4PlinkoSettled", true);
            }
            Assert.That(launches.Distinct().Count(),Is.EqualTo(7),"七个初落不能被减Choice抵消成同一中心。");
            for(int index=1;index<7;index++) Assert.That(launches[index],Is.LessThan(launches[index-1]));
            Assert.That(launches[0],Is.GreaterThan(.30f)); Assert.That(launches[6],Is.LessThan(-.30f));

            yield return null; Assert.That(controller.Game.RefillPractice(Time.frameCount).Success,Is.True); yield return null;
            foreach(string item in new[]{"stop_loss","jackpot_coupon"})
            { Assert.That(controller.Game.PurchaseItem(item, Time.frameCount).Success,Is.True); yield return null; Assert.That(controller.UseAdventureItem(item).Success,Is.True); yield return null; }
            var vault=Station(CasinoGameKind.CooperativeVault); var door=Node(vault,"CooperativeVault.Door");
            Quaternion doorClosed=door.localRotation;
            yield return Frame(vault.InteractionPosition+Vector3.back*.6f,vault.transform.position+Vector3.up*1.4f);
            Assert.That(controller.Game.BeginGame(Id(),CasinoGameKind.CooperativeVault,100,0, null, Time.frameCount).Success,Is.True); yield return null;
            for(int clue=0;clue<3 && controller.Game.GetActions().All(action=>action.Kind!=CasinoMiniGameAction.EnterCode);clue++)
            {
                int hidden=Array.FindIndex(controller.Game.GetPresentation().SelectedValues,value=>value==0);
                Assert.That(hidden,Is.GreaterThanOrEqualTo(0));
                Assert.That(controller.Game.Act(Id(),CasinoMiniGameAction.InspectClue,hidden, null, Time.frameCount).Success,Is.True); yield return null;
            }
            Assert.That(controller.Game.GetActions().Any(action=>action.Kind==CasinoMiniGameAction.EnterCode),Is.True);
            // 密码真实各位为1..6；公开动作允许0..999，000是可提交且肯定错误的尝试。
            for(int attempt=0;attempt<3;attempt++)
            { Assert.That(controller.Game.Act(Id(),CasinoMiniGameAction.EnterCode,0, null, Time.frameCount).Success,Is.True); yield return null; }
            var failedVault=controller.Game.GetPresentation();
            Assert.That(failedVault.IsComplete,Is.True); Assert.That(failedVault.IsObjectiveSuccess,Is.False);
            Assert.That(failedVault.Payout,Is.GreaterThan(failedVault.Cost),"保险+彩金补偿大于投入仍不是开锁胜利。");
            Assert.That(Quaternion.Angle(door.localRotation,doorClosed),Is.LessThan(.01f));
            yield return Screenshot("P4InsuredVaultClosed", true);
            Assert.That(controller.Game.SaveAdventure(1),Is.True);
            var feedback=controller.transform.Find("ClubFeedback").GetComponent<AudioSource>(); feedback.Stop(); yield return null;
            Assert.That(controller.LoadAdventure(1),Is.True); yield return null;
            Assert.That(feedback.isPlaying,Is.False); Assert.That(Quaternion.Angle(door.localRotation,doorClosed),Is.LessThan(.01f));
            var wheel=Node(vault,"CooperativeVault.Wheel"); Quaternion wheelRestored=wheel.localRotation;
            float restoredAt=Time.unscaledTime; yield return Wait(()=>Time.unscaledTime-restoredAt>=.25f,"恢复静态金库展示",3);
            Assert.That(Quaternion.Angle(wheel.localRotation,wheelRestored),Is.LessThan(.01f),"恢复不能重新开始旧开奖自转。");
            Assert.That(feedback.isPlaying,Is.False); AssertSingleListener();
            var old=controller; controller.RequestExit();
            yield return Wait(()=>old==null&&IsStableHub(),"正式退出Hub",30); AssertSingleListener();
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            try
            {
                if(controller!=null)
                { yield return Wait(()=>controller==null||!controller.IsBusy,"等待在途操作",15); if(controller!=null)controller.RequestExit(); yield return Wait(()=>controller==null&&IsStableHub(),"释放机台场景",30); }
            }
            finally
            {
                resolution?.Dispose(); resolution=null; if(settings!=null)Object.Destroy(settings);
                if(controller==null&&saveDirectory!=null)
                { string full=Path.GetFullPath(saveDirectory); Assert.That(full.StartsWith(Path.GetFullPath("Library/JinxCasino/TestSaves")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),Is.True); if(Directory.Exists(full))Directory.Delete(full,true); }
            }
        }
        private IEnumerator EnterSavedDemo()
        {
            if(GameSceneNavigator.Instance==null)
            { var boot=SceneManager.LoadSceneAsync("AppEntrance",LoadSceneMode.Single); Assert.That(boot,Is.Not.Null); yield return Wait(()=>boot.isDone,"正式启动入口",90); }
            yield return Wait(IsStableHub,"稳定Hub",90); Assert.That(GameSceneNavigator.Instance.IsEditorDirect,Is.False);
            resolution=new GameViewResolution(1280,720); yield return Wait(()=>Screen.width==1280&&Screen.height==720,"720p截图视口",15);
            var travel=GameSceneNavigator.Instance.SwitchAsync(GameSceneId.JinxCasino).AsTask(); yield return Wait(()=>travel.IsCompleted,"保存P4场景",45);
            Assert.That(travel.GetAwaiter().GetResult().Status,Is.EqualTo(GameSceneSwitchStatus.Succeeded));
            yield return Wait(()=>UIManager.Instance.Get<JinxCasinoHudView>()?.State==ViewState.Visible,"保存HUD",30);
            controller=Object.FindFirstObjectByType<JinxCasinoController>(); camera=Camera.main; body=camera.GetComponentInParent<CharacterController>();
            presenter=UIManager.Instance.Get<JinxCasinoHudView>().gameObject.GetComponentInChildren<JinxCasinoAdventurePresenter>(true);
            stations=controller.gameObject.scene.GetRootGameObjects().SelectMany(root=>root.GetComponentsInChildren<JinxCasinoStation>(true)).Where(station=>station.GetComponent<JinxCasinoRotationStand>()==null).ToArray();
            Assert.That(stations,Has.Length.EqualTo(17)); Assert.That(stations.All(station=>station.GetComponent<JinxCasinoStationPresentation>()!=null),Is.True);
            saveDirectory=Path.GetFullPath(Path.Combine("Library/JinxCasino/TestSaves","P4Station-"+Guid.NewGuid().ToString("N")));
            controller.Game.SetLocalSaveStore(new CasinoLocalSaveStore(saveDirectory)); controller.Game.SetLocalProfileStore(new CasinoProfileStore(Path.Combine(saveDirectory,"Profile")));
            settings=Object.Instantiate(Field<JinxCasinoGameSettings>(controller,"gameSettings")); var config=settings.CreateConfig();
            config.EventIntervalMilliseconds=0; config.AllowedGames=Array.Empty<CasinoGameKind>(); config.ShopItemIds=Array.Empty<string>();
            typeof(JinxCasinoGameSettings).GetField("adventure",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(settings,config);
            controller.ConfigureAdventure(settings,Field<JinxCasinoWorldArea[]>(controller,"areas"),controller.GetComponent<JinxCasinoSceneEffects>());
            controller.GetComponent<JinxCasinoAudioDirector>().SetVolume(0); AssertSingleListener(); yield return null;
        }
        private IEnumerator StartPracticeFromSavedButton()
        {
            Assert.That(Field<GameObject>(presenter, "menuPanel").activeInHierarchy, Is.True, "必须从保存的开始菜单进入练习。");
            Click(Field<Button>(presenter, "practiceButton"));
            yield return Wait(() => controller.Game.HasAdventure && !Field<GameObject>(presenter, "menuPanel").activeInHierarchy,
                "真实练习按钮建立旅程并关闭开始菜单", 5);
            Assert.That(controller.Game.State.Mode, Is.EqualTo(CasinoAdventureMode.Practice));
            Canvas.ForceUpdateCanvases(); yield return null; yield return null;
            AssertFieldVisible();
        }
        private IEnumerator Frame(Vector3 position,Vector3 target)
        {
            // 场地证据必须经过真实返回场地按钮；不隐藏对象或直接改Presenter窗口状态。
            if (Field<GameObject>(presenter, "machinePanel").activeInHierarchy)
            {
                Click(Field<Button>(presenter, "machineCloseButton"));
                yield return Wait(() => !Field<GameObject>(presenter, "machinePanel").activeInHierarchy,
                    "保存的机台关闭按钮返回场地", 5);
            }
            AssertFieldVisible();
            // 沿既有测试惯例摆放本用例CC；使用已有模态输入边界冻结鼠标，不另建相机或入口。
            body.enabled=false; body.transform.position=position; body.enabled=true; Physics.SyncTransforms();
            controller.BindAdventurePresenter(presenter,true); camera.transform.rotation=Quaternion.LookRotation(target-camera.transform.position,Vector3.up);
            Canvas.ForceUpdateCanvases(); yield return null; yield return null;
            AssertFieldVisible();
        }
        private void AssertFieldVisible()
        {
            Assert.That(presenter.gameObject.activeInHierarchy, Is.True, "保留实际HUD，不能隐藏整张界面制造场地证据。");
            foreach (string panel in new[] { "menuPanel", "machinePanel", "shopPanel", "slotsPanel", "endingPanel", "eventPanel" })
                Assert.That(Field<GameObject>(presenter, panel).activeInHierarchy, Is.False, "场地被模态面板遮挡：" + panel);
            foreach (string field in new[] { "profilePresenter", "localSettingsPresenter", "socialPresenter" })
            {
                var modal = Field<Component>(presenter, field);
                if (modal != null) Assert.That(modal.gameObject.activeInHierarchy, Is.False, "场地被模态界面遮挡：" + field);
            }
        }
        private JinxCasinoStation Station(CasinoGameKind game)=>stations.Single(station=>station.Game==game);
        private static Transform Node(JinxCasinoStation station,string name)=>station.GetComponentsInChildren<Transform>(true).Single(node=>node.name==name);
        private string Id()=>"p4-station-"+(++request);
        private static SlotContract ReadSlotContract()=>JsonUtility.FromJson<Manifest>(File.ReadAllText("Assets/LoadResources/Demos/jinx_casino/Art/Models/manifest.json")).models.Single(model=>model.name=="Plinko").slots;
        [Serializable]private sealed class Manifest{public Model[] models;}
        [Serializable]private sealed class Model{public string name;public SlotContract slots;}
        [Serializable]private sealed class SlotContract{public int count,dividers;public float pitchMetres;public string indexDirectionUnity;}
        private static T Field<T>(object target,string name)=> (T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(target);
        private static bool IsStableHub()=>GameSceneNavigator.Instance!=null&&GameSceneNavigator.Instance.CurrentScene==GameSceneId.Hub&&!GameSceneNavigator.Instance.IsTransitioning&&UIManager.Instance.Get<MainMenuView>()?.State==ViewState.Visible;
        private static void AssertSingleListener()=>Assert.That(Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Count(listener=>listener.isActiveAndEnabled),Is.EqualTo(1));
        private IEnumerator Screenshot(string label, bool fieldEvidence = false)
        {
            // 所有机台证据都必须先完成正式菜单关闭；场地图再额外拒绝任意仍打开的模态面板。
            Canvas.ForceUpdateCanvases(); yield return null; yield return null;
            Assert.That(Field<GameObject>(presenter, "menuPanel").activeInHierarchy, Is.False, "开始菜单遮挡截图：" + label);
            if (fieldEvidence) AssertFieldVisible();
            Canvas.ForceUpdateCanvases();
            string directory=Path.GetFullPath("Library/JinxCasino/Verification"); Directory.CreateDirectory(directory);
            string path=Path.Combine(directory,label+"-"+DateTime.UtcNow.ToString("yyyyMMddHHmmss")+"-"+Guid.NewGuid().ToString("N")+".png");
            ScreenCapture.CaptureScreenshot(path); yield return null; yield return null; yield return Wait(()=>File.Exists(path)&&new FileInfo(path).Length>0,"截图"+label,15); Debug.Log("Jinx P4 screenshot: "+path);
        }
        private static IEnumerator Wait(Func<bool> predicate,string reason,float timeout)
        { float deadline=Time.realtimeSinceStartup+timeout; while(!predicate()){if(Time.realtimeSinceStartup>deadline)Assert.Fail("等待超时："+reason);yield return null;} }
    }
}
#endif
