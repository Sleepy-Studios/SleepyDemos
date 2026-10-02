using System;
using System.Linq;
using Hotfix.JinxCasino.Rules;
using TMPro;
using UnityEngine;

namespace Hotfix.JinxCasino.Interaction
{
    /// 保存的实体补给柜台；商品、价格与库存只读原有领域目录和快照。
    public sealed class JinxCasinoShopCounter : MonoBehaviour
    {
        [SerializeField] private string counterId;
        [SerializeField] private Transform approach;
        [SerializeField] private Transform focusPose;
        [SerializeField] private string[] itemIds = Array.Empty<string>();
        [SerializeField] private JinxCasinoTableTarget[] targets = Array.Empty<JinxCasinoTableTarget>();
        [SerializeField] private TMP_Text quote;
        [SerializeField] private TMP_Text receipt;
        [SerializeField] private TMP_Text useLabel;
        public string CounterId => counterId;
        public Transform FocusPose => focusPose;
        public Vector3 InteractionPosition => approach != null ? approach.position : transform.position;
        public System.Collections.Generic.IReadOnlyList<JinxCasinoTableTarget> Targets => targets;
        public int ProductCount => itemIds.Length;

        /// <summary>装配三款补给及实体操作物件，不生成第二套商店规则。</summary>
        /// <param name="id">保存的柜台ID。</param>
        /// <param name="nearby">接近位置。</param>
        /// <param name="cameraPose">借用本地相机的聚焦挂点。</param>
        /// <param name="products">目录中的商品ID。</param>
        /// <param name="controls">属于柜台的操作目标。</param>
        /// <param name="offer">商品详情铭牌。</param>
        /// <param name="result">交易回执铭牌。</param>
        /// <param name="inventoryAction">库存使用按钮文字。</param>
        public void Configure(string id, Transform nearby, Transform cameraPose, string[] products, JinxCasinoTableTarget[] controls,
            TMP_Text offer, TMP_Text result, TMP_Text inventoryAction)
        {
            if (string.IsNullOrWhiteSpace(id) || nearby == null || cameraPose == null || !cameraPose.IsChildOf(transform) ||
                products == null || products.Length == 0 || products.Distinct().Count() != products.Length ||
                products.Any(item => CasinoContentCatalog.FindItem(item) == null) || controls == null || controls.Length == 0 ||
                controls.Where(target => target != null).Select(target => target.TargetId).Distinct().Count() != controls.Length ||
                controls.Any(target => target == null || !target.transform.IsChildOf(transform)))
                throw new ArgumentException("柜台需要合法商品、挂点及自身的操作目标。");
            counterId = id; approach = nearby; focusPose = cameraPose; itemIds = (string[])products.Clone();
            targets = (JinxCasinoTableTarget[])controls.Clone(); quote = offer; receipt = result; useLabel = inventoryAction;
        }

        /// <summary>读取选择的真实商品；越界时不回退到另一商品。</summary>
        /// <param name="index">保存的商品序号。</param>
        public CasinoItemDefinition Product(int index) => index >= 0 && index < itemIds.Length ? CasinoContentCatalog.FindItem(itemIds[index]) : null;

        /// <summary>刷新完整报价、原库存与可操作状态；仅显示，不支付或消费。</summary>
        /// <param name="state">当前冒险公开副本。</param>
        /// <param name="selected">选中的商品序号。</param>
        /// <param name="message">最近一次真实交易反馈。</param>
        public void Present(CasinoAdventureState state, int selected, string message)
        {
            var item = Product(selected);
            bool available = state != null && item != null && (state.Config.ShopItemIds.Length == 0 || state.Config.ShopItemIds.Contains(item.Id));
            int count = state?.Inventory.Find(value => value.ItemId == item?.Id)?.Count ?? 0;
            bool prepared = state != null && item != null && state.PreparedItems.Contains(item.Id);
            bool canBuy = available && state.Phase != CasinoAdventurePhase.Ended && state.Phase != CasinoAdventurePhase.Failed &&
                state.Phase != CasinoAdventurePhase.Closing && state.Coins - state.LockedCoins >= item.Price;
            if (quote != null) quote.text = item == null ? "选择一件补给" : item.Name + "  ·  " + item.Price + "筹码\n" + item.Description +
                "\n库存 " + count + "  ·  可用筹码 " + (state == null ? 0 : state.Coins - state.LockedCoins) + (available ? string.Empty : "\n本局不售此商品，已有库存仍可使用");
            if (receipt != null) receipt.text = message ?? "先选实物，再按购买；选择本身不扣款。";
            if (useLabel != null) useLabel.text = prepared ? "取消预备" : "使用库存";
            foreach (var target in targets)
            {
                bool enabled = target.Action == JinxCasinoTableAction.SelectProduct || target.Action == JinxCasinoTableAction.Help ||
                    target.Action == JinxCasinoTableAction.PurchaseProduct && canBuy ||
                    target.Action == JinxCasinoTableAction.UseProduct && count > 0 && state.Phase != CasinoAdventurePhase.Ended;
                target.SetAvailable(enabled, "当前阶段、余额或库存不满足操作条件。");
            }
        }
    }
}
