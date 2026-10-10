using System.Threading;
using Cysharp.Threading.Tasks;
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
        [SerializeField] internal float duration = 0.62f;
        private Vector3[] positions;
        private Vector3[] scales;
        private float[] tilts;
        private CanvasGroup[] groups;
        private Button[] buttons;
        private Transform[] ribbons;
        private float elapsed;
        private float extraDelay;
        private bool reducedMotion;
        private bool folding;

        internal bool FoldOnExit { get; set; } = true;

        /// <summary>从本页保存姿态播放原作纸卡弹起，模式页等待封面落下。</summary>
        /// <param name="delay">封面打开时为 0.8 秒，其余页面为 0。</param>
        /// <param name="reduceMotion">开启时直接显示纸卡终态，仍显示焦点和书签。</param>
        public void Play(float delay, bool reduceMotion = false)
        {
            folding = false;
            reducedMotion = reduceMotion;
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
            if (positions == null || folding)
            {
                return;
            }
            elapsed += Time.unscaledDeltaTime;
            Apply();
        }

        /// <summary>更新本页低动态偏好；退出折卡读取当前值，页面入口不重播。</summary>
        /// <param name="value">本 Demo 当前实际低动态策略。</param>
        public void SetReducedMotion(bool value)
        {
            reducedMotion = value;
        }

        internal async UniTask FoldAsync(CancellationToken cancellationToken)
        {
            if (!FoldOnExit || positions == null || reducedMotion)
            {
                return;
            }
            folding = true;
            float seconds = 0;
            ApplyFold(0);
            while (seconds < 0.16f)
            {
                await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken);
                seconds += Time.unscaledDeltaTime;
                ApplyFold(Mathf.Clamp01(seconds / 0.16f));
            }
            cancellationToken.ThrowIfCancellationRequested();
        }

        internal void CompleteFold()
        {
            if (positions == null || !FoldOnExit || reducedMotion)
            {
                return;
            }
            folding = true;
            ApplyFold(1);
        }

        internal void RestoreAfterFold()
        {
            if (!folding)
            {
                return;
            }
            folding = false;
            elapsed = extraDelay + duration;
            foreach (float delay in delays)
            {
                elapsed = Mathf.Max(elapsed, extraDelay + delay + duration);
            }
            Apply();
        }

        private void ApplyFold(float t)
        {
            // CSS ease-in = cubic-bezier(.42,0,1,1)，同时从纸卡底边折至 -92 度，不额外淡出。
            float eased = t <= 0 ? 0 : t >= 1 ? 1 : KagitKartHudMotion.Ease(t, new Vector4(0.42f, 0, 1, 1));
            float angle = -92 * eased;
            for (int i = 0; i < cards.Length; i++)
            {
                cards[i].localRotation = Quaternion.Euler(angle, 0, tilts[i]);
                cards[i].localPosition = positions[i];
                cards[i].localScale = scales[i];
                groups[i].alpha = 1;
                if (ribbons[i] != null)
                {
                    ribbons[i].localScale = new Vector3(1, 0, 1);
                }
            }
        }

        private void Apply()
        {
            for (int i = 0; i < cards.Length; i++)
            {
                float t = reducedMotion ? 1 : Mathf.Clamp01((elapsed - extraDelay - delays[i]) / duration);
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
        private static float Ease(float x) => x <= 0 || x >= 1 ? Mathf.Clamp01(x)
            : KagitKartHudMotion.Ease(x, new Vector4(0.25f, 0.8f, 0.3f, 1));
    }
}
