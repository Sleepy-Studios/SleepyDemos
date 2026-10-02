#if UNITY_EDITOR
using System;
using System.Collections;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using Hotfix;
using Hotfix.SceneManagement;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Tests.Module
{
    /// 已保存的公共Widget只隐藏拥有的控件；测试初始化顺序、作用域计数和实际输入覆盖面。
    public sealed class GraphicsSettingsEntryScopeTests
    {
        private IDisposable first;
        private IDisposable second;
        private bool originalPanel;
        private bool captured;

        [UnitySetUp]
        public IEnumerator Setup()
        {
            if (GameSceneNavigator.Instance == null)
            { var startup = SceneManager.LoadSceneAsync("AppEntrance", LoadSceneMode.Single); Assert.That(startup, Is.Not.Null); yield return Wait(() => startup.isDone, "AppEntrance", 90); }
            yield return Wait(() => GameSceneNavigator.Instance != null && GameSceneNavigator.Instance.CurrentScene == GameSceneId.Hub &&
                !GameSceneNavigator.Instance.IsTransitioning && UIManager.Instance.Get<MainMenuView>()?.State == ViewState.Visible, "真实Hub", 90);
            Assert.That(GraphicsSettingsUI.IsEntrySuppressed, Is.False, "上一使用者必须释放作用域。");
            yield return EnsureEntry();
            originalPanel = UIManager.Instance.Get<DlssSettingsView>().IsSettingsPanelOpen; captured = true;
        }

        [UnityTest]
        public IEnumerator ScopeBeforeInitializeHidesControlsWhileManagedViewStaysVisible()
        {
            var existing = UIManager.Instance.Get<DlssSettingsView>();
            yield return UIManager.Instance.CloseAsync(existing, false).ToCoroutine();
            first = GraphicsSettingsUI.SuppressEntry();
            yield return EnsureEntry();
            var view = UIManager.Instance.Get<DlssSettingsView>();
            AssertSuppressed(view); int count = UIManager.Instance.StackCount;
            yield return EnsureEntry();
            Assert.That(UIManager.Instance.Get<DlssSettingsView>(), Is.SameAs(view)); Assert.That(UIManager.Instance.StackCount, Is.EqualTo(count));
            AssertSuppressed(view);
            first.Dispose(); first = null;
            Assert.That(view.State, Is.EqualTo(ViewState.Visible));
            Assert.That(view.transform.Find("OpenButton").gameObject.activeSelf, Is.True);
            Assert.That(view.IsSettingsPanelOpen, Is.False, "初始化默认关闭的面板不能被释放作用域自动打开。");
        }

        [UnityTest]
        public IEnumerator NestedLeasesAndDoubleDisposeRestorePriorOpenPanelOnSameView()
        {
            var view = UIManager.Instance.Get<DlssSettingsView>(); view.SetSettingsPanelOpen(true);
            int count = UIManager.Instance.StackCount;
            first = GraphicsSettingsUI.SuppressEntry(); second = GraphicsSettingsUI.SuppressEntry();
            AssertSuppressed(view); view.SetSettingsPanelOpen(true);
            Assert.That(view.IsSettingsPanelOpen, Is.False, "其它调用方不能重开已抑制的内部面板。");
            first.Dispose(); first.Dispose(); first = null;
            Assert.That(GraphicsSettingsUI.IsEntrySuppressed, Is.True); AssertSuppressed(view);
            second.Dispose(); second = null;
            Assert.That(GraphicsSettingsUI.IsEntrySuppressed, Is.False);
            Assert.That(UIManager.Instance.Get<DlssSettingsView>(), Is.SameAs(view)); Assert.That(UIManager.Instance.StackCount, Is.EqualTo(count));
            Assert.That(view.State, Is.EqualTo(ViewState.Visible)); Assert.That(view.IsSettingsPanelOpen, Is.True);
            Assert.That(view.transform.Find("OpenButton").gameObject.activeSelf, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HidingOldViewUnsubscribesAndReplacementStillHonorsCurrentLease()
        {
            var old = UIManager.Instance.Get<DlssSettingsView>(); old.SetSettingsPanelOpen(true);
            first = GraphicsSettingsUI.SuppressEntry();
            yield return UIManager.Instance.CloseAsync(old, false).ToCoroutine();
            Assert.That(old.State, Is.EqualTo(ViewState.Destroyed));
            yield return EnsureEntry(); var replacement = UIManager.Instance.Get<DlssSettingsView>(); AssertSuppressed(replacement);
            first.Dispose(); first = null;
            Assert.That(replacement.State, Is.EqualTo(ViewState.Visible)); Assert.That(replacement.IsSettingsPanelOpen, Is.False);
            Assert.That(replacement.transform.Find("OpenButton").gameObject.activeSelf, Is.True);
            second = GraphicsSettingsUI.SuppressEntry(); AssertSuppressed(replacement);
            second.Dispose(); second = null;
        }

        private static void AssertSuppressed(DlssSettingsView view)
        {
            Assert.That(view.State, Is.EqualTo(ViewState.Visible)); Assert.That(view.gameObject.activeSelf, Is.True);
            Assert.That(view.transform.Find("OpenButton").gameObject.activeSelf, Is.False);
            Assert.That(view.transform.Find("SettingsPanel").gameObject.activeSelf, Is.False);
            Assert.That(view.gameObject.GetComponents<Graphic>(), Is.Empty, "保存的空根不能新增遮挡射线的Graphic。");
            Assert.That(view.gameObject.GetComponentsInChildren<Graphic>(), Is.Empty, "两个隐藏子树必须覆盖本Widget全部输入Graphic。");
        }
        private static IEnumerator EnsureEntry()
        { GraphicsSettingsUI.Initialize().Forget(); yield return null; yield return Wait(() => UIManager.Instance.Get<DlssSettingsView>()?.State == ViewState.Visible, "公共入口真实初始化", 5); yield return null; }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            first?.Dispose(); first = null; second?.Dispose(); second = null;
            if (!captured) yield break;
            yield return EnsureEntry(); UIManager.Instance.Get<DlssSettingsView>().SetSettingsPanelOpen(originalPanel);
        }
        private static IEnumerator Wait(Func<bool> condition, string description, float seconds)
        { float deadline = Time.realtimeSinceStartup + seconds; while (!condition() && Time.realtimeSinceStartup < deadline) yield return null; Assert.That(condition(), Is.True, description + "超时"); }
    }
}
#endif
