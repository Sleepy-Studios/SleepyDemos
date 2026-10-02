using System;
using Hotfix.JinxCasino.Rules;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix.JinxCasino.Adapters.UI
{
    [Serializable]
    public sealed class CasinoItemIconBinding { public string Id; public Sprite Sprite; }

    /// 保存的商店行模板；界面只复用模板，不在运行时构造控件。
    public sealed class JinxCasinoItemCard : MonoBehaviour
    {
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private Button purchaseButton;
        [SerializeField] private Button useButton;
        [SerializeField] private TMP_Text useButtonText;
        [SerializeField] private Image iconImage;
        [SerializeField] private CasinoItemIconBinding[] itemIcons = Array.Empty<CasinoItemIconBinding>();
        private Action purchase;
        private Action use;
        public string ItemId { get; private set; }
        public long Price { get; private set; }

        /// <summary>绑定该行的领域定义和本界面回调。</summary>
        /// <param name="item">保存的内容目录定义。</param>
        /// <param name="buy">购买操作，不在行组件里改钱。</param>
        /// <param name="useItem">使用或撤销预备操作。</param>
        public void Bind(CasinoItemDefinition item, Action buy, Action useItem)
        {
            if (purchaseButton != null) purchaseButton.onClick.RemoveListener(OnPurchase);
            if (useButton != null) useButton.onClick.RemoveListener(OnUse);
            ItemId = item.Id; Price = item.Price; purchase = buy; use = useItem;
            RefreshIcon();
            titleText.text = item.Name + " · " + item.Price;
            descriptionText.text = item.Description;
            purchaseButton.onClick.AddListener(OnPurchase); useButton.onClick.AddListener(OnUse);
        }

        /// <summary>保存模板图标引用，克隆行时沿用同一组Sprite资产。</summary>
        /// <param name="target">模板上已有的图标Image。</param>
        /// <param name="bindings">24件道具稳定ID与原创透明Sprite的映射。</param>
        public void ConfigureIcons(Image target, CasinoItemIconBinding[] bindings)
        { iconImage = target; itemIcons = bindings ?? Array.Empty<CasinoItemIconBinding>(); RefreshIcon(); }

        private void RefreshIcon()
        {
            if (iconImage == null) return;
            Sprite sprite = null;
            foreach (var binding in itemIcons) if (binding != null && binding.Id == ItemId) { sprite = binding.Sprite; break; }
            iconImage.sprite = sprite; iconImage.enabled = sprite != null; iconImage.preserveAspect = true; iconImage.raycastTarget = false;
        }

        /// <summary>刷新当前库存和可操作状态，保留模板上的持久按钮配置。</summary>
        /// <param name="count">库存数量。</param>
        /// <param name="prepared">是否预备到下一笔投入。</param>
        /// <param name="canPurchase">阶段/余额允许购买。</param>
        /// <param name="canUse">有库存且阶段允许使用。</param>
        public void Refresh(int count, bool prepared, bool canPurchase, bool canUse)
        {
            countText.text = "库存 " + count + (prepared ? " · 已预备" : string.Empty);
            purchaseButton.interactable = canPurchase; useButton.interactable = canUse;
            useButtonText.text = prepared ? "撤销预备" : "使用";
        }
        private void OnPurchase() => purchase?.Invoke();
        private void OnUse() => use?.Invoke();
        private void OnDestroy()
        {
            if (purchaseButton != null) purchaseButton.onClick.RemoveListener(OnPurchase);
            if (useButton != null) useButton.onClick.RemoveListener(OnUse);
            purchase = null; use = null;
        }
    }
}
