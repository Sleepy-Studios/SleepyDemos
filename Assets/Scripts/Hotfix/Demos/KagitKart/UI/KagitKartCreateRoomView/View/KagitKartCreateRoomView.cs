namespace Hotfix
{
    using Core.Runtime;
    using Cysharp.Threading.Tasks;
    using TMPro;
    using Hotfix.KagitKart;
    using UnityEngine;
    using UnityEngine.EventSystems;

    [Module("KagitKart")]
    [UIBind("KagitKartCreateRoomView")]
    public partial class KagitKartCreateRoomView : View
    {
        private KagitKartWorld world;
        private int capacity;
        private int speedClass;
        private int trackIndex;
        private KagitKartRaceMode mode;
        private bool updating;
        private string validationMessage;
        private TMP_Text backLabel;

        protected override void OnGameObjectInitialize()
        {
            backLabel = Button_Back.GetComponentInChildren<TMP_Text>(true);
            Button_Capacity.onClick.AddListener(() =>
            {
                capacity = capacity == 2 ? 4 : capacity == 4 ? 8 : 2;
                Render();
            });
            Button_Mode.onClick.AddListener(() =>
            {
                mode = mode == KagitKartRaceMode.SingleRace ? KagitKartRaceMode.GrandPrix : KagitKartRaceMode.SingleRace;
                if (mode == KagitKartRaceMode.GrandPrix)
                {
                    trackIndex = 0;
                }
                Render();
            });
            Button_SpeedClass.onClick.AddListener(() =>
            {
                speedClass = speedClass == 80 ? 120 : speedClass == 120 ? 200 : 80;
                Render();
            });
            Button_Track.onClick.AddListener(() =>
            {
                trackIndex = (trackIndex + 1) % KagitKartCatalog.Tracks.Count;
                Render();
            });
            TMP_InputField_RoomName.onValueChanged.AddListener(_ =>
            {
                validationMessage = null;
                Render();
            });
            Button_Confirm.onClick.AddListener(Submit);
            Button_Back.onClick.AddListener(Back);
            UIMenuScope_Sheet.Canceled += Back;
            BindData<KagitKartData>(_ => Render());
        }

        /// <summary>读取房主大厅设置，或初始化新的房间候选。</summary>
        /// <param name="owner">当前 Demo 宿主；已加入的房间只允许房主在大厅修改。</param>
        public void SetData(KagitKartWorld owner)
        {
            world = owner;
            var room = owner.Network?.Room;
            updating = room != null && owner.Network.IsHost;
            capacity = updating ? room.Capacity : 8;
            speedClass = updating ? room.SpeedClass : 120;
            mode = updating ? room.Mode : KagitKartRaceMode.SingleRace;
            trackIndex = 0;
            if (updating)
            {
                for (int i = 0; i < KagitKartCatalog.Tracks.Count; i++)
                {
                    if (KagitKartCatalog.Tracks[i] == room.TrackId)
                    {
                        trackIndex = i;
                        break;
                    }
                }
            }
            validationMessage = null;
            TMP_InputField_RoomName.SetTextWithoutNotify(updating ? room.Name : "纸艺车队");
            var safe = Screen.safeArea;
            RectTransform_Viewport.anchorMin = new Vector2(safe.xMin / Mathf.Max(1, Screen.width), safe.yMin / Mathf.Max(1, Screen.height));
            RectTransform_Viewport.anchorMax = new Vector2(safe.xMax / Mathf.Max(1, Screen.width), safe.yMax / Mathf.Max(1, Screen.height));
            float fit = Mathf.Min(1, (RectTransform_Viewport.rect.width - 48) / 1120, (RectTransform_Viewport.rect.height - 48) / 920);
            RectTransform_SheetGroup.localScale = Vector3.one * Mathf.Max(0.01f, fit);
            KagitKartMenuPaperMotion_Viewport.Play(owner.MenuEntranceDelay, owner.ReducedMotion);
            Render();
        }

        protected override void OnShow() => EventSystem.current?.SetSelectedGameObject(TMP_InputField_RoomName.gameObject);

        private void Submit()
        {
            if (world == null || world.Busy)
            {
                return;
            }
            string name = TMP_InputField_RoomName.text.Trim();
            if (name.Length == 0)
            {
                validationMessage = "请输入房间名称。";
                Render();
                return;
            }
            validationMessage = null;
            GlobalData.Dispatch(new KagitKartRoomConfigurationAction(world, name, capacity, mode,
                speedClass, KagitKartCatalog.Tracks[trackIndex], updating));
        }

        private void Render()
        {
            if (world == null)
            {
                return;
            }
            bool editable = !world.Busy && (!updating || world.Network?.IsHost == true && world.Network.Room?.Phase == "Lobby");
            KagitKartMenuPaperMotion_Viewport.SetReducedMotion(world.ReducedMotion);
            TextMeshProUGUI_TitleLabel.text = updating ? "修改房间设置" : "创建房间";
            TextMeshProUGUI_CapacityValue.text = capacity + " 人";
            TextMeshProUGUI_ModeValue.text = mode == KagitKartRaceMode.GrandPrix ? "大奖赛" : "单场比赛";
            TextMeshProUGUI_SpeedClassValue.text = speedClass + "g";
            TextMeshProUGUI_TrackValue.text = KagitKartCatalog.TrackName(KagitKartCatalog.Tracks[trackIndex]);
            TMP_InputField_RoomName.interactable = Button_Capacity.interactable = Button_Mode.interactable
                = Button_SpeedClass.interactable = Button_Confirm.interactable = editable;
            Button_Track.interactable = editable && mode == KagitKartRaceMode.SingleRace;
            Button_Back.interactable = !world.Busy || !updating && world.ConnectingOnlineRoom && !world.CancelingOnlineConnection;
            if (backLabel != null)
            {
                backLabel.text = world.CancelingOnlineConnection ? "正在取消…" : world.ConnectingOnlineRoom ? "取消连接" : "返回";
            }
            TextMeshProUGUI_Status.text = validationMessage ?? world.Feedback
                ?? (updating ? "修改配置会取消车手准备；点击选项切换。" : "点击选项切换；大奖赛从第一章开始。");
        }

        private void Back()
        {
            if (world == null)
            {
                return;
            }
            if (!updating && world.ConnectingOnlineRoom)
            {
                world.CancelOnlineRoomConnectionAsync().Forget(Debug.LogException);
            }
            else if (!world.Busy)
            {
                world.Dispatch(updating ? KagitKartCommand.Room : KagitKartCommand.Online);
            }
        }

        protected override void OnHide() => world = null;

        protected override void OnDestroy()
        {
            UIMenuScope_Sheet.Canceled -= Back;
            world = null;
        }
    }
}
