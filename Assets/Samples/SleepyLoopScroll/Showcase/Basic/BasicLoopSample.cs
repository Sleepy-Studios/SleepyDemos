using System.Collections.Generic;
using UnityEngine;

namespace SleepyStudios.LoopScroll.Samples.Basic
{
    public sealed class BasicLoopSample : LoopSamplePage
    {
        private readonly List<LoopSampleItem> items = new List<LoopSampleItem>();
        private LoopScrollView vertical, horizontal, grid, current;
        protected override string TitleKey => "basic";
        protected override void BuildPage()
        {
            for (var i = 0; i < 100000; i++) items.Add(new LoopSampleItem(i));
            vertical = MakeList("Vertical", new Vector2(840, 480), new Vector2(0, -10), LoopLayout.Vertical);
            horizontal = MakeList("Horizontal", new Vector2(840, 480), new Vector2(0, -10), LoopLayout.Horizontal);
            grid = MakeList("Grid", new Vector2(840, 480), new Vector2(0, -10), LoopLayout.VerticalGrid);
            Submit(vertical); Submit(horizontal); Submit(grid); Choose(vertical); SetStatus("basicDesc");
            ActionButton("vertical", new Vector2(-310, 250), () => Choose(vertical));
            ActionButton("horizontal", new Vector2(-105, 250), () => Choose(horizontal));
            ActionButton("grid", new Vector2(100, 250), () => Choose(grid));
            ActionButton("goto50000", new Vector2(305, 250), () => current.ScrollToCell(50000, ScrollAlignment.Center, new ScrollAnimation(.25f)));
        }
        private void Submit(LoopScrollView view)
        {
            view.RegisterCellBinding<LoopCell>((cell, index, context) => Bind(cell, items[index], context));
            view.SetTotalCount(items, getItemKey: item => ((LoopSampleItem)item).Key);
        }
        private void Choose(LoopScrollView view)
        { vertical.gameObject.SetActive(view == vertical); horizontal.gameObject.SetActive(view == horizontal); grid.gameObject.SetActive(view == grid); current = view; }
        protected override void UpdateDataLanguage()
        { vertical?.RefreshCells(); horizontal?.RefreshCells(); grid?.RefreshCells(); }
    }
}
