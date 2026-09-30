using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix.BlockPorters.Adapters
{
    /// 序列化 UI 引用与布局适配；业务生命周期仍由 Core View 管理。
    public sealed class BlockPortersHudPresenter : MonoBehaviour
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private TextMeshProUGUI title;
        [SerializeField] private TextMeshProUGUI progress;
        [SerializeField] private Image progressFill;
        [SerializeField] private TextMeshProUGUI slots;
        [SerializeField] private TextMeshProUGUI hint;
        [SerializeField] private Button[] columns;
        [SerializeField] private TextMeshProUGUI[] columnLabels;
        [SerializeField] private Image[] previews;
        [SerializeField] private TextMeshProUGUI[] previewLabels;
        [SerializeField] private Image[] taskSlots;
        [SerializeField] private TextMeshProUGUI[] taskLabels;
        [SerializeField] private Button pause;
        [SerializeField] private Button sound;
        [SerializeField] private Button restart;
        [SerializeField] private Button exit;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TextMeshProUGUI resultTitle;
        [SerializeField] private TextMeshProUGUI resultDescription;
        [SerializeField] private Button revive;
        [SerializeField] private Button next;
        [SerializeField] private Button resultRestart;
        [SerializeField] private Button resultExit;
        [SerializeField] private TextMeshProUGUI[] controlLabels;
        private BlockPortersController owner;
        private Vector2 lastSize;
        private Rect lastSafeArea;

        /// <summary>保存固定节点引用；所有绑定节点同时进入 ComponentItemIndex。</summary>
        /// <param name="layout">600×1080 的竖屏内容根。</param>
        /// <param name="texts">标题、进度、任务位、提示、结果标题、结果说明。</param>
        /// <param name="fill">进度条填充。</param>
        /// <param name="queueButtons">四列队伍按钮。</param>
        /// <param name="queueLabels">四列队伍标签。</param>
        /// <param name="queuePreviews">每列下一队的预览。</param>
        /// <param name="queuePreviewLabels">后排队伍人数标签。</param>
        /// <param name="slotImages">七个任务位的底色。</param>
        /// <param name="slotTexts">七个任务位的待命/运输标签。</param>
        /// <param name="buttons">暂停、声音、重开、返回、复活、下一关、结果重开、结果返回。</param>
        /// <param name="panel">结果全屏拦截面板。</param>
        /// <param name="buttonTexts">暂停、声音、复活和下一关标签。</param>
        public void Configure(RectTransform layout, TextMeshProUGUI[] texts, Image fill, Button[] queueButtons,
            TextMeshProUGUI[] queueLabels, Image[] queuePreviews, TextMeshProUGUI[] queuePreviewLabels,
            Image[] slotImages, TextMeshProUGUI[] slotTexts, Button[] buttons, GameObject panel, TextMeshProUGUI[] buttonTexts)
        {
            content = layout; title = texts[0]; progress = texts[1]; slots = texts[2]; hint = texts[3];
            resultTitle = texts[4]; resultDescription = texts[5]; progressFill = fill;
            columns = queueButtons; columnLabels = queueLabels; previews = queuePreviews; previewLabels = queuePreviewLabels;
            taskSlots = slotImages; taskLabels = slotTexts;
            pause = buttons[0]; sound = buttons[1]; restart = buttons[2]; exit = buttons[3];
            revive = buttons[4]; next = buttons[5]; resultRestart = buttons[6]; resultExit = buttons[7]; resultPanel = panel;
            controlLabels = buttonTexts;
        }

        private void Awake()
        {
            for (int i = 0; i < columns.Length; i++) { int column = i; columns[i].onClick.AddListener(() => owner?.Dispatch(column)); }
            pause.onClick.AddListener(() => owner?.TogglePause());
            sound.onClick.AddListener(() => owner?.ToggleSound());
            restart.onClick.AddListener(() => owner?.Restart());
            resultRestart.onClick.AddListener(() => owner?.Restart());
            exit.onClick.AddListener(() => owner?.ReturnToHub());
            resultExit.onClick.AddListener(() => owner?.ReturnToHub());
            revive.onClick.AddListener(() => owner?.RequestRevive());
            next.onClick.AddListener(() => owner?.NextLevel());
        }

        /// <summary>绑定单个场景会话，重复绑定前先解除旧订阅。</summary>
        /// <param name="controller">Core UI ShowAsync 数据载荷传入的场景所有者。</param>
        public void Bind(BlockPortersController controller) { Unbind(); owner = controller; owner.Changed += Refresh; Refresh(); }
        /// 解除订阅并移除对场景会话的引用。
        public void Unbind() { if (owner != null) owner.Changed -= Refresh; owner = null; }
        private void OnDestroy() => Unbind();

        private void LateUpdate()
        {
            Vector2 size = ((RectTransform)transform).rect.size;
            if (size == lastSize && lastSafeArea == Screen.safeArea) return;
            lastSize = size; lastSafeArea = Screen.safeArea;
            float safeWidth = size.x * Screen.safeArea.width / Mathf.Max(1, Screen.width);
            float safeHeight = size.y * Screen.safeArea.height / Mathf.Max(1, Screen.height);
            float scale = Mathf.Min(safeWidth / 600, safeHeight / 1080);
            content.localScale = Vector3.one * scale;
            content.anchoredPosition = new Vector2(
                (Screen.safeArea.center.x / Mathf.Max(1, Screen.width) - 0.5f) * size.x,
                (Screen.safeArea.center.y / Mathf.Max(1, Screen.height) - 0.5f) * size.y);
        }

        private void Refresh()
        {
            if (owner == null || owner.Session == null) return;
            var session = owner.Session;
            bool canPlay = !owner.IsPaused && !owner.IsExiting && !owner.IsRewardPending && session.Status == BlockPortersStatus.Playing;
            title.text = $"第 {owner.LevelIndex + 1} 关 · {owner.CurrentLevel.DisplayName}";
            progress.text = $"已入坑 {session.Delivered} / {session.Total}";
            progressFill.fillAmount = (float)session.Delivered / session.Total;
            slots.text = $"任务位 {session.Teams.Count} / {session.Capacity}";
            hint.text = owner.IsPaused ? "已暂停 · 点击继续" : "点最前排派队 · 同色小人只搬可达方块";
            for (int column = 0; column < 4; column++)
            {
                var team = session.Peek(column);
                columns[column].interactable = canPlay && session.Teams.Count < session.Capacity && team.HasValue;
                columns[column].image.color = team.HasValue ? owner.CurrentLevel.Palette[team.Value.Color] : new Color(0.16f, 0.21f, 0.3f);
                columnLabels[column].text = team.HasValue ? $"{owner.CurrentLevel.ColorLabel(team.Value.Color)}队\n{team.Value.Count} 人" : "完成";
                for (int row = 0; row < 1; row++)
                {
                    int index = column + row;
                    var upcoming = session.Peek(column, row + 1);
                    previews[index].gameObject.SetActive(upcoming.HasValue);
                    if (upcoming.HasValue)
                    {
                        previews[index].color = owner.CurrentLevel.Palette[upcoming.Value.Color];
                        previewLabels[index].text = $"{owner.CurrentLevel.ColorLabel(upcoming.Value.Color)} · {upcoming.Value.Count}";
                    }
                }
            }
            for (int i = 0; i < 7; i++)
            {
                taskSlots[i].gameObject.SetActive(i < session.Capacity);
                PorterTeam active = null;
                foreach (var team in session.Teams) if (team.Slot == i) { active = team; break; }
                taskSlots[i].color = active != null ? owner.CurrentLevel.Palette[active.Color] : new Color(0.14f, 0.2f, 0.29f);
                taskLabels[i].color = active != null ? new Color(0.04f, 0.07f, 0.12f) : Color.white;
                taskLabels[i].text = active != null ? $"{active.Waiting} 等\n{active.InFlight} 运" : "空位";
            }
            controlLabels[0].text = owner.IsPaused ? "继续" : "暂停";
            controlLabels[1].text = owner.IsMuted ? "静音" : "声音";
            pause.interactable = !owner.IsExiting && session.Status == BlockPortersStatus.Playing;
            restart.interactable = exit.interactable = !owner.IsExiting;
            bool failed = session.Status == BlockPortersStatus.Failed;
            resultPanel.SetActive(session.Status != BlockPortersStatus.Playing);
            resultTitle.text = failed ? "任务位堵满了" : "全部搬空！";
            resultDescription.text = failed ? "试着先派能接触到外层方块的颜色。\n复活会增加 2 个任务位。" : "小人和方块都跳进深坑啦。\n准备挑战下一幅图案吧！";
            revive.gameObject.SetActive(failed);
            revive.interactable = failed && !session.HasRevived && !owner.IsRewardPending && !owner.IsExiting;
            controlLabels[2].text = session.HasRevived ? "本关已复活" : owner.IsRewardPending ? "模拟奖励处理中" : "模拟复活 · +2 位";
            next.gameObject.SetActive(!failed);
            controlLabels[3].text = owner.LevelIndex == owner.LevelCount - 1 ? "再玩一轮" : "下一关";
            resultRestart.interactable = resultExit.interactable = next.interactable = !owner.IsExiting;
        }

    }
}
