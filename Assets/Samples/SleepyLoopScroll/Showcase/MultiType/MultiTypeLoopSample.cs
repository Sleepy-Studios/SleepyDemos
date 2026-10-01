using System.Collections.Generic;
using UnityEngine;

namespace SleepyStudios.LoopScroll.Samples.MultiType
{
    public sealed class MultiTypeLoopSample : LoopSamplePage
    {
        private readonly List<LoopSampleItem> items = new List<LoopSampleItem>();
        private LoopScrollView list;
        protected override string TitleKey => "multi";
        protected override void BuildPage()
        {
            for (var i = 0; i < 200; i++) items.Add(new LoopSampleItem(i, i % 2 == 0 ? "equipment" : "consumable", i % 2));
            list = MakeList("Mixed", new Vector2(840, 480), new Vector2(0, -10), LoopLayout.Vertical, false,
                new[] { Template(new Color(.13f, .24f, .36f)), Template(new Color(.2f, .17f, .32f)) });
            list.SetDataSource(new MixedSource(items));
            var selection = list.gameObject.AddComponent<LoopSelectionController>(); selection.MultiSelect = true;
            selection.CellSelectionChanged += (cell, selected) => cell.GetComponent<UnityEngine.UI.Image>().color = selected
                ? new Color(.1f, .55f, .5f) : items[cell.Context.Index].Type == 0 ? new Color(.13f, .24f, .36f) : new Color(.2f, .17f, .32f);
            selection.SelectionChanged += () => SetStatus("selected", selection.SelectedKeys.Count);
            SetStatus("multiDesc");
            ActionButton("clearSelection", new Vector2(-205, 250), selection.ClearSelection);
            ActionButton("refresh", new Vector2(0, 250), list.RefreshCells);
            ActionButton("goto150", new Vector2(205, 250), () => list.ScrollToCell(150, ScrollAlignment.Center));
        }
        protected override void UpdateDataLanguage() { list?.RefreshCells(); }
        private sealed class MixedSource : ILoopDataSource
        {
            private readonly List<LoopSampleItem> items;
            public MixedSource(List<LoopSampleItem> items) { this.items = items; }
            public int Count => items.Count;
            public bool HasStableKeys => true;
            public string GetItemKey(int index) => items[index].Key;
            public int GetCellType(int index) => items[index].Type;
            public float GetEstimatedSize(int index, float crossAxisSize) => 52;
            public void BindCell(LoopCell cell, int index, CellBindContext context) => Bind(cell, items[index], context);
            public void UnbindCell(LoopCell cell, CellBindContext context) { }
        }
    }
}
