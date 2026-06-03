using System;
using System.Collections.Generic;
using Core.Runtime;
using UnityEngine;

namespace Hotfix
{
    public class UserData : IData
    {
        public List<IHandler> Handlers { get; } = new List<IHandler>
        {
            new UserHandler()
        };

        public HardwareProfile Hardware { get; private set; }

        public UserData InitData()
        {
            RefreshHardwareProfile();
            return this;
        }

        public void RefreshHardwareProfile()
        {
            Hardware = HardwareProfile.Capture();
        }

        public void ClearData()
        {
            Hardware = null;
        }

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
                    CapturedAt = DateTime.Now
                };
            }

            public string ToSummary()
            {
                return $"HardwareProfile: DeviceName={DeviceName}, DeviceModel={DeviceModel}, DeviceType={DeviceType}, OS={OperatingSystem}, CPU={ProcessorType}, CPUCores={ProcessorCount}, RAM={SystemMemorySizeMb}MB, GPU={GraphicsDeviceName}, GPUType={GraphicsDeviceType}, GPUMemory={GraphicsMemorySizeMb}MB, CapturedAt={CapturedAt:yyyy-MM-dd HH:mm:ss}";
            }
        }
    }
}
