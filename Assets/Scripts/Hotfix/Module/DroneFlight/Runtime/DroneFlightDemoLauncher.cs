using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using Hotfix.SceneManagement;

namespace Hotfix.DroneFlight
{
    /// <summary>
    /// 主菜单上的独立入口，不修改 MvcBind 自动生成字段。
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class DroneFlightDemoLauncher : MonoBehaviour
    {
        private Button button;

        private void Awake()
        {
            button = GetComponent<Button>();
            button.onClick.AddListener(OpenDemo);
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OpenDemo);
            }
        }

        private void OpenDemo()
        {
            button.interactable = false;
            OpenDemoAsync().Forget();
        }

        private async UniTaskVoid OpenDemoAsync()
        {
            var navigator = GameSceneNavigator.Instance;
            if (navigator == null)
            {
                button.interactable = true;
                Debug.LogError("[DroneFlight] 全局场景导航尚未初始化。", this);
                return;
            }

            var result = await navigator.SwitchAsync(GameSceneId.DroneFlight);
            if (result.Status == GameSceneSwitchStatus.Failed)
            {
                if (button != null)
                {
                    button.interactable = true;
                }

                Debug.LogError($"[DroneFlight] 无法进入 Demo：{result.Error}", this);
            }
            else if (result.Status == GameSceneSwitchStatus.Busy && button != null)
            {
                button.interactable = true;
            }
        }
    }
}
