using Core.Runtime;
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix.KagitKart
{
    /// 原作HUD通知；只订阅本场玩家规则，JS寿命和CSS墙钟分别推进。
    public sealed class KagitKartHudNotice : MonoBehaviour
    {
        [SerializeField] private RectTransform frame;
        [SerializeField] private RectTransform center;
        [SerializeField] private RectTransform placeCard;
        [SerializeField] private TMP_Text placeText;
        [SerializeField] private TMP_Text totalText;
        [SerializeField] private TMP_Text lapText;
        [SerializeField] private TMP_Text clockText;
        [SerializeField] private RectTransform countdown;
        [SerializeField] private KagitKartCountdownCard countdownCard;
        [SerializeField] private RectTransform bannerTemplate;
        [SerializeField] private RectTransform gradeTemplate;
        [SerializeField] private RectTransform wrong;
        [SerializeField] private RectTransform finish;
        [SerializeField] private RectTransform waiting;
        [SerializeField] private RectTransform split;
        [SerializeField] private RectTransform flipTemplate;
        [SerializeField] private RectTransform threat;
        [SerializeField] private Image threatIcon;
        [SerializeField] private Image threatDisc;
        [SerializeField] private Image threatRing;
        [SerializeField] private RectTransform threatEndCap;
        [SerializeField] private KagitKartItemSlot itemSlot;

        private static readonly Color Ink = UIColors.KagitKart.Ink;
        private static readonly Color Paper = UIColors.KagitKart.Paper;
        private static readonly Color Red = UIColors.KagitKart.Accent;
        private static readonly Color Blue = UIColors.KagitKart.Blue;
        private static readonly Color Mustard = UIColors.KagitKart.Mustard;
        private readonly List<Notice> notices = new();
        private KagitKartWorld world;
        private KagitKartSession session;
        private TMP_Text countText;
        private TMP_Text splitText;
        private CanvasGroup countGroup;
        private CanvasGroup splitGroup;
        private TMP_Text waitingText;
        private Vector2 splitAnchor;
        private bool layoutCached;
        private Canvas rootCanvas;
        private float emScale = 1;
        private Vector2 sizedScreen;
        private float sizedCanvasScale = -1;
        private bool sizedTouch;
        private int countdownNumber = 3;
        private float countAngle;
        private bool pendingStartGrade;
        private bool threatActive;
        private bool near;
        private bool countdownReady;
        private bool placeStamped;
        private bool lastReduced;
        private string threatKind;
        private float threatEta, threatTotal;
        private float stampWall = 1, countWall, wrongWall, nearWall, finishWall, finishGradeWall;
        private float goLeft = -1, splitLeft, splitWall = 1, splitFrom;
        private bool splitShown;
        private int shownPlace;

        private enum NoticeKind
        {
            Banner,
            Grade,
            Flip
        }

        private sealed class Notice
        {
            public RectTransform root;
            public CanvasGroup group;
            public NoticeKind kind;
            public float left, wall, awayWall;
            public bool away;
        }

        /// <summary>绑定当前场景会话；重新绑定先取消旧场订阅并清理通知。</summary>
        /// <param name="owner">当前World，空值释放本场表现；不拥有比赛或资源。</param>
        public void Bind(KagitKartWorld owner)
        {
            Clear();
            world = owner;
            session = owner?.Session;
            if (session == null)
            {
                return;
            }
            countText = countdown.GetComponentInChildren<TMP_Text>(true);
            countText.OnPreRenderText += FoldCountdown;
            splitText = split.GetComponentInChildren<TMP_Text>(true);
            countGroup = countdown.GetComponent<CanvasGroup>();
            splitGroup = split.GetComponent<CanvasGroup>();
            waitingText = waiting.GetComponentInChildren<TMP_Text>(true);
            rootCanvas = frame.GetComponentInParent<Canvas>();
            if (!layoutCached)
            {
                splitAnchor = split.anchoredPosition;
                layoutCached = true;
            }
            session.Race.Signaled += OnRace;
            session.Items.Signaled += OnItem;
            session.Player.Signaled += OnVehicle;
            lastReduced = owner.ReducedMotion;
            ApplySizing();
            countdownReady = !owner.IntroPlaying;
            if (countdownReady && session.Race.State == KagitKartRaceState.Countdown)
            {
                ShowCountdown(session.Race.Countdown);
            }
            wrong.gameObject.SetActive(world.PlayerEntry.Progress.WrongWay && !session.Player.Finished);
            center.gameObject.SetActive(!owner.IntroPlaying);
            RefreshNumbers();
        }

        /// <summary>推进原HUD；暂停感知秒数只管JS寿命，墙钟秒数继续CSS动画。</summary>
        /// <param name="dt">暂停时为零的HUD秒数；不自行改规则时钟。</param>
        /// <param name="wallDt">未缩放表现秒数，暂停菜单期间仍推进。</param>
        public void Advance(float dt, float wallDt)
        {
            if (session == null)
            {
                return;
            }
            bool reduced = world.ReducedMotion;
            dt = Mathf.Max(0, dt);
            wallDt = Mathf.Max(0, wallDt);
            if (!countdownReady && !world.IntroPlaying && session.Race.State == KagitKartRaceState.Countdown)
            {
                countdownReady = true;
                ShowCountdown(session.Race.Countdown);
            }
            if (lastReduced && !reduced)
            {
                countWall = wrongWall = nearWall = finishWall = 0;
                if (placeStamped)
                {
                    stampWall = 0;
                }
                foreach (var n in notices)
                {
                    if (n.kind == NoticeKind.Banner)
                    {
                        n.wall = n.awayWall = 0;
                    }
                }
            }
            lastReduced = reduced;
            ApplySizing();
            if (pendingStartGrade)
            {
                pendingStartGrade = false;
                if (session.Race.StartGrade == KagitKartStartGrade.Perfect)
                {
                    Tag("完美起步！", false, 1.7f);
                }
                else if (session.Race.StartGrade == KagitKartStartGrade.Good)
                {
                    Tag("起步不错", true, 1.7f);
                }
            }
            RefreshNumbers();
            center.gameObject.SetActive(!world.IntroPlaying);
            Rect rect = frame.rect;
            center.position = frame.TransformPoint(rect.center + Vector2.up * rect.height * (Screen.height <= 520 ? 0.36f : 0.28f));
            stampWall += wallDt;
            float stamp = !placeStamped || reduced || stampWall >= 0.3f ? 1 : KagitKartHudMotion.Ease(stampWall / 0.3f, new Vector4(0.2f, 1.6f, 0.45f, 1));
            CssRotation(placeCard, Mathf.LerpUnclamped(-9, -3.5f, stamp));
            float placeFont = world.ShowTouchControls ? 4.2f : Screen.height <= 520 ? 5 : 6.2f;
            float outerScale = world.ShowTouchControls ? 0.75f : 1;
            placeCard.localScale = Vector3.one * (emScale * placeFont / 6.2f / outerScale * Mathf.LerpUnclamped(1.25f, 1, stamp));
            countWall += wallDt;
            if (goLeft >= 0)
            {
                goLeft -= dt;
                if (goLeft <= 0)
                {
                    goLeft = -1;
                    countdown.gameObject.SetActive(false);
                }
            }
            if (countdown.gameObject.activeSelf)
            {
                float t = Mathf.Clamp01(countWall / 0.5f);
                float angle = reduced ? 0 : t < 0.6f
                    ? Mathf.LerpUnclamped(-95, 12, KagitKartHudMotion.Ease(t / 0.6f, new Vector4(0.2f, 0.9f, 0.3f, 1.25f)))
                    : Mathf.LerpUnclamped(12, 0, KagitKartHudMotion.Ease((t - 0.6f) / 0.4f, new Vector4(0.2f, 0.9f, 0.3f, 1.25f)));
                // UGUI Overlay没有CSS perspective；仅变形这张纸卡的TMP顶点。
                if (!Mathf.Approximately(countAngle, angle))
                {
                    countAngle = angle;
                    countText.ForceMeshUpdate();
                }
                countGroup.alpha = 1;
                countdownCard.SetFold(angle, countText.fontSize);
            }
            wrongWall += wallDt;
            CssRotation(wrong, reduced ? 0 : 3 * Alternate(wrongWall / 0.8f));
            if (finish.gameObject.activeSelf)
            {
                finishWall += wallDt;
                var checker = (RectTransform)finish.GetChild(0);
                Slam(checker, checker.GetComponent<CanvasGroup>(), finishWall, 0.95f, reduced);
                if (reduced)
                {
                    CssRotation(checker, -3);
                }
                // kk-grade没有kk-slam类，原低动态选择器不取消其CSS动画。
                var grade = (RectTransform)finish.GetChild(1);
                finishGradeWall += wallDt;
                Slam(grade, grade.GetComponent<CanvasGroup>(), finishGradeWall, 0.9f, false);
            }
            for (int i = notices.Count - 1; i >= 0; i--)
            {
                var n = notices[i];
                n.left -= dt;
                n.wall += wallDt;
                if (n.left <= 0)
                {
                    Destroy(n.root.gameObject);
                    notices.RemoveAt(i);
                    continue;
                }
                if (n.kind == NoticeKind.Flip)
                {
                    n.root.localScale = Vector3.one * emScale * (Screen.height <= 520 ? 2f / 3 : 1);
                    n.root.GetChild(1).gameObject.SetActive(Screen.height > 520 && !world.ShowTouchControls);
                    float t = Mathf.Clamp01(n.wall / 1.1f);
                    float scale = t < 0.28f ? KagitKartHudMotion.Ease(t / 0.28f, new Vector4(0.4f, 0, 0.2f, 1))
                        : t < 0.68f ? 1 : 1 - KagitKartHudMotion.Ease((t - 0.68f) / 0.32f, new Vector4(0.4f, 0, 0.2f, 1));
                    if (reduced)
                    {
                        scale = t < 0.28f || t >= 1 ? 0 : 1;
                    }
                    n.root.GetChild(0).localScale = Vector3.one * scale;
                    var label = n.root.GetChild(1).GetComponent<CanvasGroup>();
                    label.alpha = t < 0.15f || t >= 0.78f ? 0 : t < 0.3f ? Ease((t - 0.15f) / 0.15f)
                        : t <= 0.62f ? 1 : 1 - Ease((t - 0.62f) / 0.16f);
                    if (reduced)
                    {
                        label.alpha = t < 0.3f || t >= 0.78f ? 0 : 1;
                    }
                }
                else if (n.kind == NoticeKind.Banner && n.left <= 0.4f)
                {
                    if (!n.away)
                    {
                        n.away = true;
                        n.awayWall = 0;
                    }
                    n.awayWall += wallDt;
                    float away = reduced ? 0 : KagitKartHudMotion.Ease(n.awayWall / 0.35f, new Vector4(0.42f, 0, 1, 1));
                    n.group.alpha = 1 - away;
                    n.root.localScale = Vector3.one;
                    CssRotation(n.root, -3 * away);
                    n.root.GetChild(0).localPosition = Vector3.up * (60 * away);
                }
                else
                {
                    Slam(n.root, n.group, n.wall, n.kind == NoticeKind.Grade ? 0.9f : 0.95f, n.kind != NoticeKind.Grade && reduced);
                }
            }
            if (splitShown && splitLeft > 0)
            {
                splitLeft -= dt;
                if (splitLeft <= 0)
                {
                    splitShown = false;
                    splitFrom = splitGroup.alpha;
                    splitWall = 0;
                }
            }
            splitWall += wallDt;
            float splitT = Ease(Mathf.Clamp01(splitWall / 0.2f));
            splitGroup.alpha = Mathf.Lerp(splitFrom, splitShown ? 1 : 0, splitT);
            split.anchoredPosition = splitAnchor + Vector2.up * (8.8f * (1 - splitGroup.alpha));
            bool wait = session.Race.State == KagitKartRaceState.Finishing && session.Race.WaitLeft > 0;
            if (wait && !waiting.gameObject.activeSelf)
            {
                waiting.SetAsLastSibling();
            }
            waiting.gameObject.SetActive(wait);
            if (wait)
            {
                waitingText.text = $"等待其他车手 · {Math.Ceiling(session.Race.WaitLeft):0} 秒";
            }
            UpdateThreat(dt, wallDt, reduced);
        }

        private void RefreshNumbers()
        {
            var player = session.Player;
            int place = player.Place;
            if (place > 0 && place != shownPlace)
            {
                SetPlace(place, false);
            }
            totalText.gameObject.SetActive(session.Karts.Count > 1);
            totalText.text = "/" + session.Karts.Count;
            lapText.text = $"<size=36%>圈</size> {Mathf.Clamp(world.PlayerEntry.Progress.Lap, 1, session.Race.Laps)}/{session.Race.Laps}";
            double seconds = player.Finished ? world.PlayerEntry.FinishTime
                : world.IntroPlaying || session.Race.State == KagitKartRaceState.Countdown ? 0 : session.RaceTime;
            clockText.text = KagitKartHudMotion.TimeText(seconds);
        }

        private void ApplySizing()
        {
            var screen = new Vector2(Screen.width, Screen.height);
            bool touch = world.ShowTouchControls;
            if (screen == sizedScreen && rootCanvas.scaleFactor == sizedCanvasScale && touch == sizedTouch)
            {
                return;
            }
            sizedScreen = screen;
            sizedCanvasScale = rootCanvas.scaleFactor;
            sizedTouch = touch;
            float em = Mathf.Clamp(Mathf.Min(Screen.width * 0.0104f, Screen.height * 0.0195f), 11, 20);
            emScale = em / (20 * rootCanvas.scaleFactor);
            transform.localScale = Vector3.one * emScale;
            lapText.transform.parent.parent.localScale = Vector3.one * emScale;
            split.localScale = Vector3.one;
            totalText.fontSize = world.ShowTouchControls ? 29.76f : 21.08f;
            bool small = Screen.height <= 520;
            countText.fontSize = countdownNumber > 0 ? small ? 200 : 260 : small ? 130 : 150;
            countdown.sizeDelta = new Vector2(600, countText.fontSize * (countdownNumber > 0 ? 1.23f : 1));
            foreach (var n in notices)
            {
                if (n.kind == NoticeKind.Banner)
                {
                    SizeBanner(n.root);
                }
            }
        }

        private void FoldCountdown(TMP_TextInfo info)
        {
            float sin = Mathf.Sin(countAngle * Mathf.Deg2Rad);
            float cos = Mathf.Cos(countAngle * Mathf.Deg2Rad);
            float offsetY = countText.rectTransform.localPosition.y;
            for (int i = 0; i < info.meshInfo.Length; i++)
            {
                var mesh = info.meshInfo[i];
                for (int vertex = 0; vertex < mesh.vertexCount; vertex++)
                {
                    Vector3 point = mesh.vertices[vertex];
                    float y = point.y + offsetY;
                    // perspective(40em)，CSS向下的y换为UGUI向上的y；原点50% 88%。
                    float perspective = 1 + y * sin / (countText.fontSize * 40);
                    point.x /= perspective;
                    point.y = y * cos / perspective - offsetY;
                    mesh.vertices[vertex] = point;
                }
            }
        }

        private void SetPlace(int value, bool stamp)
        {
            shownPlace = value;
            placeText.text = value + ".";
            placeText.color = value == 1 ? UIColors.KagitKart.FirstPlace : value == 2 ? UIColors.KagitKart.SecondPlace : value == 3 ? UIColors.KagitKart.ThirdPlace : Ink;
            placeStamped = stamp && !world.ReducedMotion;
            if (stamp && !world.ReducedMotion)
            {
                stampWall = 0;
            }
        }

        private void ShowCountdown(int number)
        {
            countdown.gameObject.SetActive(true);
            countdown.SetAsFirstSibling();
            countText.text = number > 0 ? number.ToString() : "出发！";
            countText.gameObject.SetActive(number <= 0);
            countdownCard.ShowNumber(number);
            countdownNumber = number;
            bool small = Screen.height <= 520;
            countText.fontSize = number > 0 ? small ? 200 : 260 : small ? 130 : 150;
            countdown.sizeDelta = new Vector2(600, countText.fontSize * (number > 0 ? 1.23f : 1));
            countText.color = Paper;
            countText.outlineColor = number == 2 ? Mustard : number == 1 ? Blue : Red;
            countText.outlineWidth = 0.18f;
            countText.fontMaterial.EnableKeyword("OUTLINE_ON");
            countWall = 0;
            countAngle = world.ReducedMotion ? 0 : -95;
            countText.ForceMeshUpdate();
            countdownCard.SetFold(countAngle, countText.fontSize);
            goLeft = number > 0 ? -1 : 0.9f;
        }

        private void OnRace(KagitKartRaceEvent signal)
        {
            // 取消订阅不会撤回本次事件已经复制的回调列表。
            if (session == null) return;
            if (signal.Kind == "countdown")
            {
                ShowCountdown(signal.Number);
                return;
            }
            if (signal.Kind == "raceStart")
            {
                pendingStartGrade = true;
                return;
            }
            if (signal.Kart != session.Player)
            {
                return;
            }
            switch (signal.Kind)
            {
                case "place":
                    if (signal.Number != shownPlace)
                    {
                        SetPlace(signal.Number, true);
                    }
                    break;
                case "lap":
                    if (!world.ReducedMotion)
                    {
                        Add(flipTemplate, NoticeKind.Flip, signal.Number + " 圈", 1.2f);
                    }
                    break;
                case "finalLap":
                    Add(bannerTemplate, NoticeKind.Banner, "最后一圈", 2.3f);
                    break;
                case "wrongWay":
                    if (signal.Number != 0 && !wrong.gameObject.activeSelf)
                    {
                        wrongWall = 0;
                        wrong.SetAsLastSibling();
                    }
                    wrong.gameObject.SetActive(signal.Number != 0);
                    break;
                case "finish":
                    wrong.gameObject.SetActive(false);
                    finish.gameObject.SetActive(true);
                    finish.SetAsLastSibling();
                    finishWall = finishGradeWall = 0;
                    var result = finish.GetChild(1);
                    result.GetComponentInChildren<TMP_Text>(true).text = "第 " + signal.Number + " 名";
                    result.GetComponent<Image>().color = signal.Number <= 3 ? Ink : Paper;
                    result.GetComponentInChildren<TMP_Text>(true).color = signal.Number <= 3 ? Mustard : Ink;
                    break;
                case "checkpoint":
                    if (signal.Split.HasValue)
                    {
                        splitText.text = KagitKartHudMotion.SplitText(signal.Split.Value);
                        split.GetComponent<Image>().color = signal.Split.Value <= 0 ? Blue : Red;
                        if (!splitShown)
                        {
                            splitFrom = splitGroup.alpha;
                            splitWall = 0;
                        }
                        splitShown = true;
                        splitLeft = 2.6f;
                    }
                    break;
            }
        }

        private void OnVehicle(KagitKartVehicle kart, KagitKartVehicleSignal signal)
        {
            if (session == null) return;
            if (signal == KagitKartVehicleSignal.Hit && kart.SpinCause == "burnout")
            {
                Tag("起步熄火了", true, 1.5f);
            }
        }

        private void OnItem(KagitKartItemEvent signal)
        {
            if (session == null) return;
            if (signal.Kind != "threat" || signal.Kart != session.Player)
            {
                return;
            }
            if (!threatActive || threatKind != signal.Item)
            {
                threatIcon.sprite = itemSlot.Icon(signal.Item);
                threatIcon.enabled = threatIcon.sprite != null;
            }
            threatKind = signal.Item;
            threatEta = signal.Eta;
            threatTotal = Mathf.Max(signal.Eta, threatTotal, 1.5f);
            threatActive = true;
            threat.gameObject.SetActive(true);
        }

        private void UpdateThreat(float dt, float wallDt, bool reduced)
        {
            if (!threatActive)
            {
                return;
            }
            threatEta -= dt;
            if (threatEta < -0.35f)
            {
                threatActive = false;
                threatTotal = 0;
                near = false;
                threat.gameObject.SetActive(false);
                return;
            }
            Vector3 relative = Vector3.back;
            float heading = 0;
            foreach (var projectile in session.Items.Projectiles)
            {
                if (projectile != null && projectile.Target == session.Player && projectile.Kind == threatKind)
                {
                    relative = projectile.Position - session.Player.Position;
                    heading = session.Player.Heading;
                    break;
                }
            }
            float canvasScale = rootCanvas.scaleFactor;
            Vector2 point = ThreatPoint(frame.rect.size * canvasScale, relative, heading, 70) / canvasScale;
            threat.position = frame.TransformPoint(frame.rect.center + point);
            // 原SVG dasharray保留百分比的一位小数。
            threatRing.fillAmount = (float)Math.Round(Mathf.Clamp01(threatEta / threatTotal) * 100, 1, MidpointRounding.AwayFromZero) / 100;
            float ringAngle = threatRing.fillAmount * Mathf.PI * 2;
            threatEndCap.anchoredPosition = new Vector2(Mathf.Sin(ringAngle), Mathf.Cos(ringAngle)) * 61.64f;
            bool isNear = threatEta <= 0.6f;
            if (isNear != near)
            {
                near = isNear;
                nearWall = 0;
            }
            nearWall += wallDt;
            threatDisc.color = near ? UIColors.KagitKart.NearThreat : Paper;
            threat.localScale = Vector3.one * (reduced || !near ? 1 : 1 + 0.12f * Alternate(nearWall / 0.25f));
        }

        private static Vector2 ThreatPoint(Vector2 size, Vector3 relative, float heading, float margin)
        {
            double fwd = relative.x * Math.Sin(heading) + relative.z * Math.Cos(heading);
            double right = relative.x * -Math.Cos(heading) + relative.z * Math.Sin(heading);
            double angle = Math.Atan2(right, fwd);
            double x = size.x / 2 + Math.Sin(angle) * (size.x / 2 - margin);
            double y = size.y / 2 - Math.Cos(angle) * (size.y / 2 - margin);
            return new Vector2((float)(Math.Round(x, 1, MidpointRounding.AwayFromZero) - size.x / 2),
                (float)(size.y / 2 - Math.Round(y, 1, MidpointRounding.AwayFromZero)));
        }

        private void Tag(string text, bool meh, float life)
        {
            var n = Add(gradeTemplate, NoticeKind.Grade, text, life);
            n.root.GetComponent<Image>().color = meh ? Paper : Ink;
            n.root.GetComponentInChildren<TMP_Text>(true).color = meh ? Ink : Mustard;
        }

        private Notice Add(RectTransform template, NoticeKind kind, string text, float life)
        {
            var root = Instantiate(template, template.parent);
            root.gameObject.SetActive(true);
            root.SetAsLastSibling();
            root.GetComponentInChildren<TMP_Text>(true).text = text;
            var n = new Notice { root = root, group = root.GetComponent<CanvasGroup>(), kind = kind, left = life };
            if (kind == NoticeKind.Banner)
            {
                SizeBanner(root);
            }
            notices.Add(n);
            return n;
        }

        private static void SizeBanner(RectTransform root)
        {
            float scale = Screen.height <= 520 ? 2.6f / 3.4f : 1;
            root.sizeDelta = new Vector2(540, 116) * scale;
            root.GetChild(0).localScale = Vector3.one * scale;
        }

        private static float Ease(float value) => KagitKartHudMotion.Ease(value, new Vector4(0.25f, 0.1f, 0.25f, 1));

        private static float Alternate(float value)
        {
            float t = Mathf.PingPong(value, 1);
            return KagitKartHudMotion.Ease(t, new Vector4(0.42f, 0, 0.58f, 1));
        }

        private static void CssRotation(RectTransform root, float degrees) => root.localRotation = Quaternion.Euler(0, 0, -degrees);

        private static void Slam(RectTransform root, CanvasGroup group, float wall, float duration, bool reduced)
        {
            float t = Mathf.Clamp01(wall / duration);
            float scale;
            float angle = -3;
            float alpha = 1;
            var curve = new Vector4(0.2f, 0.9f, 0.3f, 1);
            if (reduced)
            {
                scale = 1;
                angle = 0;
            }
            else if (t < 0.18f)
            {
                float u = KagitKartHudMotion.Ease(t / 0.18f, curve);
                scale = Mathf.LerpUnclamped(3.2f, 0.94f, u);
                angle = Mathf.LerpUnclamped(-12, -3, u);
                alpha = u;
            }
            else if (t < 0.26f)
            {
                scale = Mathf.LerpUnclamped(0.94f, 1.04f, KagitKartHudMotion.Ease((t - 0.18f) / 0.08f, curve));
            }
            else
            {
                scale = Mathf.LerpUnclamped(1.04f, 1, KagitKartHudMotion.Ease((t - 0.26f) / 0.08f, curve));
            }
            root.localScale = Vector3.one * scale;
            CssRotation(root, angle);
            group.alpha = alpha;
        }

        /// 取消本场订阅并移除通知；不会清理其它HUD模块。
        public void Clear()
        {
            if (countText != null)
            {
                countText.OnPreRenderText -= FoldCountdown;
            }
            if (session != null)
            {
                session.Race.Signaled -= OnRace;
                session.Items.Signaled -= OnItem;
                session.Player.Signaled -= OnVehicle;
            }
            session = null;
            world = null;
            foreach (var n in notices)
            {
                Destroy(n.root.gameObject);
            }
            notices.Clear();
            foreach (var root in new[] { countdown, wrong, finish, waiting, threat })
            {
                if (root != null)
                {
                    root.gameObject.SetActive(false);
                }
            }
            if (split != null)
            {
                split.GetComponent<CanvasGroup>().alpha = 0;
                if (layoutCached)
                {
                    split.anchoredPosition = splitAnchor;
                }
            }
            splitLeft = splitFrom = 0;
            splitWall = 1;
            splitShown = pendingStartGrade = threatActive = near = false;
            threatTotal = 0;
            shownPlace = 0;
            countdownReady = placeStamped = false;
            sizedCanvasScale = -1;
            stampWall = 1;
            goLeft = -1;
        }

        private void OnDestroy() => Clear();
    }
}
