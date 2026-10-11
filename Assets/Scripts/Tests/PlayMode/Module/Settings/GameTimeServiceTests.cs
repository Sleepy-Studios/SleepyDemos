using System;
using System.Collections;
using Core.Runtime.TimeControls;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Module
{
    public sealed class GameTimeServiceTests
    {
        [UnityTest]
        public IEnumerator PresetCurveIsCopiedAndRampFinishesAtExactTarget()
        {
            float originalScale = Time.timeScale;
            float originalStep = Time.fixedDeltaTime;
            var preset = ScriptableObject.CreateInstance<GameTimePreset>();
            var curve = AnimationCurve.Linear(0, 0, 1, 1);
            preset.Configure(0.75f, 0.03f, curve);
            using var scope = GameTimeService.Push(0.5f);
            try
            {
                curve.keys = new[] { new Keyframe(0, 1), new Keyframe(1, 1) };
                Assert.That(preset.Curve.Evaluate(0), Is.Zero);
                var exposed = preset.Curve;
                exposed.keys = curve.keys;
                Assert.That(preset.Curve.Evaluate(0), Is.Zero);
                yield return scope.RampSpeedAsync(preset).ToCoroutine();
                Assert.That(scope.Speed, Is.EqualTo(0.75f));
                Assert.That(preset.Curve.Evaluate(0), Is.Zero);
                Assert.That(preset.TargetSpeed, Is.EqualTo(0.75f));
                Assert.That(Time.fixedDeltaTime, Is.EqualTo(originalStep).Within(0.0000001f));
            }
            finally
            {
                scope.Dispose();
                UnityEngine.Object.Destroy(preset);
                Time.timeScale = originalScale;
                Time.fixedDeltaTime = originalStep;
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator PersistentFreezeIsNotOverriddenAndLeasesAreSafeAfterOwnerExit()
        {
            float originalScale = Time.timeScale;
            float originalStep = Time.fixedDeltaTime;
            var owner = GameTimeService.Push(0.5f);
            var first = owner.Freeze();
            var second = owner.Freeze();
            var newer = GameTimeService.Push(2);
            try
            {
                Assert.That(GameTimeService.Scale, Is.Zero);
                first.Dispose();
                first.Dispose();
                Assert.That(GameTimeService.Scale, Is.Zero);
                owner.Dispose();
                Assert.That(GameTimeService.Scale, Is.EqualTo(2));
                second.Dispose();
                second.Dispose();
                Assert.That(GameTimeService.Scale, Is.EqualTo(2));
                newer.Dispose();
                Assert.That(Time.timeScale, Is.EqualTo(originalScale));
                Assert.That(Time.fixedDeltaTime, Is.EqualTo(originalStep).Within(0.0000001f));
            }
            finally
            {
                first.Dispose();
                second.Dispose();
                owner.Dispose();
                newer.Dispose();
                Time.timeScale = originalScale;
                Time.fixedDeltaTime = originalStep;
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator NestedScopesFreezeAndRampUseRealTimeAndRestoreOriginalPhysicsStep()
        {
            float originalScale = Time.timeScale;
            float originalStep = Time.fixedDeltaTime;
            GameTimeService.Scope older = null;
            GameTimeService.Scope newer = null;
            try
            {
                older = GameTimeService.Push(0.5f);
                newer = GameTimeService.Push(2);
                Assert.That(GameTimeService.Scale, Is.EqualTo(2));
                older.SetSpeed(0.25f);
                Assert.That(GameTimeService.Scale, Is.EqualTo(2), "旧作用域不能抢占新演出。");
                var freeze = older.FreezeAsync(0.03f);
                Assert.That(GameTimeService.Scale, Is.Zero);
                Assert.That(GameTimeService.DeltaTime, Is.Zero);
                Assert.That(Time.fixedDeltaTime, Is.EqualTo(originalStep).Within(0.0000001f));
                yield return freeze.ToCoroutine();
                Assert.That(GameTimeService.Scale, Is.EqualTo(2));
                yield return newer.RampSpeedAsync(0.75f, 0.03f).ToCoroutine();
                Assert.That(GameTimeService.Scale, Is.EqualTo(0.75f).Within(0.0001f));
                newer.Dispose();
                newer = null;
                Assert.That(GameTimeService.Scale, Is.EqualTo(0.25f));
                older.Dispose();
                older = null;
                Assert.That(Time.timeScale, Is.EqualTo(originalScale));
                Assert.That(Time.fixedDeltaTime, Is.EqualTo(originalStep).Within(0.0000001f));
                Assert.Throws<ArgumentOutOfRangeException>(() => GameTimeService.Push(float.NaN));
            }
            finally
            {
                newer?.Dispose();
                older?.Dispose();
                Time.timeScale = originalScale;
                Time.fixedDeltaTime = originalStep;
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator DisposingFrozenScopeCancelsOperationAndDoesNotChangeNewOwnerOnDriverDestroy()
        {
            float originalScale = Time.timeScale;
            float originalStep = Time.fixedDeltaTime;
            GameTimeService.Scope first = null;
            GameTimeService.Scope replacement = null;
            try
            {
                first = GameTimeService.Push(0.5f);
                // 本项目 UniTask 的 AsTask 会将取消转换为 fault，直接验收原生取消契约。
                var freeze = first.FreezeAsync(10).Preserve();
                Assert.That(Time.timeScale, Is.Zero);
                first.Dispose();
                first = null;
                replacement = GameTimeService.Push(1.5f);
                yield return null;
                yield return null;
                Assert.That(freeze.Status, Is.EqualTo(UniTaskStatus.Canceled));
                Assert.Throws<OperationCanceledException>(() => freeze.GetAwaiter().GetResult());
                Assert.That(Time.timeScale, Is.EqualTo(1.5f));
                replacement.Dispose();
                replacement = null;
                Assert.That(Time.timeScale, Is.EqualTo(originalScale));
                // Unity 原生属性写回有数个 ULP 的量化，仍必须拒绝实际步长缩放。
                Assert.That(Time.fixedDeltaTime, Is.EqualTo(originalStep).Within(0.0000001f));
            }
            finally
            {
                first?.Dispose();
                replacement?.Dispose();
                Time.timeScale = originalScale;
                Time.fixedDeltaTime = originalStep;
            }
            yield return null;
        }
    }
}
