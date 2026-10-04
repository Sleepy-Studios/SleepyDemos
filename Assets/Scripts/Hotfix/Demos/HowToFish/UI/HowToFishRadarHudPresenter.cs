using TMPro;
using UnityEngine;

namespace Hotfix.HowToFish
{
    /// HUD 独立区域，只维护已保存的本区域显示。
    public sealed class HowToFishRadarHudPresenter : MonoBehaviour
    {
        [SerializeField] private TextMeshProUGUI radarStatus;
        [SerializeField] private TextMeshProUGUI[] radarIslands;
        /// <summary>刷新当前世界的区域显示。</summary>
        /// <param name="world">当前 HUD 已绑定的世界。</param>
        public void Refresh(HowToFishWorld world)
        {
            bool radarVisible = world.HasSession && !world.IsPaused && world.Player.Equipment?.Kind == HowToFishItemKind.Radar;
            gameObject.SetActive(radarVisible);
            if (radarVisible)
            {
                foreach (var dot in radarIslands) dot.gameObject.SetActive(false);
                radarStatus.text = "雷达 · 朝向 " + world.Player.transform.eulerAngles.y.ToString("0") + "°";
                foreach (var island in world.Islands)
                {
                    if (island.Index > world.Session.State.unlockedIsland || island.Index >= radarIslands.Length) continue;
                    var offset = island.Position - world.Player.transform.position;
                    var local = Quaternion.Euler(0, -world.Player.transform.eulerAngles.y, 0) * offset;
                    var dot = radarIslands[island.Index];
                    dot.gameObject.SetActive(true);
                    dot.rectTransform.anchoredPosition = Vector2.ClampMagnitude(new Vector2(local.x, local.z) * .055f, 70);
                    dot.text = "● " + island.DisplayName;
                    if (island.Index == world.Session.State.unlockedIsland)
                        radarStatus.text += $"\n{island.DisplayName} {new Vector2(offset.x, offset.z).magnitude:0}m";
                }
            }
        }
    }
}
