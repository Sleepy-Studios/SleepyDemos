using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Hotfix.KagitKart
{
    /// 原作菜单纸卡沿底边弹起、焦点抬起与书签表现，不承担输入命令。
    public sealed class KagitKartMenuPaperMotion : MonoBehaviour
    {
        [SerializeField] internal RectTransform[] cards;
        [SerializeField] internal float[] delays;
        private Vector3[] positions;
        private Vector3[] scales;
        private float[] tilts;
        private CanvasGroup[] groups;
        private Button[] buttons;
        private Transform[] ribbons;
        private float elapsed;
        private float extraDelay;

        /// <summary>从本页保存姿态播放原作纸卡弹起，模式页等待封面落下。</summary>
        /// <param name="delay">封面打开时为 0.8 秒，其余页面为 0。</param>
        public void Play(float delay)
        {
            if (positions == null)
            {
                positions = new Vector3[cards.Length];
                scales = new Vector3[cards.Length];
                tilts = new float[cards.Length];
                groups = new CanvasGroup[cards.Length];
                buttons = new Button[cards.Length];
                ribbons = new Transform[cards.Length];
                for (int i = 0; i < cards.Length; i++)
                {
                    positions[i] = cards[i].localPosition;
                    scales[i] = cards[i].localScale;
                    tilts[i] = cards[i].localEulerAngles.z;
                    groups[i] = cards[i].GetComponent<CanvasGroup>();
                    if (groups[i] == null)
                    {
                        throw new System.ArgumentException("纸卡资源缺少 CanvasGroup：" + cards[i].name);
                    }
                    buttons[i] = cards[i].GetComponentInChildren<Button>();
                    ribbons[i] = cards[i].Find("Bookmark");
                }
            }
            elapsed = 0;
            extraDelay = delay;
            Apply();
        }

        private void Update()
        {
            if (positions == null)
            {
                return;
            }
            elapsed += Time.unscaledDeltaTime;
            Apply();
        }

        private void Apply()
        {
            for (int i = 0; i < cards.Length; i++)
            {
                float t = Mathf.Clamp01((elapsed - extraDelay - delays[i]) / 0.62f);
                float angle = t < 0.55f ? Mathf.LerpUnclamped(-92, 9, Ease(t / 0.55f))
                    : t < 0.78f ? Mathf.LerpUnclamped(9, -3, Ease((t - 0.55f) / 0.23f))
                    : Mathf.LerpUnclamped(-3, 0, Ease((t - 0.78f) / 0.22f));
                bool focused = buttons[i] != null && EventSystem.current?.currentSelectedGameObject == buttons[i].gameObject;
                cards[i].localRotation = Quaternion.Euler(angle, 0, focused ? 0 : tilts[i]);
                bool chapterTab = cards[i].name.StartsWith("Track", System.StringComparison.Ordinal);
                cards[i].localPosition = positions[i] + (focused ? chapterTab ? Vector3.left * 48 : Vector3.up * 10 : Vector3.zero);
                cards[i].localScale = scales[i] * (focused && !chapterTab ? 1.035f : 1);
                groups[i].alpha = Ease(Mathf.Clamp01(t / 0.1f));
                if (ribbons[i] != null)
                {
                    ribbons[i].localScale = new Vector3(1, focused ? 1 : 0, 1);
                }
            }
        }

        // 对应原作 cubic-bezier(.25,.8,.3,1)，先解横轴再读取纵轴。
        private static float Ease(float x)
        {
            if (x <= 0 || x >= 1)
            {
                return Mathf.Clamp01(x);
            }
            float low = 0, high = 1;
            for (int i = 0; i < 12; i++)
            {
                float t = (low + high) / 2;
                float curve = 3 * (1 - t) * (1 - t) * t * 0.25f + 3 * (1 - t) * t * t * 0.3f + t * t * t;
                if (curve < x)
                {
                    low = t;
                }
                else
                {
                    high = t;
                }
            }
            float u = (low + high) / 2;
            return 3 * (1 - u) * (1 - u) * u * 0.8f + 3 * (1 - u) * u * u + u * u * u;
        }
    }
}
