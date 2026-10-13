namespace Hotfix
{
    using Core.Runtime;
    using System;
    using Hotfix.KagitKart;
    using UnityEngine;
    using UnityEngine.EventSystems;
    using UnityEngine.UI;

    [Module("KagitKart")]
    [UIBind("KagitKartCharacterView")]
    public partial class KagitKartCharacterView : View
    {
        private KagitKartWorld world;
        private Button[] choices;
        private KagitKartChoiceCard[] cards;
        private Action[] focused;
        private Image[][] ticks;
        private int preview;

        protected override void OnGameObjectInitialize()
        {
            choices = new[] { Button_Character0, Button_Character1, Button_Character2, Button_Character3,
                Button_Character4, Button_Character5, Button_Character6, Button_Character7 };
            cards = new[] { KagitKartChoiceCard_Character0, KagitKartChoiceCard_Character1, KagitKartChoiceCard_Character2, KagitKartChoiceCard_Character3,
                KagitKartChoiceCard_Character4, KagitKartChoiceCard_Character5, KagitKartChoiceCard_Character6, KagitKartChoiceCard_Character7 };
            focused = new Action[cards.Length];
            for (int i = 0; i < choices.Length; i++)
            {
                int index = i;
                focused[i] = () => Preview(index);
                cards[i].Focused += focused[i];
                choices[i].onClick.AddListener(() => Choose(index));
            }
            var rows = new[] { RectTransform_SpeedTicks, RectTransform_AccelTicks, RectTransform_HandlingTicks, RectTransform_WeightTicks, RectTransform_OffroadTicks };
            ticks = new Image[rows.Length][];
            for (int i = 0; i < rows.Length; i++)
            {
                ticks[i] = rows[i].GetComponentsInChildren<Image>();
            }
            Button_Back.onClick.AddListener(Back);
            Button_Hub.onClick.AddListener(() => world?.Dispatch(KagitKartCommand.Hub));
            UIMenuScope_Viewport.Canceled += Back;
            BindData<KagitKartData>(_ => Render());
        }

        /// <summary>显示前交付原作角色与属性，焦点预览不修改已确认角色。</summary>
        /// <param name="owner">本 Demo 场景宿主。</param>
        public void SetData(KagitKartWorld owner)
        {
            world = owner;
            preview = 0;
            for (int i = 0; i < owner.Roster.Count; i++)
            {
                if (owner.Roster[i].id == owner.CharacterId)
                {
                    preview = i;
                }
            }
            var safe = Screen.safeArea;
            RectTransform_Viewport.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            RectTransform_Viewport.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            KagitKartMenuPaperMotion_Viewport.Play(0, owner.ReducedMotion);
            Render();
        }

        protected override void OnShow() => EventSystem.current?.SetSelectedGameObject(choices[preview].gameObject);

        private void Preview(int index)
        {
            if (world == null)
            {
                return;
            }
            preview = index;
            GlobalData.Dispatch(new KagitKartAction(world, KagitKartCommand.PreviewCharacter, id: world.Roster[index].id));
            Render();
        }

        private void Choose(int index)
        {
            if (world != null)
            {
                GlobalData.Dispatch(new KagitKartAction(world, KagitKartCommand.ChooseCharacter, id: world.Roster[index].id));
            }
        }

        private void Render()
        {
            if (world == null)
            {
                return;
            }
            KagitKartMenuPaperMotion_Viewport.SetReducedMotion(world.ReducedMotion);
            var c = world.Roster[preview];
            TextMeshProUGUI_DriverName.text = KagitKartCatalog.CharacterName(c.id);
            var values = new[] { c.stats.speed, c.stats.accel, c.stats.handling, c.stats.weight, c.stats.offroad };
            for (int row = 0; row < ticks.Length; row++)
            {
                int count = Mathf.Clamp(Mathf.FloorToInt(values[row] + 0.5f), 0, 5);
                for (int k = 0; k < ticks[row].Length; k++)
                {
                    ticks[row][k].color = k < count ? UIColors.KagitKart.StatFilled : UIColors.KagitKart.StatEmpty;
                }
            }
            foreach (var choice in choices)
            {
                choice.interactable = !world.Busy;
            }
            Button_Back.interactable = Button_Hub.interactable = !world.Busy;
            TextMeshProUGUI_Status.text = world.Feedback ?? (world.SpeedClass + "g  ·  "
                + (world.Mode == KagitKartRaceMode.GrandPrix ? "确认车手后开始四章大奖赛" : "确认车手后选择章节"));
        }

        private void Back() => world?.Dispatch(KagitKartCommand.Classes);

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
