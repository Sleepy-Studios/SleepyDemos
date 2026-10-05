using System;
using System.Collections.Generic;
using Core.Runtime;
using UnityEngine;

namespace Hotfix
{
    /// 全局本机用户与硬件状态。
    public class UserData : IData
    {
        public List<IHandler> Handlers { get; } = new List<IHandler>
        {
            new UserHandler()
        };

        /// 本机硬件采样信息；清理后为 null。
        public HardwareProfile Hardware { get; private set; }

        /// 初始化本机信息并返回当前 Data。
        public UserData InitData()
        {
            RefreshHardwareProfile();
            return this;
        }

        /// 更新本机硬件采样。
        public void RefreshHardwareProfile()
        {
            Hardware = HardwareProfile.Capture();
        }

        /// 重置业务状态，使上一轮请求结果失效。
        public void ClearData()
        {
            Hardware = null;
        }

        /// 取得调试用硬件摘要。
        public string GetHardwareSummary()
        {
            return Hardware == null ? "HardwareProfile: empty" : Hardware.ToSummary();
        }

        public sealed class HardwareProfile
        {
            public string DeviceName { get; private set; }

            public string DeviceModel { get; private set; }

            public string DeviceType { get; private set; }

            public string OperatingSystem { get; private set; }

            public string ProcessorType { get; private set; }

            public int ProcessorCount { get; private set; }

            public int SystemMemorySizeMb { get; private set; }

            public string GraphicsDeviceName { get; private set; }

            public string GraphicsDeviceType { get; private set; }

            public int GraphicsMemorySizeMb { get; private set; }

            public DateTime CapturedAt { get; private set; }

            /// 采集当前平台的硬件参数。
            public static HardwareProfile Capture()
            {
                return new HardwareProfile
                {
                    DeviceName = SystemInfo.deviceName,
                    DeviceModel = SystemInfo.deviceModel,
                    DeviceType = SystemInfo.deviceType.ToString(),
                    OperatingSystem = SystemInfo.operatingSystem,
                    ProcessorType = SystemInfo.processorType,
                    ProcessorCount = SystemInfo.processorCount,
                    SystemMemorySizeMb = SystemInfo.systemMemorySize,
                    GraphicsDeviceName = SystemInfo.graphicsDeviceName,
                    GraphicsDeviceType = SystemInfo.graphicsDeviceType.ToString(),
                    GraphicsMemorySizeMb = SystemInfo.graphicsMemorySize,
                    CapturedAt = TimeUtil.LocalNow
                };
            }

            /// 取得本次采样的调试摘要。
            public string ToSummary()
            {
                return $"HardwareProfile: DeviceName={DeviceName}, DeviceModel={DeviceModel}, DeviceType={DeviceType}, OS={OperatingSystem}, CPU={ProcessorType}, CPUCores={ProcessorCount}, RAM={SystemMemorySizeMb}MB, GPU={GraphicsDeviceName}, GPUType={GraphicsDeviceType}, GPUMemory={GraphicsMemorySizeMb}MB, CapturedAt={CapturedAt:yyyy-MM-dd HH:mm:ss}";
            }
        }
    }
}
