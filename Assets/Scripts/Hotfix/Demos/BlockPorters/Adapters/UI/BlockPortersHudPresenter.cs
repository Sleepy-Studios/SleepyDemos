using Core.Runtime;
using Core.Runtime.Inputs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix.BlockPorters.Adapters
{
    /// 序列化 UI 引用与布局适配；业务生命周期仍由 Core View 管理。
    public sealed class BlockPortersHudPresenter : MonoBehaviour
    {
        [SerializeField] private RectTransform content;
        [SerializeField] private BlockPortersUiStyle style;
        [SerializeField] private Image[] queueFaces;
        [SerializeField] private Image[] previewFaces;
        [SerializeField] private TextMeshProUGUI title;
        [SerializeField] private TextMeshProUGUI progress;
        [SerializeField] private Image progressFill;
        [SerializeField] private TextMeshProUGUI slots;
        [SerializeField] private TextMeshProUGUI hint;
        [SerializeField] private Button[] columns;
        [SerializeField] private TextMeshProUGUI[] columnLabels;
        [SerializeField] private Image[] previews;
        [SerializeField] private TextMeshProUGUI[] previewLabels;
        [SerializeField] private TextMeshProUGUI[] previewColorLabels;
        [SerializeField] private Image[] taskSlots;
        [SerializeField] private TextMeshProUGUI[] taskLabels;
        [SerializeField] private Button pause;
        [SerializeField] private Button sound;
        [SerializeField] private Button restart;
        [SerializeField] private Button exit;
        [SerializeField] private GameObject resultPanel;
        [SerializeField] private TextMeshProUGUI resultTitle;
        [SerializeField] private TextMeshProUGUI resultDescription;
        [SerializeField] private Button[] extraButtons;
        [SerializeField] private Button[] resultExtraButtons;
        [SerializeField] private TextMeshProUGUI[] extraLabels;
        [SerializeField] private Image[] extraIcons;
                [SerializeField] private Button next;
        [SerializeField] private Button resultRestart;
        [SerializeField] private Button resultExit;
        [SerializeField] private TextMeshProUGUI[] controlLabels;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private RectTransform settingsCard;
        [SerializeField] private RectTransform resultCard;
        [SerializeField] private Button settingsContinue;
        [SerializeField] private Button settingsClose;
        [SerializeField] private TextMeshProUGUI levelName;
        [SerializeField] private TextMeshProUGUI[] queueColorLabels;
        [SerializeField] private Image[] taskBadges;
        private BlockPortersController owner;
        private Vector2 lastSize;
        private Rect lastSafeArea;
        private BlockPortersButtonFeedback[] feedback;
        private readonly int[] queueKeys = { -1, -1, -1, -1, -1 };
        private readonly float[] advanceAges = { -1, -1, -1, -1, -1 };
        private readonly Vector2[] queuePositions = new Vector2[5];
        private readonly Vector2[] queueSizes = new Vector2[5];
        private readonly Vector2[] countPositions = new Vector2[5];
        private readonly Vector2[] countSizes = new Vector2[5];
        private readonly float[] countFontSizes = new float[5];
        private readonly Vector2[] previewPositions = new Vector2[15];
        private bool layoutReady;
        private BlockPortersSession shownSession;
        private float AdvanceDuration => style.AdvanceDuration;
        private bool settingsOpen;
        private bool wasPaused;
        private bool hadResult;
        private float settingsAge;
        private float resultAge;
        private UIProgressBar progressBar;
        private float displayedProgress;
        private float targetProgress;
        private bool refreshPending;
        private static readonly Color Empty = new(.91f, .87f, .81f);

        private void Awake()
        {
            progressBar = progressFill.GetComponent<UIProgressBar>();
            ApplyTileStyle();
            for (int i = 0; i < columns.Length; i++)
            {
                int column = i;
                var rect = (RectTransform)columns[i].transform;
                queuePositions[i] = rect.anchoredPosition;
                queueSizes[i] = rect.sizeDelta;
                countPositions[i] = columnLabels[i].rectTransform.anchoredPosition;
                countSizes[i] = columnLabels[i].rectTransform.sizeDelta;
                countFontSizes[i] = columnLabels[i].fontSize;
                columns[i].transition = Selectable.Transition.None;
                columns[i].onClick.AddListener(() => DispatchColumn(column));
            }
            layoutReady = true;
            feedback = GetComponentsInChildren<BlockPortersButtonFeedback>(true);
            pause.onClick.AddListener(ToggleSettings);
            settingsContinue.onClick.AddListener(CloseSettings);
            settingsClose.onClick.AddListener(CloseSettings);
            sound.onClick.AddListener(() => owner?.ToggleSound());
            restart.onClick.AddListener(() => owner?.Restart());
            resultRestart.onClick.AddListener(() => owner?.Restart());
            exit.onClick.AddListener(() => owner?.ReturnToHub());
            resultExit.onClick.AddListener(() => owner?.ReturnToHub());
            for (int side = 0; side < 2; side++)
            {
                int index = side;
                // 开放后仍使用完整的凹槽颜色；按压由统一反馈组件处理。
                extraButtons[side].transition = Selectable.Transition.None;
                extraButtons[side].onClick.AddListener(() => owner?.RequestUnlockSlot(index));
                resultExtraButtons[side].onClick.AddListener(() => owner?.RequestUnlockSlot(index));
            }
            for (int i = 0; i < previews.Length; i++) previewPositions[i] = previews[i].rectTransform.anchoredPosition;
            next.onClick.AddListener(() => owner?.NextLevel());
        }

        private void ApplyTileStyle()
        {
            for (int column = 0; column < 5; column++)
            {
                var rect = (RectTransform)columns[column].transform;
                rect.sizeDelta = style.TileDimensions;
                rect.anchoredPosition = new Vector2(style.ColumnX(column) - 300, 540 - style.QueueRowY);
                queueFaces[column].rectTransform.sizeDelta = style.FaceDimensions;
                columnLabels[column].fontSize = style.CountFontSize;
                queueColorLabels[column].fontSize = style.DetailFontSize;
                for (int row = 0; row < 3; row++)
                {
                    int index = column * 3 + row;
                    previews[index].rectTransform.sizeDelta = style.TileDimensions;
                    previews[index].rectTransform.anchoredPosition = new Vector2(style.ColumnX(column) - 300,
                        540 - style.QueueRowY - (row + 1) * (style.TileSize + style.RowGap));
                    previewFaces[index].rectTransform.sizeDelta = style.FaceDimensions;
                    previewLabels[index].fontSize = style.CountFontSize;
                    previewColorLabels[index].fontSize = style.DetailFontSize;
                }
            }
            for (int i = 0; i < 7; i++)
            {
                taskSlots[i].rectTransform.sizeDelta = style.TileDimensions;
                Vector2 center = i < 5 ? new Vector2(style.ColumnX(i), style.TaskRowY)
                    : i == 5 ? style.LeftExtraCenter : style.RightExtraCenter;
                taskSlots[i].rectTransform.anchoredPosition = new Vector2(center.x - 300, 540 - center.y);
                taskBadges[i].rectTransform.sizeDelta = style.FaceDimensions;
                taskLabels[i].fontSize = style.CountFontSize;
                if (i >= 5) extraLabels[i - 5].fontSize = style.DetailFontSize;
            }
        }

        /// <summary>绑定单个场景会话，重复绑定前先解除旧订阅。</summary>
        /// <param name="controller">Core UI ShowAsync 数据载荷传入的场景所有者。</param>
        public void Bind(BlockPortersController controller)
        {
            Unbind(); owner = controller; settingsOpen = false; hadResult = false;
            displayedProgress = targetProgress = 0;
            for (int i = 0; i < queueKeys.Length; i++) queueKeys[i] = -1;
            var menu = GetComponent<UIMenuScope>();
            if (menu != null) menu.Canceled += ToggleSettings;
            var settingsMenu = settingsPanel.GetComponent<UIMenuScope>();
            if (settingsMenu != null) settingsMenu.Canceled += CloseSettings;
            owner.Changed += RequestRefresh; Refresh();
        }
        /// 解除订阅并移除对场景会话的引用。
        public void Unbind()
        {
            if (owner != null) owner.Changed -= RequestRefresh;
            var menu = GetComponent<UIMenuScope>();
            if (menu != null) menu.Canceled -= ToggleSettings;
            var settingsMenu = settingsPanel != null ? settingsPanel.GetComponent<UIMenuScope>() : null;
            if (settingsMenu != null) settingsMenu.Canceled -= CloseSettings;
            ClearAdvances(); shownSession = null; refreshPending = false;
            owner = null; settingsOpen = false;
        }
        private void OnDestroy() => Unbind();
        private void RequestRefresh() => refreshPending = true;

        private void DispatchColumn(int column)
        {
            if (owner == null || !columns[column].interactable || advanceAges[column] >= 0 ||
                owner.IsPaused || owner.IsExiting || owner.IsRewardPending) return;
            int count = owner.Session.Teams.Count;
            owner.Dispatch(column);
            if (owner.Session.Teams.Count <= count) return;
            if (owner.Session.Peek(column).HasValue) advanceAges[column] = 0;
            Refresh();
            if (advanceAges[column] >= 0) AnimateAdvance(column);
        }

        private void AnimateAdvance(int column)
        {
            var rect = (RectTransform)columns[column].transform;
            float t = Mathf.Clamp01(advanceAges[column] / AdvanceDuration);
            float eased = 1 - Mathf.Pow(1 - t, 3);
            int first = column * 3;
            rect.anchoredPosition = Vector2.Lerp(previewPositions[first], queuePositions[column], eased);
            columnLabels[column].fontSize = Mathf.Lerp(previewLabels[first].fontSize, countFontSizes[column], eased);
            for (int row = 0; row < 2; row++)
                previews[first + row].rectTransform.anchoredPosition = Vector2.Lerp(previewPositions[first + row + 1], previewPositions[first + row], eased);
        }

        private void ClearAdvances()
        {
            for (int i = 0; i < advanceAges.Length; i++)
            {
                advanceAges[i] = -1;
                if (!layoutReady || columns == null || i >= columns.Length || columns[i] == null) continue;
                ResetAdvancePose(i);
            }
        }

        private void ResetAdvancePose(int column)
        {
            var rect = (RectTransform)columns[column].transform;
            rect.anchoredPosition = queuePositions[column]; rect.sizeDelta = queueSizes[column];
            columnLabels[column].rectTransform.anchoredPosition = countPositions[column]; columnLabels[column].rectTransform.sizeDelta = countSizes[column];
            columnLabels[column].fontSize = countFontSizes[column]; queueColorLabels[column].gameObject.SetActive(true);
            for (int row = 0; row < 3; row++) previews[column * 3 + row].rectTransform.anchoredPosition = previewPositions[column * 3 + row];
        }

        /// 打开设置时记录原暂停状态；关闭只恢复本面板产生的暂停。
        private void ToggleSettings()
        {
            if (owner == null || owner.IsExiting) return;
            if (settingsOpen) { CloseSettings(); return; }
            ClearAdvances();
            wasPaused = owner.IsPaused; settingsOpen = true; settingsAge = 0;
            settingsPanel.SetActive(true);
            settingsCard.localScale = Vector3.one * .94f;
            if (!wasPaused) owner.TogglePause();
            else Refresh();
        }

        private void CloseSettings()
        {
            if (owner == null || !settingsOpen) return;
            settingsOpen = false;
            settingsPanel.SetActive(false);
            if (!wasPaused && owner.IsPaused) owner.TogglePause();
            else Refresh();
        }

        private void LateUpdate()
        {
            if (refreshPending) { refreshPending = false; Refresh(); }
            float delta = Time.unscaledDeltaTime;
            if (feedback != null) foreach (var item in feedback) item.Tick(delta);
            bool advanceFinished = false;
            for (int i = 0; i < advanceAges.Length; i++)
            {
                if (advanceAges[i] < 0) continue;
                advanceAges[i] += delta; AnimateAdvance(i);
                if (advanceAges[i] < AdvanceDuration) continue;
                advanceAges[i] = -1; ResetAdvancePose(i); advanceFinished = true;
            }
            if (advanceFinished) Refresh();
            displayedProgress = Mathf.MoveTowards(displayedProgress, targetProgress, delta * .9f);
            progressBar.SetValue(displayedProgress);
            if (settingsOpen) { settingsAge += delta; AnimateCard(settingsCard, settingsAge); }
            if (hadResult) { resultAge += delta; AnimateCard(resultCard, resultAge); }
            Vector2 size = ((RectTransform)transform).rect.size;
            if (size == lastSize && lastSafeArea == Screen.safeArea) return;
            lastSize = size; lastSafeArea = Screen.safeArea;
            var screenLayout = BlockPortersScreenLayout.Calculate(Screen.width, Screen.height, Screen.safeArea);
            float scale = screenLayout.Scale * size.x / Mathf.Max(1, Screen.width);
            content.localScale = Vector3.one * scale;
            content.anchoredPosition = new Vector2(
                (Screen.safeArea.center.x / Mathf.Max(1, Screen.width) - 0.5f) * size.x,
                (Screen.safeArea.center.y / Mathf.Max(1, Screen.height) - 0.5f) * size.y);
            FitOverlay(settingsPanel, settingsCard, size, scale);
            FitOverlay(resultPanel, resultCard, size, scale);
        }

        private void FitOverlay(GameObject overlay, RectTransform card, Vector2 size, float scale)
        {
            float inverse = 1 / Mathf.Max(.0001f, scale);
            var rect = (RectTransform)overlay.transform;
            rect.sizeDelta = size * inverse;
            rect.anchoredPosition = -content.anchoredPosition * inverse;
            // 遮罩覆盖整个宿主，卡片仍居中于安全区；长屏留白也不能穿透到其他 Widget。
            card.anchoredPosition = content.anchoredPosition * inverse;
        }

        private static void AnimateCard(RectTransform card, float age)
        {
            float t = Mathf.Clamp01(age / .18f);
            card.localScale = Vector3.one * Mathf.Lerp(.94f, 1, 1 - Mathf.Pow(1 - t, 3));
        }

        private void Refresh()
        {
            if (owner == null || owner.Session == null) return;
            var session = owner.Session;
            bool canPlay = !owner.IsPaused && !owner.IsExiting && !owner.IsRewardPending && session.Status == BlockPortersStatus.Playing;
            if (shownSession != session)
            {
                shownSession = session; displayedProgress = targetProgress = 0;
                ClearAdvances();
                for (int i = 0; i < queueKeys.Length; i++) queueKeys[i] = -1;
            }
            if (!canPlay) ClearAdvances();
            title.text = $"第 {owner.LevelIndex + 1} 关";
            levelName.text = owner.CurrentLevel.DisplayName;
            progress.text = $"{session.Delivered} / {session.Total}";
            targetProgress = (float)session.Delivered / session.Total;
            if (targetProgress < displayedProgress || session.Status == BlockPortersStatus.Won) displayedProgress = targetProgress;
            progressBar.SetValue(displayedProgress);
            slots.text = $"搬运队伍  {session.Teams.Count} / {session.Capacity}";
            bool waiting = false;
            foreach (var team in session.Teams) waiting |= team.Waiting > 0;
            hint.text = owner.IsPaused ? "休息一下，随时继续" : waiting ? "有队伍在等路 · 先搬开外层" : "点前排派队，一起搬空图案";
            for (int column = 0; column < columns.Length; column++)
            {
                var team = session.Peek(column);
                columns[column].interactable = canPlay && advanceAges[column] < 0 && session.Teams.Count < session.Capacity && team.HasValue;
                Color color = team.HasValue ? owner.CurrentLevel.Palette[team.Value.Color] : Empty;
                columns[column].image.color = Color.white;
                queueFaces[column].gameObject.SetActive(team.HasValue);
                queueFaces[column].color = color;
                columnLabels[column].color = queueColorLabels[column].color = style.Ink(color);
                columnLabels[column].text = team.HasValue ? $"<b>{team.Value.Count}</b>" : "";
                queueColorLabels[column].text = team.HasValue ? owner.CurrentLevel.ColorLabel(team.Value.Color) : "";
                int key = team.HasValue ? team.Value.Color * 9 + team.Value.Count : -2;
                if (queueKeys[column] != -1 && queueKeys[column] != key) columns[column].GetComponent<BlockPortersButtonFeedback>().Pulse();
                queueKeys[column] = key;
                for (int row = 0; row < 3; row++)
                {
                    int index = column * 3 + row;
                    var upcoming = session.Peek(column, row + 1);
                    previews[index].gameObject.SetActive(upcoming.HasValue && (row < 2 || advanceAges[column] < 0));
                    if (upcoming.HasValue)
                    {
                        Color nextColor = owner.CurrentLevel.Palette[upcoming.Value.Color];
                        previews[index].color = style.PreviewBaseTint;
                        previewFaces[index].color = nextColor;
                        previewLabels[index].color = previewColorLabels[index].color = style.Ink(nextColor);
                        previewLabels[index].text = $"<b>{upcoming.Value.Count}</b>";
                        previewColorLabels[index].text = owner.CurrentLevel.ColorLabel(upcoming.Value.Color);
                    }
                }
            }
            for (int i = 0; i < 7; i++)
            {
                taskSlots[i].gameObject.SetActive(true);
                bool unlocked = session.IsSlotAvailable(i);
                PorterTeam active = null;
                foreach (var team in session.Teams) if (team.Slot == i) { active = team; break; }
                taskSlots[i].color = Color.white;
                taskBadges[i].gameObject.SetActive(active != null);
                taskBadges[i].rectTransform.sizeDelta = style.FaceDimensions;
                taskBadges[i].color = active != null ? owner.CurrentLevel.Palette[active.Color] : Empty;
                taskLabels[i].color = active != null ? style.Ink(owner.CurrentLevel.Palette[active.Color]) : style.DarkInk;
                taskLabels[i].text = active != null ? $"<b>{active.Count - active.Delivered}</b>\n<size={style.DetailFontSize}>{active.Waiting}待 {active.InFlight}运</size>" : "";
                if (i < 5) continue;
                int side = i - 5;
                extraButtons[side].interactable = !unlocked && !owner.IsExiting && !owner.IsRewardPending && session.Status != BlockPortersStatus.Won;
                extraIcons[side].gameObject.SetActive(!unlocked);
                extraLabels[side].gameObject.SetActive(!unlocked);
                extraLabels[side].text = owner.IsRewardPending ? "模拟奖励中" : "模拟广告\n解锁 +1";
                resultExtraButtons[side].gameObject.SetActive(session.Status == BlockPortersStatus.Failed && !unlocked);
                resultExtraButtons[side].interactable = extraButtons[side].interactable;
            }
            if (settingsOpen && !owner.IsPaused) settingsOpen = false;
            settingsPanel.SetActive(settingsOpen);
            controlLabels[0].text = "";
            controlLabels[1].text = owner.IsMuted ? "声音已关闭" : "声音已开启";
            pause.interactable = !owner.IsExiting && session.Status == BlockPortersStatus.Playing;
            restart.interactable = exit.interactable = !owner.IsExiting;
            bool failed = session.Status == BlockPortersStatus.Failed;
            bool showResult = session.Status != BlockPortersStatus.Playing;
            if (showResult && !hadResult) resultAge = 0;
            hadResult = showResult; resultPanel.SetActive(showResult);
            resultTitle.text = failed ? "队伍堵住啦" : "搬得真漂亮！";
            resultDescription.text = failed ? session.UnlockedExtraSlots == 3 ? "任务位都已开放。\n重新挑战，先搬开外层吧！" : "解锁额外任务位继续搬运，\n或者重新挑战，先搬开外层。" : "这一幅图案已经全部搬空。\n下一幅，也一起轻松完成吧！";
            next.gameObject.SetActive(!failed);
            controlLabels[2].text = owner.LevelIndex == owner.LevelCount - 1 ? "再玩一轮" : "下一关";
            resultRestart.interactable = resultExit.interactable = next.interactable = !owner.IsExiting;
        }

    }
}
