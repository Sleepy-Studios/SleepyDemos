using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Scripting;

namespace Core.Runtime.Platform.TapTap
{
    /// TapTap H5 的浏览器前后台入口；玩法仍通过原有暂停门闩明确继续。
    public sealed class TapTapPlatform : MonoBehaviour
    {
#if UNITY_WEBGL && SLEEPY_TAPTAP && !UNITY_EDITOR
        private static TapTapPlatform instance;
        private bool hidden;
        private bool audioPausedBeforeHide;
#endif

        /// 在正式启动前注册浏览器生命周期；原生平台和 Editor 无副作用。
        public static void Initialize()
        {
#if UNITY_WEBGL && SLEEPY_TAPTAP && !UNITY_EDITOR
            if (instance != null)
            {
                return;
            }
            var root = new GameObject("SleepyTapTapPlatform");
            DontDestroyOnLoad(root);
            var driver = root.AddComponent<TapTapPlatform>();
            var result = JsonUtility.FromJson<InitializeResult>(SleepyTapTap_Initialize(root.name));
            if (result == null || !result.ok)
            {
                Destroy(root);
                throw new InvalidOperationException(result?.error ?? "TapTap 生命周期初始化失败。");
            }
            instance = driver;
            driver.OnTapLifecycle(result.hidden ? "1" : "0");
#endif
        }

#if UNITY_WEBGL && SLEEPY_TAPTAP && !UNITY_EDITOR
        [Preserve]
        private void OnTapLifecycle(string value)
        {
            bool paused = value == "1";
            if (hidden == paused)
            {
                return;
            }
            hidden = paused;
            if (paused)
            {
                audioPausedBeforeHide = AudioListener.pause;
                AudioListener.pause = true;
            }
            else
            {
                AudioListener.pause = audioPausedBeforeHide;
            }
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                BroadcastPause(SceneManager.GetSceneAt(index), paused);
            }
            // 常驻 UI 的触控与输入组件也必须释放按住状态。
            BroadcastPause(gameObject.scene, paused);
        }

        private static void BroadcastPause(Scene scene, bool paused)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }
            foreach (var root in scene.GetRootGameObjects())
            {
                root.BroadcastMessage("OnApplicationPause", paused, SendMessageOptions.DontRequireReceiver);
                root.BroadcastMessage("OnApplicationFocus", !paused, SendMessageOptions.DontRequireReceiver);
            }
        }

        private void OnDestroy()
        {
            if (instance != this)
            {
                return;
            }
            SleepyTapTap_Shutdown();
            if (hidden)
            {
                AudioListener.pause = audioPausedBeforeHide;
            }
            instance = null;
        }

        [Serializable]
        private sealed class InitializeResult
        {
            public bool ok;
            public bool hidden;
            public string error;
        }

        [DllImport("__Internal")]
        private static extern string SleepyTapTap_Initialize(string receiver);
        [DllImport("__Internal")]
        private static extern void SleepyTapTap_Shutdown();
#endif
    }
}
