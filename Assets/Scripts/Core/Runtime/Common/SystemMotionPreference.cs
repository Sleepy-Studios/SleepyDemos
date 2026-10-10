#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
using System.Runtime.InteropServices;
#elif UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine;
#endif

namespace Core.Runtime
{
    /// 按平台读取系统减少动态偏好；不缓存、不订阅系统变化，也不保存游戏设置。
    public static class SystemMotionPreference
    {
        /// Unity 主线程读取 Windows/Android 系统值；失败或未支持的平台返回 false。
        public static bool ReadReducedMotion()
        {
#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
            return SystemParametersInfoW(0x1042, 0, out int animationEnabled, 0) && animationEnabled == 0;
#elif UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var resolver = activity.Call<AndroidJavaObject>("getContentResolver"))
                using (var settings = new AndroidJavaClass("android.provider.Settings$Global"))
                {
                    // Chromium 的 reduce 只对应尺度为零；0.5 的加速动画仍是 no-preference。
                    return settings.CallStatic<float>("getFloat", resolver, "animator_duration_scale", 1f) == 0f;
                }
            }
            catch (AndroidJavaException)
            {
                return false;
            }
#else
            return false;
#endif
        }

#if UNITY_EDITOR_WIN || (UNITY_STANDALONE_WIN && !UNITY_EDITOR)
        // Win32 BOOL 固定四字节；成功状态与接收到的动画开关分开处理。
        [DllImport("user32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SystemParametersInfoW(uint action, uint parameter, out int value, uint flags);
#endif
    }
}
