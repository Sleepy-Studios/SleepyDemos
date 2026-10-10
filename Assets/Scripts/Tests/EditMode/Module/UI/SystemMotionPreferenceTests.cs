#if UNITY_EDITOR_WIN
using System.Runtime.InteropServices;
#endif
using Core.Runtime;
using NUnit.Framework;

namespace Tests.Module
{
    public sealed class SystemMotionPreferenceTests
    {
        [Test]
        public void ReadPreferenceMatchesRealWindowsNativeAnimationFlag()
        {
#if UNITY_EDITOR_WIN
            Assert.That(ReadClientAreaAnimation(0x1042, 0, out bool enabled, 0), Is.True,
                "Windows 原生偏好查询失败，不能把本测试报告为真实系统验证通过。");
            Assert.That(SystemMotionPreference.ReadReducedMotion(), Is.EqualTo(!enabled));
#else
            Assert.That(SystemMotionPreference.ReadReducedMotion(), Is.False);
#endif
        }

#if UNITY_EDITOR_WIN
        [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ReadClientAreaAnimation(uint action, uint parameter, [MarshalAs(UnmanagedType.Bool)] out bool value, uint flags);
#endif
    }
}
