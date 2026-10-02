using System;
using Hotfix.JinxCasino.Rules;
using System.Collections.Generic;
using Core.Runtime.Inputs;
using UnityEngine;

namespace Hotfix.JinxCasino.Adapters
{
    public sealed partial class JinxCasinoController
    {
        [SerializeField] private JinxCasinoShopCounter shopCounter;
        private JinxCasinoShopCounter focusedShop;
        private IReadOnlyList<JinxCasinoTableTarget> focusedShopTargets = Array.Empty<JinxCasinoTableTarget>();
        private int selectedShopProduct;
        private int lastShopActionFrame = -1;
        public bool HasShopFocus => focusedShop != null;
        private bool HasShopBinding => !ReferenceEquals(focusedShop, null);
        public string ShopFeedback { get; private set; }

        /// <summary>绑定保存柜台，旧原型不配置此字段。</summary>
        /// <param name="counter">本场景柜台。</param>
        public void ConfigureShopCounter(JinxCasinoShopCounter counter) => shopCounter = counter;

        /// 玩家与柜台接近锚点的真实距离，两端采用同一交互范围。
        public bool IsShopNearby => shopCounter != null && shopCounter.isActiveAndEnabled && body != null &&
            (shopCounter.InteractionPosition - body.transform.position).sqrMagnitude <= 9;

        private bool PreferNearbyShop(JinxCasinoStation station) => IsShopNearby && (station == null ||
            (shopCounter.InteractionPosition - body.transform.position).sqrMagnitude < (station.InteractionPosition - body.transform.position).sqrMagnitude);

        private bool TryOpenImmersionShop()
        {
            if (immersionInput == null || !IsShopNearby || IsImmersionPaused || IsAdventureInputBlocked ||
                !tableFocus.TryEnter(shopCounter, shopCounter.FocusPose, 58)) return false;
            focusedShop = shopCounter; selectedShopProduct = 0; lastShopActionFrame = -1; ShopFeedback = null;
            focusedShopTargets = focusedShop.Targets;
            foreach (var target in focusedShopTargets) { target.BindPresentationClock(PresentationClock); target.Invoked += OnShopTargetInvoked; }
            tableSelection.Bind(focusedShop.transform, focusedShop.Targets);
            RefreshImmersionShop(); ApplyImmersionContext(GameplayInputContext.Interaction); ImmersionInputChanged?.Invoke();
            return true;
        }

        private void OnShopTargetInvoked(JinxCasinoTableTarget target) => ApplyShopCommand(target.Action, target.Value);

        private void ApplyShopCommand(JinxCasinoTableAction action, int value)
        {
            if (focusedShop == null || !tableFocus.IsReady || IsImmersionPaused || !IsShopNearby || !Game.HasAdventure ||
                lastShopActionFrame == Time.frameCount) return;
            lastShopActionFrame = Time.frameCount;
            if (action == JinxCasinoTableAction.SelectProduct)
            {
                if (focusedShop.Product(value) == null) return;
                selectedShopProduct = value; ShopFeedback = "已选择，尚未扣款；按购买确认。";
            }
            else
            {
                var item = focusedShop.Product(selectedShopProduct);
                if (item == null) return;
                CasinoAdventureResult result = null;
                if (action == JinxCasinoTableAction.PurchaseProduct) result = Game.PurchaseItem(item.Id, Time.frameCount);
                else if (action == JinxCasinoTableAction.UseProduct || action == JinxCasinoTableAction.Secondary)
                    result = Game.State.PreparedItems.Contains(item.Id) ? Game.CancelPreparedItem(item.Id, Time.frameCount) : UseAdventureItem(item.Id, "team");
                else if (action == JinxCasinoTableAction.Help) ShopFeedback = item.Description;
                if (result != null)
                {
                    ShopFeedback = result.Description ?? result.Error;
                    ObserveTutorialShopCommand(action, item.Id, result);
                }
            }
            RefreshImmersionShop(); ImmersionInputChanged?.Invoke();
        }

        private void RefreshImmersionShop() => focusedShop?.Present(Game.State, selectedShopProduct, ShopFeedback);

        private void CloseImmersionShop()
        {
            // 组件可能先于其物件销毁；用进入时的目标集合退订，不能依赖Unity fake-null组件。
            foreach (var target in focusedShopTargets) if (target != null) target.Invoked -= OnShopTargetInvoked;
            focusedShopTargets = Array.Empty<JinxCasinoTableTarget>();
            focusedShop = null; ShopFeedback = null;
        }
    }
}
