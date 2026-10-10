namespace Hotfix
{
    using Core.Runtime;
    using System;
    using Hotfix.KagitKart;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.UI;

    [Module("KagitKart")]
    [UIBind("KagitKartTrackView")]
    public partial class KagitKartTrackView : View
    {
        private KagitKartWorld world;
        private Button[] choices;
        private KagitKartChoiceCard[] cards;
        private Action[] focused;
        private Sprite[] sketches;
        private int preview;

        protected override void OnGameObjectInitialize()
        {
            choices = new[] { Button_Track0, Button_Track1, Button_Track2, Button_Track3 };
            cards = new[] { KagitKartChoiceCard_Track0, KagitKartChoiceCard_Track1, KagitKartChoiceCard_Track2, KagitKartChoiceCard_Track3 };
            sketches = new[] { Image_Sketch0.sprite, Image_Sketch1.sprite, Image_Sketch2.sprite, Image_Sketch3.sprite };
            focused = new Action[cards.Length];
            for (int i = 0; i < choices.Length; i++)
            {
                int index = i;
                focused[i] = () => Preview(index);
                cards[i].Focused += focused[i];
                choices[i].onClick.AddListener(() => Choose(index));
            }
            Button_Back.onClick.AddListener(Back);
            Button_Hub.onClick.AddListener(() => world?.Dispatch(KagitKartCommand.Hub));
            UIMenuScope_Viewport.Canceled += Back;
            BindData<KagitKartData>(_ => Render());
        }

        /// <summary>显示前绑定章节与计时记录来源，悬停预览不提交赛道。</summary>
        /// <param name="owner">本 Demo 场景宿主。</param>
        public void SetData(KagitKartWorld owner)
        {
            world = owner;
            preview = 0;
            for (int i = 0; i < KagitKartCatalog.Tracks.Count; i++)
            {
                if (KagitKartCatalog.Tracks[i] == world.TrackId)
                {
                    preview = i;
                }
            }
            var safe = Screen.safeArea;
            RectTransform_Viewport.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            RectTransform_Viewport.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            KagitKartMenuPaperMotion_Viewport.Play(0);
            Preview(preview);
        }

        protected override void OnShow() => EventSystem.current?.SetSelectedGameObject(choices[preview].gameObject);

        private void Preview(int index)
        {
            if (world == null)
            {
                return;
            }
            preview = index;
            string id = KagitKartCatalog.Tracks[index];
            GlobalData.Dispatch(new KagitKartAction(world, KagitKartCommand.PreviewTrack, id: id));
            TextMeshProUGUI_Chapter.text = "第" + (index + 1) + "章";
            TextMeshProUGUI_TrackName.text = KagitKartCatalog.TrackName(id);
            TextMeshProUGUI_Description.text = KagitKartCatalog.TrackDescription(world.TrackId);
            Image_Sketch.sprite = sketches[index];
            TextMeshProUGUI_Record.text = "";
            if (world.Mode == KagitKartRaceMode.TimeTrial)
            {
                var best = world.LoadRecord(id);
                TextMeshProUGUI_Record.text = best == null ? "最佳成绩读取失败，本场仍可比赛。"
                    : "全程最佳  " + (best.bestRace.HasValue ? KagitKartHudView.TimeText(best.bestRace.Value) : "暂无记录")
                    + "\n单圈最佳  " + (best.bestLap.HasValue ? KagitKartHudView.TimeText(best.bestLap.Value) : "暂无记录");
            }
            Render();
        }

        private void Choose(int index)
        {
            if (world != null)
            {
                GlobalData.Dispatch(new KagitKartAction(world, KagitKartCommand.ChooseTrack, id: KagitKartCatalog.Tracks[index]));
            }
        }

        private void Render()
        {
            if (world == null)
            {
                return;
            }
            foreach (var choice in choices)
            {
                choice.interactable = !world.Busy;
            }
            Button_Back.interactable = Button_Hub.interactable = !world.Busy;
            TextMeshProUGUI_Status.text = world.Feedback ?? (world.SpeedClass + "g  ·  " + KagitKartCatalog.CharacterName(world.CharacterId)
                + "  ·  " + (world.Mode == KagitKartRaceMode.TimeTrial ? "计时幽灵赛" : "单场比赛"));
        }

        private void Back() => world?.Dispatch(KagitKartCommand.Characters);

        protected override void OnHide() => world = null;

        protected override void OnDestroy()
        {
            UIMenuScope_Viewport.Canceled -= Back;
            for (int i = 0; i < cards.Length; i++)
            {
                cards[i].Focused -= focused[i];
            }
            world = null;
        }
    }
}
