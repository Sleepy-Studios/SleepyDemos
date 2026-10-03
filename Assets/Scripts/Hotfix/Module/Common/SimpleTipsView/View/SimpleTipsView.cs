namespace Hotfix
{
    using Core.Runtime;
    using Cysharp.Threading.Tasks;
    using UnityEngine;

    [Module("Common")]
    [Mvc("SimpleTipsView")]
    public partial class SimpleTipsView : View
    {
        private SimpleTipsRequest request;

        protected override void OnGameObjectInitialize()
        {
            Button_Blocker.onClick.AddListener(RequestClose);
            UIMenuScope_SimpleTips.Canceled += RequestClose;
            UITooltip_SimpleTips.CloseRequested += RequestClose;
        }

        internal void SetData(SimpleTipsRequest data)
        {
            request = data;
            UITipsPanel_SimpleTips.SetContent(data.Content, data.Title, data.Options.MaxWidth);
            Button_Blocker.gameObject.SetActive(data.Options.CloseOnOutside);
            UIMenuScope_SimpleTips.enabled = data.Options.CloseOnOutside;
            var group = gameObject.GetComponent<CanvasGroup>();
            group.blocksRaycasts = data.Options.CloseOnOutside;
            Position();
        }

        protected override void OnShow() => Position();

        protected override void OnHide()
        {
            UITooltip_SimpleTips.ClearTarget();
            request = default;
        }

        protected override void OnDestroy()
        {
            Button_Blocker.onClick.RemoveListener(RequestClose);
            UIMenuScope_SimpleTips.Canceled -= RequestClose;
            UITooltip_SimpleTips.CloseRequested -= RequestClose;
            request = default;
        }

        private void Position()
        {
            if (request.FollowsTarget)
                UITooltip_SimpleTips.SetTarget(request.Target, request.Options.Direction, request.Options.Gap);
            else
                UITooltip_SimpleTips.SetScreenPoint(request.Point, request.Options.Direction, request.Options.Gap);
        }

        private void RequestClose()
        {
            TipsUI.CloseSimpleAsync(request.Version, this, request.Token).Forget();
        }
    }
}
