using System;
using System.Collections.Generic;
using System.Linq;
using Hotfix.JinxCasino.Adapters;
using Hotfix.JinxCasino.Rules;
using TMPro;
using UnityEngine;

namespace Hotfix.Editor.JinxCasino
{
    /// 样板场景的专属表现引用装配；运行时不搜索或生成模型、Canvas。
    public static class JinxCasinoS1PresentationBuilder
    {
        /// <summary>为三种样板机台保存牌面、轮轴、灯具及动态铭牌引用。</summary>
        /// <param name="station">已装配操作目标的具体机台。</param>
        /// <param name="visual">该机台的独立模型根。</param>
        /// <param name="font">Demo专属中文字库。</param>
        /// <param name="palette">按名称索引的独立样板材质。</param>
        /// <param name="savedRulesLabel">可选的专属规则铭牌。</param>
        /// <returns>保存于机台上的对应表现组件。</returns>
        public static JinxCasinoS1Presentation ConfigureS1Presentation(JinxCasinoStation station, Transform visual,
            TMP_FontAsset font, Dictionary<string, Material> palette, TMP_Text savedRulesLabel = null)
        {
            if (station == null || !station.HasTableInteraction || visual == null || font == null)
                throw new ArgumentException("先装配具体机台、保存目标、正式视觉和中文字体。");
            var plaque = Node(visual, "StatePlaque");
            // 此字库3D单行高度约.041米；低于行高时Truncate会裁掉整行。
            var amount = Label(plaque, "Amount", font, new Vector3(0, .023f, .047f), .52f, .046f, .35f);
            var result = Label(plaque, "Result", font, new Vector3(0, -.023f, .047f), .52f, .046f, .35f);
            if (savedRulesLabel == null) savedRulesLabel = EnsureRulesPlacard(station, font, palette);
            switch (station.Game)
            {
                case CasinoGameKind.Slots:
                    var slots = GetOrAdd<JinxCasinoS1SlotsPresentation>(station.gameObject);
                    slots.Configure(station.StationId, Enumerable.Range(0, 3).Select(index => Node(visual, "Slots.Reel" + index)).ToArray(),
                        Node(visual, "Slots.LeverPivot"), Enumerable.Range(0, 8).Select(index => Node(visual, "Slots.PayoutChip" + index)).ToArray(),
                        amount, savedRulesLabel, result); return slots;
                case CasinoGameKind.Blackjack:
                    var cardFaces = Enumerable.Range(1, 13).Select(rank => Face(Node(visual, "Blackjack.Rank" + rank))).ToArray();
                    var players = Enumerable.Range(0, 12).Select(index => Card(Node(visual, "Blackjack.PlayerCard" + index))).ToArray();
                    var dealers = Enumerable.Range(0, 12).Select(index => Card(Node(visual, "Blackjack.DealerCard" + index))).ToArray();
                    var cards = GetOrAdd<JinxCasinoS1BlackjackPresentation>(station.gameObject);
                    cards.Configure(station.StationId, players, dealers, cardFaces, Face(players[0].Pose), Card(Node(visual, "Blackjack.DrawCard")),
                        Node(visual, "Blackjack.DealOrigin"), Node(visual, "Blackjack.DealerArmPivot"),
                        Label(Node(visual, "Blackjack.PlayerTotal"), "Total", font, new Vector3(0, 0, .058f), .24f, .08f, .52f),
                        Label(Node(visual, "Blackjack.DealerTotal"), "Total", font, new Vector3(0, 0, .058f), .24f, .08f, .52f),
                        amount, savedRulesLabel, result); return cards;
                case CasinoGameKind.CooperativeLevers:
                    var bindings = Enumerable.Range(0, 2).Select(index => new CasinoS1LeverVisual
                    {
                        Lever = Node(visual, "CooperativeLevers.Lever" + index), Needle = Node(visual, "CooperativeLevers.TimingNeedle" + index),
                        Window = Node(visual, "CooperativeLevers.Window" + index).gameObject,
                        HelpWindow = Node(visual, "CooperativeLevers.HelpWindow" + index).gameObject,
                        WindowRenderer = SingleRenderer(Node(visual, "CooperativeLevers.Window" + index)),
                        HelpWindowRenderer = SingleRenderer(Node(visual, "CooperativeLevers.HelpWindow" + index)),
                        Dark = palette["InkBlue"], Open = palette["Mint"], Pulled = palette["CopperGold"],
                        WindowStartMilliseconds = 200 + index * 250, WindowEndMilliseconds = 400 + index * 250
                    }).ToArray();
                    var levers = GetOrAdd<JinxCasinoS1LeversPresentation>(station.gameObject);
                    levers.Configure(station.StationId, bindings, Node(visual, "CooperativeLevers.HelpWrench"),
                        SingleRenderer(Node(visual, "CooperativeLevers.SyncLamp")), palette["InkBlue"], palette["Mint"], amount, savedRulesLabel, result); return levers;
                default: throw new ArgumentOutOfRangeException(nameof(station), "S1只绑定三台专属模型。");
            }
        }
        /// <summary>创建独立的机台赔率夹板；已有夹板保留位置及人工外观，只返回正文引用。</summary>
        /// <param name="station">具体机台及保存的聚焦挂点。</param>
        /// <param name="font">Demo专属字库。</param>
        /// <param name="palette">夹板使用的独立样板材质。</param>
        /// <param name="applyLayout">显式更新夹板位置和字号；false保留已有夹板的人工布局。</param>
        /// <returns>用于完整规则与当前加成的正文。</returns>
        public static TMP_Text EnsureRulesPlacard(JinxCasinoStation station, TMP_FontAsset font, Dictionary<string, Material> palette, bool applyLayout = false)
        {
            var existing = station.transform.Find("RulesPlacard");
            if (existing != null)
            {
                var savedRules = existing.Find("Rules").GetComponent<TMP_Text>();
                if (existing.Find("Support") == null)
                    BoardPart(existing, "Support", new Vector3(0, -.52f, -.035f), new Vector3(.07f, .34f, .055f), palette["CopperGold"], true);
                if (applyLayout) PositionRulesPlacard(station, existing, savedRules);
                return savedRules;
            }
            var board = new GameObject("RulesPlacard").transform;
            board.SetParent(station.transform, false);
            BoardPart(board, "Frame", Vector3.zero, new Vector3(.78f, .78f, .045f), palette["CopperGold"], true);
            BoardPart(board, "Face", new Vector3(0, 0, .029f), new Vector3(.73f, .73f, .016f), palette["InkBlue"], false);
            BoardPart(board, "Clip", new Vector3(0, .376f, .051f), new Vector3(.19f, .07f, .038f), palette["Mint"], false);
            BoardPart(board, "Support", new Vector3(0, -.52f, -.035f), new Vector3(.07f, .34f, .055f), palette["CopperGold"], true);
            var title = Label(board, "Title", font, new Vector3(0, .282f, .043f), .66f, .07f, .42f);
            title.text = station.Game == CasinoGameKind.Slots ? "水果返还表" : station.Game == CasinoGameKind.Blackjack ? "牌桌小抄" : "合拍须知";
            var rules = Label(board, "Rules", font, new Vector3(0, -.041f, .043f), .65f, .53f, .36f);
            rules.alignment = TextAlignmentOptions.TopLeft; rules.textWrappingMode = TextWrappingModes.Normal;
            rules.text = "投入前请看返还规则";
            PositionRulesPlacard(station, board, rules);
            return rules;
        }

