using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SleepyStudios.LoopScroll.Samples.Chat
{
    public sealed class ChatLoopSample : LoopSamplePage
    {
        private readonly List<LoopSampleItem> items = new List<LoopSampleItem>();
        private LoopScrollView list;
        private int serial = 1000;
        protected override string TitleKey => "chat";
        protected override void BuildPage()
        {
            for (var i = 0; i < 100; i++) items.Add(new LoopSampleItem(i, "message", i % 2));
            list = MakeList("Chat", new Vector2(840, 480), new Vector2(0, -10), LoopLayout.Vertical, true,
                new[] { Template(new Color(.12f, .24f, .34f)), Template(new Color(.2f, .17f, .32f)) });
            var chat = list.gameObject.AddComponent<LoopChatController>();
            chat.UnreadChanged += count => SetStatus("unread", count);
            list.SetDataSource(new MessageSource(items)); SetStatus("chatDesc");
            ActionButton("history", new Vector2(-205, 250), () => { var id = serial++; items.Insert(0, new LoopSampleItem(id, "message", id % 2)); chat.PrependHistory(1); });
            ActionButton("newMessage", new Vector2(0, 250), () => { var id = serial++; items.Add(new LoopSampleItem(id, "message", id % 2)); chat.AppendMessages(1); });
            ActionButton("latest", new Vector2(205, 250), chat.JumpToLatest);
        }
        protected override void UpdateDataLanguage()
        {
            if (list != null) list.RefillCells(new RefillOptions(list.DistanceToEnd <= 1 ? ScrollAnchorPolicy.StickToEnd : ScrollAnchorPolicy.KeepFirstVisible));
        }
        private sealed class MessageSource : ILoopDataSource
        {
            private readonly List<LoopSampleItem> data;
            public MessageSource(List<LoopSampleItem> data) { this.data = data; }
            public int Count => data.Count;
            public bool HasStableKeys => true;
            public string GetItemKey(int index) => data[index].Key;
            public int GetCellType(int index) => data[index].Type;
            public float GetEstimatedSize(int index, float crossAxisSize) => 100;
            public void BindCell(LoopCell cell, int index, CellBindContext context)
            {
                Bind(cell, data[index], context);
                var label = cell.GetComponentInChildren<Text>(true);
                cell.GetComponent<LayoutElement>().preferredHeight = Mathf.Max(52, label.preferredHeight + 16);
            }
            public void UnbindCell(LoopCell cell, CellBindContext context) { }
        }
    }
}
