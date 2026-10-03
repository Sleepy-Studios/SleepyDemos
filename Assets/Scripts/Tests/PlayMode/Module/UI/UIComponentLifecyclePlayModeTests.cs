#if UNITY_EDITOR
using System.Collections;
using Core.Runtime;
using Core.Runtime.Inputs;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TMPro;
using Object = UnityEngine.Object;

namespace Tests.Module
{
    public sealed class UIComponentLifecyclePlayModeTests
    {
        [UnityTest]
        public IEnumerator CanceledPointerHoldDoesNotConsumeLaterSubmit()
        {
            var events = new GameObject("CommandEvents", typeof(EventSystem));
            var root = new GameObject("Command", typeof(RectTransform), typeof(Button), typeof(InputCommandButton));
            var command = root.GetComponent<InputCommandButton>();
            try
            {
                var fields = new SerializedObject(command);
                fields.FindProperty("hold").boolValue = true; fields.ApplyModifiedPropertiesWithoutUndo();
                int clicks = 0, releases = 0;
                command.Clicked += _ => clicks++;
                command.HoldChanged += (_, held) => { if (!held) releases++; };
                var pointer = new PointerEventData(events.GetComponent<EventSystem>()) { pointerId = 4 };
                command.OnPointerDown(pointer); command.OnPointerUp(pointer);
                root.GetComponent<Button>().onClick.Invoke();
                Assert.That(clicks, Is.Zero, "同帧指针 Click 不能再提交一次保持命令。");
                yield return null;
                root.GetComponent<Button>().OnSubmit(new BaseEventData(events.GetComponent<EventSystem>()));
                Assert.That(clicks, Is.EqualTo(1), "拖出取消不能留下吞掉下一次确认的标记。");
                Assert.That(releases, Is.EqualTo(1));
            }
            finally { Object.Destroy(root); Object.Destroy(events); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator LongTextShrinksAndShortTextRestoresExplicitDesignSize()
        {
            var canvas = new GameObject("TextCanvas", typeof(Canvas));
            var root = new GameObject("AutoFit", typeof(RectTransform)); root.transform.SetParent(canvas.transform);
            root.AddComponent<TextMeshProUGUI>();
            try
            {
                var text = root.GetComponent<TextMeshProUGUI>();
                text.font = TMP_Settings.defaultFontAsset;
                text.fontSizeMin = 10; text.fontSize = 36;
                var fit = root.AddComponent<TMPAutoFitLayoutElement>();
                fit.SetDesignFontSize(36); fit.SetMaxWidth(100); fit.SetMaxHeight(40);
                text.text = "A very long sentence that requires several lines inside a small box.";
                fit.RefreshLayout(); text.ForceMeshUpdate(); yield return null;
                Assert.That(text.fontSize, Is.LessThan(36));
                text.text = "A"; fit.RefreshLayout(); text.ForceMeshUpdate();
                Assert.That(text.fontSize, Is.EqualTo(36).Within(.01f));
                Assert.That(fit.DesignFontSize, Is.EqualTo(36));
            }
            finally { Object.Destroy(canvas); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator MouseMinusOnePointerCannotReenterAndDisableReleasesOwnership()
        {
            var events = new GameObject("PressFixtureEvents", typeof(EventSystem));
            var first = new GameObject("First", typeof(RectTransform), typeof(PressButton));
            var second = new GameObject("Second", typeof(RectTransform), typeof(PressButton));
            var pointer = new PointerEventData(events.GetComponent<EventSystem>()) { pointerId = -1 };
            try
            {
                int down = 0;
                var source = first.GetComponent<PressButton>(); source.OnMouseDown.AddListener(() => down++);
                source.OnPointerDown(pointer); source.OnPointerDown(pointer);
                Assert.That(down, Is.EqualTo(1), "同一个鼠标按下不能重复进入。");
                second.GetComponent<PressButton>().OnPointerDown(pointer);
                Assert.That(second.GetComponent<PressButton>().IsPressed, Is.False);
                first.SetActive(false);
                second.GetComponent<PressButton>().OnPointerDown(pointer);
                Assert.That(second.GetComponent<PressButton>().IsPressed, Is.True, "禁用时释放合法的 -1 指针所有权。");
                second.GetComponent<PressButton>().OnPointerUp(pointer);
            }
            finally { Object.Destroy(first); Object.Destroy(second); Object.Destroy(events); }
            yield return null;
        }

        [UnityTest]
        public IEnumerator HiddenAsyncTabDoesNotNotifyAndCanInitializeAgainSynchronously()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/LoadResources/UI/Common/_TemplateInstantiatePrefab/Tab/Tab.prefab");
            var root = Object.Instantiate(prefab);
            var tab = root.GetComponent<UITab>();
            try
            {
                int completed = 0;
                tab.Init(new[] { "一", "二", "三", "四" }, action: () => completed++, isAsync: true);
                root.SetActive(false);
                yield return null; yield return null;
                Assert.That(completed, Is.Zero, "隐藏后旧异步任务不能交付完成回调。");
                root.SetActive(true);
                tab.Init(new[] { "新的 A", "新的 B" }, initIndex: 1, action: () => completed++);
                Assert.That(completed, Is.EqualTo(1));
                Assert.That(tab.Index, Is.EqualTo(1));
                Assert.That(tab.Items[0].activeSelf, Is.True);
                Assert.That(tab.Items[1].activeSelf, Is.True);
                for (int i = 2; i < tab.Count; i++) Assert.That(tab.Items[i].activeSelf, Is.False);
            }
            finally { Object.Destroy(root); }
            yield return null;
        }
    }
}
#endif
