using System;
using Hotfix.JinxCasino.Adapters;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Hotfix.JinxCasino.Adapters.UI
{
    /// 保存的本机交流面板；六种固定消息及附近机台标记，不自行构造传输或网络回执。
    public sealed class JinxCasinoSocialPresenter : MonoBehaviour
    {
        [SerializeField] private TMP_Dropdown messageDropdown;
        [SerializeField] private TMP_Text feedback;
        [SerializeField] private Button sendButton;
        [SerializeField] private Button markButton;
        [SerializeField] private Button clearButton;
        [SerializeField] private Button closeButton;
        private JinxCasinoController owner;
        private JinxCasinoLocalSocialFeedback consumer;
        private UnityAction send;
        private UnityAction mark;
        private UnityAction clear;
        private UnityAction close;

        /// <summary>绑定保存按钮和真实本地反馈消费者。</summary>
        /// <param name="controller">已开始离线旅程的宿主。</param>
        /// <param name="onClose">返回场地，解除当前模态移动阻挡。</param>
        public void Bind(JinxCasinoController controller, Action onClose)
        {
            Unbind(); owner = controller; consumer = owner.GetComponent<JinxCasinoLocalSocialFeedback>();
            if (consumer != null) consumer.Changed += Refresh;
            messageDropdown.ClearOptions(); messageDropdown.AddOptions(new System.Collections.Generic.List<string>(CasinoLocalMessages.Labels));
            send = () => { PlayClick(); int index = messageDropdown.value; var ids = CasinoLocalMessages.Ids;
                if (index >= 0 && index < ids.Length) owner.SendLocalMessage(ids[index]); Refresh(); };
            mark = () => { PlayClick(); owner.MarkNearbyStation(); Refresh(); };
            clear = () => { PlayClick(); owner.ClearLocalSocialFeedback(); Refresh(); };
            close = () => { PlayClick(); onClose?.Invoke(); };
            sendButton.onClick.AddListener(send); markButton.onClick.AddListener(mark); clearButton.onClick.AddListener(clear); closeButton.onClick.AddListener(close);
            Refresh();
        }

        /// View释放时清理标记；单纯关闭交流模态保留世界标记供玩家回到场地查看。
        public void Unbind()
        {
            if (consumer != null) consumer.Changed -= Refresh;
            if (owner != null) owner.ClearLocalSocialFeedback();
            if (send != null) sendButton.onClick.RemoveListener(send); if (mark != null) markButton.onClick.RemoveListener(mark);
            if (clear != null) clearButton.onClick.RemoveListener(clear); if (close != null) closeButton.onClick.RemoveListener(close);
            send = mark = clear = close = null; owner = null; consumer = null;
        }
        private void Refresh()
        {
            if (owner == null) return;
            feedback.text = owner.LocalSocialStatus;
            bool available = owner.Game.HasAdventure && consumer != null;
            sendButton.interactable = markButton.interactable = available; clearButton.interactable = consumer != null;
        }
        private void PlayClick() => owner?.GetComponent<JinxCasinoAudioDirector>()?.PlayUiClick();
        private void OnEnable() => Refresh();
        private void OnDestroy() => Unbind();
    }
}
