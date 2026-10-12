using System;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/*
 * DroneFlight 自动化测试统一可读性入口。
 * 依据测试类型命名识别无人机用例，并在 Unity Test Runner 执行时输出开始、结束和结果。
 */
namespace Tests.Demo
{
    internal static class DroneFlightTestDiagnostics
    {
        [InitializeOnLoadMethod]
        private static void RegisterCallbacks()
        {
            TestRunnerApi.RegisterTestCallback(new ConsoleReporter());
        }

        private sealed class ConsoleReporter : ICallbacks
        {
            public void RunStarted(ITestAdaptor testsToRun)
            {
            }

            public void RunFinished(ITestResultAdaptor result)
            {
            }

            public void TestStarted(ITestAdaptor test)
            {
                if (!TryGetDescription(test, out var suiteName, out var description))
                {
                    return;
                }

                Debug.Log($"[DroneFlight测试][开始] {description} | {suiteName}.{test.Name}");
            }

            public void TestFinished(ITestResultAdaptor result)
            {
                if (!TryGetDescription(result.Test, out var suiteName, out var description))
                {
                    return;
                }

                Debug.Log(
                    $"[DroneFlight测试][结束] {description} | {suiteName}.{result.Name} | "
                    + $"结果：{TranslateStatus(result.TestStatus)} | 耗时：{result.Duration:F3}s");
            }

            private static bool TryGetDescription(
                ITestAdaptor test,
                out string suiteName,
                out string description)
            {
                suiteName = string.Empty;
                description = string.Empty;
                if (test == null || test.IsSuite)
                {
                    return false;
                }

                var fixture = test;
                while (fixture != null && fixture.TypeInfo == null)
                {
                    fixture = fixture.Parent;
                }

                var type = fixture?.TypeInfo?.Type;
                if (type?.Namespace != "Tests.Demo"
                    || !(type.Name.StartsWith("Drone", StringComparison.Ordinal)
                         || type.Name.StartsWith("Quadrotor", StringComparison.Ordinal)))
                {
                    return false;
                }

                suiteName = type.Name;
                description = string.IsNullOrEmpty(test.Description)
                    ? "无人机回归"
                    : test.Description;
                return true;
            }

            private static string TranslateStatus(TestStatus status)
            {
                return status switch
                {
                    TestStatus.Passed => "通过",
                    TestStatus.Failed => "失败",
                    TestStatus.Skipped => "跳过",
                    TestStatus.Inconclusive => "未判定",
                    _ => status.ToString()
                };
            }
        }
    }
}