        private static void PositionRulesPlacard(JinxCasinoStation station, Transform board, TMP_Text rules)
        {
            board.localPosition = station.Game == CasinoGameKind.Blackjack ? new Vector3(1.2f, 1.55f, -.25f) :
                station.Game == CasinoGameKind.Slots ? new Vector3(1.25f, 1.7f, .7f) : new Vector3(1.25f, 1.68f, 0);
            board.rotation = Quaternion.LookRotation(station.FocusPose.position - board.position, Vector3.up);
            rules.enableAutoSizing = true; rules.fontSizeMin = .36f; rules.fontSizeMax = .5f; rules.fontSize = .5f;
        }

        private static void BoardPart(Transform parent, string name, Vector3 position, Vector3 size, Material material, bool collision)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube); part.name = name;
            part.transform.SetParent(parent, false); part.transform.localPosition = position; part.transform.localScale = size;
            part.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
        }

        private static CasinoS1CardSlot Card(Transform pose) => new CasinoS1CardSlot
        { Pose = pose, Filter = pose.GetComponentsInChildren<MeshFilter>(true).Single(), Renderer = SingleRenderer(pose) };
        private static CasinoS1CardFace Face(Transform pose) => new CasinoS1CardFace
        { Mesh = pose.GetComponentsInChildren<MeshFilter>(true).Single().sharedMesh, Materials = SingleRenderer(pose).sharedMaterials };
        private static Renderer SingleRenderer(Transform pose) => pose.GetComponentsInChildren<Renderer>(true).Single();
        private static T GetOrAdd<T>(GameObject root) where T : Component
        { var value = root.GetComponent<T>(); if (value == null) value = root.AddComponent<T>(); return value; }
        private static Transform Node(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).Single(value => value.name == name ||
            name == "StatePlaque" && System.Text.RegularExpressions.Regex.IsMatch(value.name, @"^StatePlaque\.\d+$"));
        private static TMP_Text Label(Transform parent, string name, TMP_FontAsset font, Vector3 position, float width, float height, float size)
        {
            var existing = parent.Find(name); var root = existing != null ? existing.gameObject : new GameObject(name);
            root.transform.SetParent(parent, false); root.transform.localPosition = position;
            // TMP可见面为-Z；保存模型铭牌可见面+Z，无须第二Camera或WorldSpace Canvas。
            root.transform.localRotation = Quaternion.Euler(0, 180, 0);
            var label = GetOrAdd<TextMeshPro>(root);
            label.font = font; label.fontSize = size; label.color = new Color(1, .95f, .78f);
            label.alignment = TextAlignmentOptions.Center; label.text = string.Empty; label.rectTransform.sizeDelta = new Vector2(width, height);
            label.textWrappingMode = TextWrappingModes.NoWrap; label.overflowMode = TextOverflowModes.Truncate;
            return label;
        }
    }
}
