using System;
using Core.Runtime.Inputs;

namespace Hotfix.KagitKart
{
    /// 纸艺卡丁车独立偏好；公共画质与辅助偏好在 Hub 统一维护。
    [Serializable]
    public sealed class KagitKartSettings
    {
        public float MusicVolume = 0.7f;
        public float SfxVolume = 0.9f;
        public bool Muted;
        public string ReducedMotion = "auto";
        public bool CameraShake = true;
        public string TouchControls = "auto";
        public bool AutoAccelerate;
        public bool ShowFps;
        public bool SeenTutorial;

        /// <summary>解析系统偏好与玩家覆盖，自动模式使用调用方传入的真实系统值。</summary>
        /// <param name="systemReducedMotion">Core 读取的系统减少动态偏好。</param>
        /// <returns>当前是否减少动态。</returns>
        public bool ResolveReducedMotion(bool systemReducedMotion) => ReducedMotion == "on" || ReducedMotion == "auto" && systemReducedMotion;

        /// <summary>自动模式跟随公共输入设备，手动打开不伪造实际设备。</summary>
        /// <param name="device">Core 最近实际操作设备。</param>
        /// <returns>是否显示触控区。</returns>
        public bool ResolveTouch(InputDeviceKind device) => TouchControls == "on" || TouchControls == "auto" && device == InputDeviceKind.Touch;

        /// 当前保存结构是否有效；损坏偏好由公共存储回退默认，不自动覆盖原值。
        public bool IsValid() => VolumeValid(MusicVolume) && VolumeValid(SfxVolume) && ChoiceValid(ReducedMotion) && ChoiceValid(TouchControls);

        /// <summary>生成独立保存候选，保存失败时原偏好不变。</summary>
        /// <param name="key">本页固定偏好名，不接受未知字段。</param>
        /// <param name="value">音量为 0–10；布尔为 0/1；三态为 0 自动、1 开、2 关。</param>
        /// <returns>已校验的独立候选。</returns>
        public KagitKartSettings WithValue(string key, int value)
        {
            var next = (KagitKartSettings)MemberwiseClone();
            switch (key)
            {
                case "MusicVolume":
                case "SfxVolume":
                    if (value < 0 || value > 10)
                    {
                        throw new ArgumentOutOfRangeException(nameof(value));
                    }
                    if (key == "MusicVolume")
                    {
                        next.MusicVolume = value / 10f;
                    }
                    else
                    {
                        next.SfxVolume = value / 10f;
                    }
                    break;
                case "ReducedMotion":
                case "TouchControls":
                    if (value < 0 || value > 2)
                    {
                        throw new ArgumentOutOfRangeException(nameof(value));
                    }
                    string choice = value == 0 ? "auto" : value == 1 ? "on" : "off";
                    if (key == "ReducedMotion")
                    {
                        next.ReducedMotion = choice;
                    }
                    else
                    {
                        next.TouchControls = choice;
                    }
                    break;
                case "Muted":
                case "CameraShake":
                case "AutoAccelerate":
                case "ShowFps":
                case "SeenTutorial":
                    if (value != 0 && value != 1)
                    {
                        throw new ArgumentOutOfRangeException(nameof(value));
                    }
                    switch (key)
                    {
                        case "Muted": next.Muted = value == 1; break;
                        case "CameraShake": next.CameraShake = value == 1; break;
                        case "AutoAccelerate": next.AutoAccelerate = value == 1; break;
                        case "ShowFps": next.ShowFps = value == 1; break;
                        case "SeenTutorial": next.SeenTutorial = value == 1; break;
                    }
                    break;
                default:
                    throw new ArgumentException("未知赛车设置。", nameof(key));
            }
            return next;
        }

        private static bool VolumeValid(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0 && value <= 1;

        private static bool ChoiceValid(string value) => value == "auto" || value == "on" || value == "off";
    }
}
