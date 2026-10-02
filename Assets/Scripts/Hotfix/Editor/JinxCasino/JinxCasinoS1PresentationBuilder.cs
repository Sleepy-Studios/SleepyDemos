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
