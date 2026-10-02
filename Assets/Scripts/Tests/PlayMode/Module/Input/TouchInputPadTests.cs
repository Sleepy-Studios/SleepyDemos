using Core.Runtime.Inputs;
#if UNITY_EDITOR
using GameViewResolution = Tests.Demo.GameViewResolution;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Tests.Module
{
    /// 双指与跨分辨率输入契约；合成指针验证处理逻辑，不替代 Android 真机手感验收。
    public sealed class TouchInputPadTests
    {
        private GameObject moveObject;
        private GameObject lookObject;
        private TouchInputPad movePad;
        private TouchInputPad lookPad;
        private GameViewResolution resolution;

        [SetUp]
        public void SetUp()
        {
            moveObject = new GameObject("move input test");
            lookObject = new GameObject("look input test");
            movePad = moveObject.AddComponent<TouchInputPad>();
            lookPad = lookObject.AddComponent<TouchInputPad>();
            movePad.Configure(false);
            lookPad.Configure(true);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            resolution?.Dispose();
            resolution = null;
            Object.Destroy(moveObject);
            Object.Destroy(lookObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator EqualScreenFractionProducesEqualMoveAndLookAt540pAnd1080p()
        {
            foreach (int height in new[] { 540, 1080 })
            {
                resolution = new GameViewResolution(height * 16 / 9, height);
                double deadline = Time.realtimeSinceStartupAsDouble + 15;
                while (Screen.height != height && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                Assert.That(Screen.height, Is.EqualTo(height), "测试必须在真实 Game View 分辨率下验证换算。");

                movePad.OnPointerDown(Pointer(11, Vector2.zero));
                movePad.OnDrag(Pointer(11, new Vector2(height / 18f, 0)));
                AssertVector(movePad.Move, new Vector2(0.5f, 0), "同屏幕比例的摇杆位移必须产生相同移动强度。");

                lookPad.OnPointerDown(Pointer(22, Vector2.zero));
                lookPad.OnDrag(Pointer(22, Vector2.zero, new Vector2(height / 12f, -height / 24f)));
                AssertVector(lookPad.ConsumeLook(), new Vector2(60, -30), "同屏幕比例的滑动必须产生相同视角增量。");
                movePad.ResetInput();
                lookPad.ResetInput();
                resolution.Dispose();
                resolution = null;
                yield return null;
            }
        }

        [Test]
        public void TwoFingersRemainIndependentAndLookIsConsumedOnce()
        {
            movePad.OnPointerDown(Pointer(11, Vector2.zero));
            lookPad.OnPointerDown(Pointer(22, Vector2.zero));
            movePad.OnDrag(Pointer(11, new Vector2(Screen.height / 9f, 0)));
            AssertVector(movePad.Move, Vector2.right, "移动指针继续保持移动。");

            lookPad.OnPointerDown(Pointer(33, Vector2.zero));
            lookPad.OnDrag(Pointer(33, Vector2.zero, new Vector2(500, 500)));
            AssertVector(lookPad.ConsumeLook(), Vector2.zero, "额外指针不能抢走视角区域。");
            lookPad.OnDrag(Pointer(22, Vector2.zero, new Vector2(Screen.height / 12f, 0)));
            AssertVector(lookPad.ConsumeLook(), new Vector2(60, 0), "视角只响应自己的指针。");
            AssertVector(lookPad.ConsumeLook(), Vector2.zero, "同一滑动不能在后续帧重复转向。");

            movePad.OnPointerUp(Pointer(22, Vector2.zero));
            AssertVector(movePad.Move, Vector2.right, "另一根手指松开不能停止移动。");
            movePad.OnPointerUp(Pointer(11, Vector2.zero));
            AssertVector(movePad.Move, Vector2.zero, "自己的手指松开必须立即停止移动。");
        }

        [Test]
        public void DisableAndResetReleasePointerOwnershipAndDiscardPendingInput()
        {
            movePad.OnPointerDown(Pointer(11, Vector2.zero));
            movePad.OnDrag(Pointer(11, new Vector2(Screen.height / 9f, 0)));
            lookPad.OnPointerDown(Pointer(22, Vector2.zero));
            lookPad.OnDrag(Pointer(22, Vector2.zero, new Vector2(100, 100)));
            moveObject.SetActive(false);
            lookPad.ResetInput();
            AssertVector(movePad.Move, Vector2.zero, "界面隐藏必须清除持续移动。");
            AssertVector(lookPad.ConsumeLook(), Vector2.zero, "失焦清理必须丢弃未消费的视角输入。");

            moveObject.SetActive(true);
            movePad.OnDrag(Pointer(11, new Vector2(1000, 1000)));
            lookPad.OnDrag(Pointer(22, Vector2.zero, new Vector2(1000, 1000)));
            AssertVector(movePad.Move, Vector2.zero, "旧触摸不能在界面重新启用后恢复移动。");
            AssertVector(lookPad.ConsumeLook(), Vector2.zero, "旧触摸不能在输入恢复后继续转向。");
            movePad.OnPointerDown(Pointer(33, Vector2.zero));
            movePad.OnDrag(Pointer(33, new Vector2(Screen.height / 9f, 0)));
            AssertVector(movePad.Move, Vector2.right, "释放所有权后可以接收新的手指。");
        }

        private static PointerEventData Pointer(int id, Vector2 position, Vector2 delta = default)
        {
            return new PointerEventData(EventSystem.current) { pointerId = id, position = position, delta = delta };
        }

        private static void AssertVector(Vector2 actual, Vector2 expected, string message)
        {
            Assert.That(Vector2.Distance(actual, expected), Is.LessThan(0.001f), message);
        }
    }
}
#endif
