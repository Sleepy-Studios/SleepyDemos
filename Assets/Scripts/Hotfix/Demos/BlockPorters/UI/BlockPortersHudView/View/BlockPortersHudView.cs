using Cysharp.Threading.Tasks;
using Core.Runtime;
using Hotfix.BlockPorters;
using Core.Runtime.Inputs;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix
{
    /// HUD读取搬运状态，负责布局和队列动画。
    [Module("BlockPorters")]
    [UIBind("BlockPortersHudView")]
    public sealed partial class BlockPortersHudView : View
    {
        private RectTransform content;
        private BlockPortersUiStyle style;
        private Image[] queueFaces;
        private Image[] previewFaces;
        private TextMeshProUGUI title;
        private TextMeshProUGUI progress;
        private Image progressFill;
        private TextMeshProUGUI slots;
        private TextMeshProUGUI hint;
        private Button[] columns;
        private TextMeshProUGUI[] columnLabels;
        private Image[] previews;
        private TextMeshProUGUI[] previewLabels;
        private TextMeshProUGUI[] previewColorLabels;
        private Image[] taskSlots;
        private TextMeshProUGUI[] taskLabels;
        private Button pause;
        private Button[] extraButtons;
        private TextMeshProUGUI[] extraLabels;
        private Image[] extraIcons;
        private TextMeshProUGUI levelName;
        private TextMeshProUGUI[] queueColorLabels;
        private Image[] taskBadges;
        private BlockPortersData owner;
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
        private UIProgressBar progressBar;
        private float displayedProgress;
        private float targetProgress;
        private bool refreshPending;
        private static readonly Color Empty = new(.91f, .87f, .81f);

        protected override void OnGameObjectInitialize()
        {
            InitializeReferences();
            owner = GlobalData.Get<BlockPortersData>();
            style = owner.Style;
            BindData<BlockPortersData>(OnData);
            BindUpdate(LateUpdate, PlayerLoopTiming.LastPostLateUpdate);

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
            feedback = gameObject.GetComponentsInChildren<BlockPortersButtonFeedback>(true);
            pause.onClick.AddListener(ToggleSettings);
            var menu = gameObject.GetComponent<UIMenuScope>();
            if (menu != null) { menu.Canceled += ToggleSettings; AddBinding(() => { if (menu != null) menu.Canceled -= ToggleSettings; }); }
            for (int side = 0; side < 2; side++)
            {
                int index = side;
                // 开放后仍使用完整的凹槽颜色；按压由统一反馈组件处理。
                extraButtons[side].transition = Selectable.Transition.None;
                extraButtons[side].onClick.AddListener(() => GlobalData.Dispatch(new BlockPortersUnlockSlotAction(index)));
            }
            for (int i = 0; i < previews.Length; i++) previewPositions[i] = previews[i].rectTransform.anchoredPosition;
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





        private void RequestRefresh() => refreshPending = true;

        private void DispatchColumn(int column)
        {
            if (owner == null || !columns[column].interactable || advanceAges[column] >= 0 ||
                owner.IsPaused || owner.IsExiting || owner.IsRewardPending) return;
            int count = owner.Session.Teams.Count;
            GlobalData.Dispatch(new BlockPortersDispatchAction(column));
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

        private void ToggleSettings() { ClearAdvances(); GlobalData.Dispatch(new BlockPortersOpenSettingsAction()); }

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
            Vector2 size = ((RectTransform)transform).rect.size;
            if (size == lastSize && lastSafeArea == Screen.safeArea) return;
            lastSize = size; lastSafeArea = Screen.safeArea;
            var screenLayout = BlockPortersScreenLayout.Calculate(Screen.width, Screen.height, Screen.safeArea);
            float scale = screenLayout.Scale * size.x / Mathf.Max(1, Screen.width);
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
            }
            pause.interactable = !owner.IsExiting && session.Status == BlockPortersStatus.Playing;
        }

        private void OnData(BlockPortersData value)
        {
            owner = value;
            if (shownSession != value.Session) Refresh(); else RequestRefresh();
        }
        private void InitializeReferences()
        {
            content = RectTransform_PortraitContent;
            queueFaces = new UnityEngine.UI.Image[] { Image_TeamFace0, Image_TeamFace1, Image_TeamFace2, Image_TeamFace3, Image_TeamFace4 };
            previewFaces = new UnityEngine.UI.Image[] { Image_UpcomingFace0, Image_UpcomingFace1, Image_UpcomingFace2, Image_UpcomingFace3, Image_UpcomingFace4, Image_UpcomingFace5, Image_UpcomingFace6, Image_UpcomingFace7, Image_UpcomingFace8, Image_UpcomingFace9, Image_UpcomingFace10, Image_UpcomingFace11, Image_UpcomingFace12, Image_UpcomingFace13, Image_UpcomingFace14 };
            title = TextMeshProUGUI_Title;
            progress = TextMeshProUGUI_Progress;
            progressFill = Image_ProgressFill;
            slots = TextMeshProUGUI_SlotsTitle;
            hint = TextMeshProUGUI_Hint;
            columns = new UnityEngine.UI.Button[] { Button_Queue0, Button_Queue1, Button_Queue2, Button_Queue3, Button_Queue4 };
            columnLabels = new TMPro.TextMeshProUGUI[] { TextMeshProUGUI_Queue0Label, TextMeshProUGUI_Queue1Label, TextMeshProUGUI_Queue2Label, TextMeshProUGUI_Queue3Label, TextMeshProUGUI_Queue4Label };
            previews = new UnityEngine.UI.Image[] { Image_Preview0, Image_Preview1, Image_Preview2, Image_Preview3, Image_Preview4, Image_Preview5, Image_Preview6, Image_Preview7, Image_Preview8, Image_Preview9, Image_Preview10, Image_Preview11, Image_Preview12, Image_Preview13, Image_Preview14 };
            previewLabels = new TMPro.TextMeshProUGUI[] { TextMeshProUGUI_PreviewLabel0, TextMeshProUGUI_PreviewLabel1, TextMeshProUGUI_PreviewLabel2, TextMeshProUGUI_PreviewLabel3, TextMeshProUGUI_PreviewLabel4, TextMeshProUGUI_PreviewLabel5, TextMeshProUGUI_PreviewLabel6, TextMeshProUGUI_PreviewLabel7, TextMeshProUGUI_PreviewLabel8, TextMeshProUGUI_PreviewLabel9, TextMeshProUGUI_PreviewLabel10, TextMeshProUGUI_PreviewLabel11, TextMeshProUGUI_PreviewLabel12, TextMeshProUGUI_PreviewLabel13, TextMeshProUGUI_PreviewLabel14 };
            previewColorLabels = new TMPro.TextMeshProUGUI[] { TextMeshProUGUI_UpcomingColorLabel0, TextMeshProUGUI_UpcomingColorLabel1, TextMeshProUGUI_UpcomingColorLabel2, TextMeshProUGUI_UpcomingColorLabel3, TextMeshProUGUI_UpcomingColorLabel4, TextMeshProUGUI_UpcomingColorLabel5, TextMeshProUGUI_UpcomingColorLabel6, TextMeshProUGUI_UpcomingColorLabel7, TextMeshProUGUI_UpcomingColorLabel8, TextMeshProUGUI_UpcomingColorLabel9, TextMeshProUGUI_UpcomingColorLabel10, TextMeshProUGUI_UpcomingColorLabel11, TextMeshProUGUI_UpcomingColorLabel12, TextMeshProUGUI_UpcomingColorLabel13, TextMeshProUGUI_UpcomingColorLabel14 };
            taskSlots = new UnityEngine.UI.Image[] { Image_TaskSlot0, Image_TaskSlot1, Image_TaskSlot2, Image_TaskSlot3, Image_TaskSlot4, Image_TaskSlot5, Image_TaskSlot6 };
            taskLabels = new TMPro.TextMeshProUGUI[] { TextMeshProUGUI_TaskLabel0, TextMeshProUGUI_TaskLabel1, TextMeshProUGUI_TaskLabel2, TextMeshProUGUI_TaskLabel3, TextMeshProUGUI_TaskLabel4, TextMeshProUGUI_TaskLabel5, TextMeshProUGUI_TaskLabel6 };
            pause = Button_Pause;
            extraButtons = new UnityEngine.UI.Button[] { Button_TaskSlot5, Button_TaskSlot6 };
            extraLabels = new TMPro.TextMeshProUGUI[] { TextMeshProUGUI_ExtraSlotLabel0, TextMeshProUGUI_ExtraSlotLabel1 };
            extraIcons = new UnityEngine.UI.Image[] { Image_ExtraSlotIcon0, Image_ExtraSlotIcon1 };
            levelName = TextMeshProUGUI_Subtitle;
            queueColorLabels = new TMPro.TextMeshProUGUI[] { TextMeshProUGUI_TeamColorLabel0, TextMeshProUGUI_TeamColorLabel1, TextMeshProUGUI_TeamColorLabel2, TextMeshProUGUI_TeamColorLabel3, TextMeshProUGUI_TeamColorLabel4 };
            taskBadges = new UnityEngine.UI.Image[] { Image_TaskBadge0, Image_TaskBadge1, Image_TaskBadge2, Image_TaskBadge3, Image_TaskBadge4, Image_TaskBadge5, Image_TaskBadge6 };
        }
    }
}
