using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

/*
 * DroneFlight 自动化测试统一可读性入口。
 * 这里集中维护各测试组的中文用途，并在 Unity Test Runner 执行具体用例时向 Console 输出开始、结束和结果。
 */
namespace Tests.Demo
{
    internal static class DroneFlightTestDiagnostics
    {
        private static readonly IReadOnlyDictionary<string, string> SuiteDescriptions =
            new Dictionary<string, string>
            {
                ["DroneFlightSceneNavigationTests"] = "验证 DroneFlight 场景收集、启动入口和主相机契约",
                ["DroneFlightControlV2Tests"] = "验证轨迹、姿态和控制分配等飞控数学",
                ["DroneControlInputTests"] = "验证飞行输入归一化与非法值保护",
                ["DroneAttitudeMathTests"] = "验证姿态误差、航向和机体系速度数学",
                ["DroneHudFormatterTests"] = "验证 HUD、快捷键提示和飞控告警",
                ["DroneFlightUiContractTests"] = "验证正式 UI 地址、层级和布局契约",
                ["DroneLandingGearStateMachineTests"] = "验证起落架状态机和手动切换",
                ["DroneMotorModelTests"] = "验证电机一阶响应、推力和运行时调参",
                ["DroneEquipmentConfigurationTests"] = "验证抓斗、渔叉配置及装备编辑器契约",
                ["DroneConfigInspectorTests"] = "验证三套配置 Inspector 的双语状态、字段覆盖和诊断",
                ["DroneFlightPortabilityBoundaryTests"] = "验证 DroneFlight 可迁移核心与宿主适配边界",
                ["DroneDebugPresentationTests"] = "验证 F2/F3 独立路由与动力矢量显示平滑",
                ["DroneTelemetryBufferTests"] = "验证遥测环形缓冲和摘要统计",
                ["DroneRotorVisualTests"] = "验证旋翼视觉转速、方向和停止行为",
                ["DroneResponseProfileTests"] = "验证 Cine、Normal、Sport 响应档位",
                ["DroneResetHoldTrackerTests"] = "验证短按和长按重载输入判定",
                ["DroneRemoteControlSequenceTests"] = "验证遥控体验 Waiting 与 Active 状态切换",
                ["DronePrototypeContractTests"] = "验证正式模型、Prefab、材质、旋翼轴和装备 Variant 契约",
                ["DronePidControllerTests"] = "验证 PID、滤波、限幅和抗积分饱和",
                ["DronePayloadTuningCalculatorTests"] = "验证载荷、悬停前馈和动力储备计算",
                ["DroneRotorPhysicsTests"] = "验证可视化夹具、正式无人机起飞及四旋翼飞控物理",
                ["DroneEquipmentPhysicsPlayModeTests"] = "验证抓斗接触、渔叉冲量和绳索受力",
                ["DroneCameraLifecycleTests"] = "验证第三人称、机腹和云台镜头生命周期"
            };

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

                var fullName = test.FullName ?? string.Empty;
                foreach (var entry in SuiteDescriptions)
                {
                    var marker = $".{entry.Key}.";
                    if (fullName.IndexOf(marker, StringComparison.Ordinal) < 0)
                    {
                        continue;
                    }

                    suiteName = entry.Key;
                    description = entry.Value;
                    return true;
                }

                return false;
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
