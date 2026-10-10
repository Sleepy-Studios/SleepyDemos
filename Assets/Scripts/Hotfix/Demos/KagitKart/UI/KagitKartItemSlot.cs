using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix.KagitKart
{
    /// 原作道具窗口、九格轮盘与拿到道具时的纸卡弹动。
    public sealed class KagitKartItemSlot : MonoBehaviour
    {
        private static readonly string[] ItemIds = { "rocket", "rocket3", "gum", "plane", "homing", "ink", "foil", "scissors" };
        [SerializeField] private Sprite[] icons;
        [SerializeField] private Image icon;
        [SerializeField] private Image window;
        [SerializeField] private RectTransform reel;
        [SerializeField] private RectTransform heldBorder;
        [SerializeField] private TMP_Text itemName;
        [SerializeField] private TMP_Text count;
        private KagitKartVehicle player;
        private KagitKartRandom random;
        private bool spinning;
        private double reelPosition;
        private float thudAge = 1;
        private float borderAge;
        private string shownItem;
        private bool wasHeld;
        private bool thudClass;
        private bool lastReduced;
        private CanvasGroup nameGroup;
        private RectTransform nameCard;
        private bool nameTarget;
        private float nameFrom, nameAge;
        private float layoutScale = 1;
        private Vector2 layoutScreen;
        private float layoutCanvasScale = -1;
        private bool layoutTouch;

        /// <summary>绑定本场玩家，不持有 Loader；空值清理本场表现。</summary>
        /// <param name="value">当前玩家。</param>
        public void Bind(KagitKartVehicle value)
        {
            player = value;
            random = new KagitKartRandom(unchecked((uint)Environment.TickCount));
            spinning = false;
            shownItem = null;
            thudAge = 1;
            borderAge = 0;
            wasHeld = false;
            thudClass = lastReduced = nameTarget = false;
            nameFrom = 0;
            nameAge = 1;
            nameCard = (RectTransform)itemName.transform.parent;
            nameGroup = nameCard.GetComponent<CanvasGroup>();
            nameGroup.alpha = 0;
            icon.enabled = false;
            reel.gameObject.SetActive(false);
            heldBorder.gameObject.SetActive(false);
            itemName.transform.parent.gameObject.SetActive(false);
            count.transform.parent.gameObject.SetActive(false);
            transform.localScale = Vector3.one * layoutScale;
            transform.localRotation = Quaternion.Euler(0, 0, -2);
        }

        /// <summary>沿用玩家库存和原作抽取时钟，暂停不会推进轮盘。</summary>
        /// <param name="dt">暂停感知秒数。</param>
        /// <param name="reducedMotion">按源选择器省略纸卡弹动，持有边框仍继续CSS动画。</param>
        public void Advance(float dt, bool reducedMotion)
        {
            if (player == null) return;
            if (lastReduced && !reducedMotion && thudClass)
            {
                thudAge = 0;
            }
            lastReduced = reducedMotion;
            if (player.Roulette > 0)
            {
                if (!spinning)
                {
                    spinning = true;
                    reelPosition = random.Next() * ItemIds.Length;
                    reel.gameObject.SetActive(true);
                    icon.enabled = false;
                    FadeName(false);
                    thudAge = 1;
                    thudClass = false;
                }
                reelPosition += KagitKartHudMotion.ReelStep(player.Roulette, player.RouletteDuration, dt);
                double percent = Math.Round(reelPosition % ItemIds.Length * 100 / 9, 3, MidpointRounding.AwayFromZero);
                reel.anchoredPosition = Vector2.up * ((float)percent * 9 * window.rectTransform.rect.height / 100);
            }
            else
            {
                if (spinning)
                {
                    spinning = false;
                    reel.gameObject.SetActive(false);
                    ShowItem(player.Item);
                    thudClass = !reducedMotion;
                    thudAge = reducedMotion ? 1 : 0;
                }
                else if (shownItem != player.Item) ShowItem(player.Item);
            }
            count.transform.parent.gameObject.SetActive(player.ItemCount > 1);
            count.text = "×" + player.ItemCount;
            if (player.ItemHeld && !wasHeld) borderAge = 0;
            wasHeld = player.ItemHeld;
            heldBorder.gameObject.SetActive(wasHeld);
            window.color = player.ItemHeld ? new Color(242f / 255, 193f / 255, 78f / 255, 0.35f)
                : new Color(45f / 255, 42f / 255, 50f / 255, 0.2f);
            borderAge += Time.unscaledDeltaTime;
            float march = borderAge % 1;
            heldBorder.localRotation = Quaternion.Euler(0, 0, -2 * march);
            heldBorder.localScale = Vector3.one * (1 + 0.02f * march);
            thudAge += Time.unscaledDeltaTime;
            float scale = 1, angle = -2;
            if (!reducedMotion && thudAge < 0.34f)
            {
                float t = thudAge / 0.34f;
                float k = KagitKartHudMotion.Ease(t < 0.6f ? t / 0.6f : (t - 0.6f) / 0.4f, new Vector4(0.2f, 1.8f, 0.4f, 1));
                scale = t < 0.6f ? Mathf.LerpUnclamped(1.22f, 0.95f, k) : Mathf.LerpUnclamped(0.95f, 1, k);
                angle = t < 0.6f ? Mathf.LerpUnclamped(-2, -4, k) : Mathf.LerpUnclamped(-4, -2, k);
            }
            transform.localScale = Vector3.one * (layoutScale * scale);
            transform.localRotation = Quaternion.Euler(0, 0, angle);
            nameAge += Time.unscaledDeltaTime;
            float fade = KagitKartHudMotion.Ease(nameAge / 0.2f, new Vector4(0.25f, 0.1f, 0.25f, 1));
            nameGroup.alpha = Mathf.Lerp(nameFrom, nameTarget ? 1 : 0, fade);
            if (!nameTarget && nameAge >= 0.2f)
            {
                nameCard.gameObject.SetActive(false);
            }
        }

        private void ShowItem(string id)
        {
            shownItem = id;
            int index = Array.IndexOf(ItemIds, id);
            icon.enabled = index >= 0;
            if (index >= 0) icon.sprite = icons[index];
            itemName.text = index >= 0 ? KagitKartCatalog.ItemName(id) : "";
            float width = itemName.GetPreferredValues(itemName.text).x;
            nameCard.sizeDelta = new Vector2(width + 17.6f, 24.8f);
            itemName.rectTransform.sizeDelta = new Vector2(width, 19.2f);
            FadeName(index >= 0);
        }

        private void FadeName(bool visible)
        {
            if (nameTarget == visible)
            {
                return;
            }
            nameTarget = visible;
            nameFrom = nameGroup.alpha;
            nameAge = 0;
            nameCard.gameObject.SetActive(true);
        }

        /// <summary>按原CSS配置槽尺寸和安全区内左上位置；触屏暂停时恢复普通race布局。</summary>
        /// <param name="touchRace">原touchRace：race视图、触控且未暂停，对应View的showTouch。</param>
        public void SetLayout(bool touchRace)
        {
            var canvas = GetComponentInParent<Canvas>();
            Vector2 screen = new Vector2(Screen.width, Screen.height);
            if (screen == layoutScreen && canvas.scaleFactor == layoutCanvasScale && touchRace == layoutTouch)
            {
                return;
            }
            layoutScreen = screen;
            layoutCanvasScale = canvas.scaleFactor;
            layoutTouch = touchRace;
            float em = Mathf.Clamp(Mathf.Min(Screen.width * 0.0104f, Screen.height * 0.0195f), 11, 20);
            layoutScale = em / (20 * canvas.scaleFactor);
            bool small = Screen.height <= 520;
            float size = touchRace ? 136 : small ? 120 : 148;
            float inset = small ? 17 : 21;
            float windowSize = size - 2 * inset;
            var root = (RectTransform)transform;
            root.sizeDelta = Vector2.one * size;
            root.anchorMin = root.anchorMax = new Vector2(0, 1);
            root.anchoredPosition = new Vector2((touchRace ? 20 : 32) + size / 2, -((touchRace ? 20 : 28) + size / 2)) * layoutScale;
            var card = (RectTransform)window.transform.parent;
            card.sizeDelta = Vector2.one * size;
            // 原slot直接包含paper，没有kk-sheet的硬阴影。
            transform.Find("ItemCardShadow").gameObject.SetActive(false);
            window.rectTransform.sizeDelta = Vector2.one * windowSize;
            icon.rectTransform.sizeDelta = Vector2.one * (windowSize - 20);
            reel.sizeDelta = new Vector2(windowSize, windowSize * 9);
            for (int i = 0; i < 9; i++)
            {
                var row = (RectTransform)reel.GetChild(i);
                row.anchoredPosition = new Vector2(0, -windowSize * (i + 0.5f));
                row.sizeDelta = Vector2.one * (windowSize - 20);
            }
            heldBorder.sizeDelta = Vector2.one * (size - 24);
            nameCard.anchoredPosition = new Vector2(0, -size / 2 - 20.4f);
            var countCard = (RectTransform)count.transform.parent;
            countCard.anchoredPosition = new Vector2(size / 2 - 8.4f, -size / 2 + 12.6f);
            nameCard.GetComponent<Image>().raycastTarget = touchRace;
            countCard.GetComponent<Image>().raycastTarget = touchRace;
            SetInset("InsetTop", new Vector2(0, windowSize / 2 - 3.2f), new Vector2(windowSize, 6.4f));
            SetInset("InsetLeft", new Vector2(-windowSize / 2 + 3.2f, -3.2f), new Vector2(6.4f, windowSize - 6.4f));
            SetInset("InsetBottom", new Vector2(0, -windowSize / 2 + 0.8f), new Vector2(windowSize, 1.6f));
            SetInset("InsetRight", new Vector2(windowSize / 2 - 0.8f, 0), new Vector2(1.6f, windowSize));
        }

        private void SetInset(string node, Vector2 position, Vector2 size)
        {
            var rect = (RectTransform)window.transform.Find(node);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        /// <summary>为预警图标复用本窗口已保存的原作图标。</summary>
        /// <param name="id">原作道具标识。</param>
        public Sprite Icon(string id)
        {
            int index = Array.IndexOf(ItemIds, id);
            return index < 0 ? null : icons[index];
        }
    }
}
