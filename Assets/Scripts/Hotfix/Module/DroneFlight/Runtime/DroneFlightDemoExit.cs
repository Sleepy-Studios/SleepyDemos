using UnityEngine;
using UnityEngine.InputSystem;
using Cysharp.Threading.Tasks;
using Hotfix.SceneManagement;

namespace Hotfix.DroneFlight
{
    /// <summary>在 DroneFlight 场景中按 Backspace 返回 Hub。</summary>
    public sealed class DroneFlightDemoExit : MonoBehaviour
    {
        private bool isLoading;

        private void Update()
        {
            if (isLoading || Keyboard.current == null || !Keyboard.current.backspaceKey.wasPressedThisFrame)
            {
                return;
            }

            isLoading = true;
            ReturnToHubAsync().Forget();
        }

        private async UniTaskVoid ReturnToHubAsync()
        {
            var navigator = GameSceneNavigator.Instance;
            if (navigator == null)
            {
                isLoading = false;
                Debug.LogError("[DroneFlight] 全局场景导航尚未初始化。", this);
                return;
            }

            var result = await navigator.SwitchAsync(GameSceneId.Hub);
            if (result.Status == GameSceneSwitchStatus.Failed)
            {
                isLoading = false;
                Debug.LogError($"[DroneFlight] 无法返回主界面：{result.Error}", this);
            }
            else if (result.Status == GameSceneSwitchStatus.Busy)
            {
                isLoading = false;
            }
        }
    }
}
