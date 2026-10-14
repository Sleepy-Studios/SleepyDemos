namespace Hotfix
{
    using Core.Runtime;
    using Hotfix.KagitKart;
    using TMPro;
    using UnityEngine;
    using UnityEngine.EventSystems;

    [Module("KagitKart")]
    [UIBind("KagitKartRoomView")]
    public partial class KagitKartRoomView : View
    {
        private KagitKartWorld world;
        private TextMeshProUGUI[] members;

        protected override void OnGameObjectInitialize()
        {
            members = new[]
            {
                TextMeshProUGUI_Member0, TextMeshProUGUI_Member1, TextMeshProUGUI_Member2, TextMeshProUGUI_Member3,
                TextMeshProUGUI_Member4, TextMeshProUGUI_Member5, TextMeshProUGUI_Member6, TextMeshProUGUI_Member7
            };
            TextMeshProUGUI_TitleLabel.richText = TextMeshProUGUI_RoomInfo.richText = false;
            TextMeshProUGUI_TitleLabel.textWrappingMode = TextWrappingModes.NoWrap;
            TextMeshProUGUI_TitleLabel.overflowMode = TextOverflowModes.Ellipsis;
            foreach (var member in members)
            {
                member.richText = false;
                member.textWrappingMode = TextWrappingModes.NoWrap;
                member.overflowMode = TextOverflowModes.Ellipsis;
            }
            Button_Character.onClick.AddListener(ChooseCharacter);
            Button_Ready.onClick.AddListener(() =>
            {
                var local = LocalPlayer();
                if (local != null)
                {
                    GlobalData.Dispatch(new KagitKartAction(world, KagitKartCommand.Ready, local.Ready ? 0 : 1));
                }
            });
            Button_Start.onClick.AddListener(() => world?.Dispatch(KagitKartCommand.StartOnlineRace));
            Button_Configure.onClick.AddListener(() => world?.Dispatch(KagitKartCommand.CreateRoom));
            Button_CopyCode.onClick.AddListener(() => world?.Dispatch(KagitKartCommand.CopyCode));
            Button_Back.onClick.AddListener(Back);
            UIMenuScope_Sheet.Canceled += Back;
            BindData<KagitKartData>(_ => Render());
        }

        /// <summary>显示房间快照；UI 不持有网络连接或推进比赛。</summary>
        /// <param name="owner">当前 Demo 宿主。</param>
        public void SetData(KagitKartWorld owner)
        {
            world = owner;
            var safe = Screen.safeArea;
            RectTransform_Viewport.anchorMin = new Vector2(safe.xMin / Mathf.Max(1, Screen.width), safe.yMin / Mathf.Max(1, Screen.height));
            RectTransform_Viewport.anchorMax = new Vector2(safe.xMax / Mathf.Max(1, Screen.width), safe.yMax / Mathf.Max(1, Screen.height));
            float fit = Mathf.Min(1, (RectTransform_Viewport.rect.width - 48) / 1120, (RectTransform_Viewport.rect.height - 48) / 920);
            RectTransform_SheetGroup.localScale = Vector3.one * Mathf.Max(0.01f, fit);
            KagitKartMenuPaperMotion_Viewport.Play(owner.MenuEntranceDelay, owner.ReducedMotion);
            Render();
        }

        protected override void OnShow() => EventSystem.current?.SetSelectedGameObject(Button_Ready.gameObject);

        private KagitKartRoomPlayer LocalPlayer()
        {
            var network = world?.Network;
            if (network?.Room == null)
            {
                return null;
            }
            foreach (var player in network.Room.Players)
            {
                if (player.MemberId == network.LocalMemberId)
                {
                    return player;
                }
            }
            return null;
        }

        private string NextCharacter()
        {
            var local = LocalPlayer();
            if (local == null || world.Roster == null)
            {
                return null;
            }
            int current = -1;
            for (int i = 0; i < world.Roster.Count; i++)
            {
                if (world.Roster[i].id == local.CharacterId)
                {
                    current = i;
                    break;
                }
            }
            for (int step = 1; step < world.Roster.Count; step++)
            {
                string id = world.Roster[(current + step) % world.Roster.Count].id;
                bool occupied = false;
                foreach (var player in world.Network.Room.Players)
                {
                    if (player.Connected && player.MemberId != local.MemberId && player.CharacterId == id)
                    {
                        occupied = true;
                        break;
                    }
                }
                if (!occupied)
                {
                    return id;
                }
            }
            return null;
        }

        private void ChooseCharacter()
        {
            string id = NextCharacter();
            if (id != null)
            {
                GlobalData.Dispatch(new KagitKartAction(world, KagitKartCommand.ChooseOnlineCharacter, id: id));
            }
        }

        private void Render()
        {
            if (world == null)
            {
                return;
            }
            var network = world.Network;
            var room = network?.Room;
            KagitKartMenuPaperMotion_Viewport.SetReducedMotion(world.ReducedMotion);
            TextMeshProUGUI_TitleLabel.text = room?.Name ?? "等待车手";
            TextMeshProUGUI_RoomInfo.text = room == null ? "" : string.Format("{0}  ·  {1}g  ·  {2}  ·  {3}/{4} 人",
                room.Mode == KagitKartRaceMode.GrandPrix ? "大奖赛" : "单场比赛", room.SpeedClass,
                KagitKartCatalog.TrackName(room.TrackId), room.Players.Length, room.Capacity);
            TextMeshProUGUI_RoomCode.text = network?.Code ?? "------";
            for (int i = 0; i < members.Length; i++)
            {
                if (room == null || i >= room.Players.Length)
                {
                    members[i].text = "";
                    continue;
                }
                var player = room.Players[i];
                string state = !player.Connected ? "已断开" : room.Phase == "Preparing"
                    ? player.Loaded ? "已加载" : "加载中" : player.Ready ? "已准备" : "未准备";
                string local = player.MemberId == network.LocalMemberId ? "（本机）" : "";
                string host = player.MemberId == network.AuthorityMemberId ? "房主" : "车手";
                members[i].text = string.Format("{0}.{1} {2}  ·  {3}  ·  {4}  ·  {5}", i + 1, local, host, state,
                    KagitKartCatalog.CharacterName(player.CharacterId), player.Name);
            }
            bool available = !world.Busy && network?.Connected == true && room != null;
            bool lobby = room?.Phase == "Lobby";
            Button_Character.interactable = available && lobby && NextCharacter() != null;
            Button_Ready.interactable = available && (lobby || room.Phase == "Results") && LocalPlayer() != null;
            Button_Start.interactable = available && network.CanStart;
            Button_Configure.gameObject.SetActive(network?.IsHost == true);
            Button_Configure.interactable = available && lobby && network.IsHost;
            Button_CopyCode.interactable = !world.Busy && !string.IsNullOrEmpty(network?.Code);
            Button_Back.interactable = !world.Busy;
            TextMeshProUGUI_Status.text = world.Feedback ?? network?.Status ?? "";
        }

        private void Back() => world?.Dispatch(KagitKartCommand.LeaveRoom);

        protected override void OnHide() => world = null;

        protected override void OnDestroy()
        {
            UIMenuScope_Sheet.Canceled -= Back;
            world = null;
        }
    }
}
