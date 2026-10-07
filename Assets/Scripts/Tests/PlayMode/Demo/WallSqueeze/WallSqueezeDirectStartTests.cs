#if UNITY_EDITOR
using System.Collections;
using Core.Runtime;
using Hotfix;
using Hotfix.SceneManagement;
using Hotfix.WallSqueeze;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TMPro;

namespace Tests.Demo
{
    /// Test Runner 从保存的 Main 场景进入 Play，验证真实 Editor 直启旁路。
    public sealed class WallSqueezeDirectStartTests
    {
        [UnityTest, Timeout(90000)]
        public IEnumerator SavedSceneDirectStartUsesOwnSceneIdAndFlatCamera()
        {
            yield return EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/LoadResources/Demos/wall_squeeze/Scenes/Main.unity",
                new LoadSceneParameters(LoadSceneMode.Single));
            float deadline = Time.realtimeSinceStartup + 45;
            WallSqueezeWorld world = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                world = Object.FindAnyObjectByType<WallSqueezeWorld>();
                if (world?.Simulation != null && UIManager.Instance.Get<WallSqueezeHudView>()?.State == ViewState.Visible)
                {
                    break;
                }
                yield return null;
            }
            Assert.That(world?.Simulation, Is.Not.Null);
            Assert.That(GameSceneNavigator.Instance.IsEditorDirect, Is.True);
            Assert.That(GameSceneNavigator.Instance.CurrentScene, Is.EqualTo(GameSceneId.WallSqueeze));
            Assert.That(Camera.main.orthographic, Is.True);
            Assert.That(Camera.main.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
            Assert.That(Camera.main.backgroundColor.r, Is.EqualTo(244f / 255).Within(.001f));
            Assert.That(world.Simulation.Remaining, Is.EqualTo(4));
            var hud = UIManager.Instance.Get<WallSqueezeHudView>();
            foreach (var graphic in hud.gameObject.GetComponentsInChildren<Graphic>(true))
            {
                Assert.That(UIRootManager.Instance.UICamera.cullingMask & (1 << graphic.gameObject.layer), Is.Not.Zero,
                    "HUD 图形必须位于公共 UI 相机可渲染的层");
            }
            Assert.That(world.LevelCount, Is.EqualTo(6));
            foreach (var button in hud.gameObject.GetComponentsInChildren<Button>())
            {
                var state = button.transform.Find("InteractionFeedback").GetComponent<UIState>();
                state.ApplyState("Normal");
                Assert.That(button.targetGraphic.color.r, Is.EqualTo(244f / 255).Within(.001f));
                Assert.That(button.GetComponentInChildren<TMP_Text>().color, Is.EqualTo(Color.black));
                state.ApplyState("Focused");
                Assert.That(button.targetGraphic.color.b, Is.EqualTo(140f / 255).Within(.001f));
                Assert.That(button.GetComponentInChildren<TMP_Text>().color.r, Is.EqualTo(244f / 255).Within(.001f));
                state.ApplyState("Normal");
            }
            // 经正式下一关入口走到混合关，检查保存模板、计时与 HUD 的实际接线。
            for (int stage = 0; stage < 5; stage++)
            {
                world.Simulation.Reset();
                switch (stage)
                {
                    case 0:
                        world.Simulation.Advance(2.2f, 0, 15.65f);
                        break;
                    case 1:
                        world.Simulation.Advance(1.65f, 0, 15.65f);
                        world.Simulation.Advance(1.7f, 1, .35f);
                        break;
                    case 2:
                        world.Simulation.Advance(1.55f, 0, 7.2f);
                        world.Simulation.Advance(1.65f, 1, .35f);
                        break;
                    case 3:
                        world.Simulation.Advance(2, 0, 15.65f);
                        world.Simulation.Advance(1.95f, 0, 7.5f);
                        world.Simulation.Advance(1.7f, 1, .35f);
                        break;
                    case 4:
                        world.Simulation.Advance(4, 0, 15.65f);
                        break;
                }
                Assert.That(world.Simulation.Result, Is.EqualTo(WallSqueezeResult.Won));
                yield return null;
                world.Dispatch(WallSqueezeCommand.Next);
                deadline = Time.realtimeSinceStartup + 15;
                while ((world.LevelIndex != stage + 1 || world.Input.Router.Context != Core.Runtime.Inputs.GameplayInputContext.Interaction)
                    && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }
                Assert.That(world.LevelIndex, Is.EqualTo(stage + 1));
                yield return null;
            }
            Assert.That(world.HasTimeLimit, Is.True);
            var bodies = world.transform.Find("Bodies");
            int armored = world.Simulation.Bodies.FindIndex(body => body.Type == WallSqueezeMonsterType.Armored);
            int slipper = world.Simulation.Bodies.FindIndex(body => body.Type == WallSqueezeMonsterType.Slipper);
            Assert.That(bodies.GetChild(armored).Find("ArmorLeft"), Is.Not.Null);
            Assert.That(bodies.GetChild(slipper).Find("SpeedUpper"), Is.Not.Null);
            Assert.That(bodies.GetChild(slipper).Find("Shape").GetComponent<SpriteRenderer>().bounds.size.y, Is.EqualTo(.4f).Within(.001f));
            var clock = hud.gameObject.transform.Find("Header/StageCell/Clock").GetComponent<TMP_Text>();
            Assert.That(clock.gameObject.activeInHierarchy, Is.True);
            Assert.That(clock.text, Does.Contain("倒计时"));
            world.Dispatch(WallSqueezeCommand.Pause);
            float frozen = world.Simulation.ElapsedSeconds;
            yield return null;
            yield return null;
            Assert.That(world.Simulation.ElapsedSeconds, Is.EqualTo(frozen));
            world.Dispatch(WallSqueezeCommand.Retry);
            Assert.That(world.LevelIndex, Is.EqualTo(5));
            Assert.That(world.Simulation.ElapsedSeconds, Is.Zero);
        }
    }
}
#endif
