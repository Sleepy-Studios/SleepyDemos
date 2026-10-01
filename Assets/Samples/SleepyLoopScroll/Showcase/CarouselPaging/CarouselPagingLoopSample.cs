using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SleepyStudios.LoopScroll.Samples.CarouselPaging
{
    public sealed class CarouselPagingLoopSample : LoopSamplePage
    {
        private readonly List<LoopSampleItem> items = new List<LoopSampleItem>();
        private LoopScrollView list, gallery;
        private Coroutine loading;
        protected override string TitleKey => "carousel";
        protected override void BuildPage()
        {
            var pages = new List<LoopSampleItem> { new LoopSampleItem(0, "ocean"), new LoopSampleItem(1, "island"), new LoopSampleItem(2, "sky") };
            gallery = MakeList("Gallery", new Vector2(840, 180), new Vector2(0, 130), LoopLayout.Horizontal);
            var carousel = gallery.gameObject.AddComponent<LoopCarouselController>(); carousel.Configure(300, 3, .2f);
            gallery.RegisterCellBinding<LoopCell>((cell, index, context) => Bind(cell, pages[index], context));
            gallery.SetTotalCount(pages, getItemKey: item => ((LoopSampleItem)item).Key);
            carousel.PageChanged += page => SetStatus("page", page + 1);
            for (var i = 0; i < 20; i++) items.Add(new LoopSampleItem(i, "loaded"));
            list = MakeList("Paged list", new Vector2(840, 260), new Vector2(0, -125), LoopLayout.Vertical);
            list.RegisterCellBinding<LoopCell>((cell, index, context) => Bind(cell, items[index], context));
            list.SetTotalCount(items, getItemKey: item => ((LoopSampleItem)item).Key);
            var paging = list.gameObject.AddComponent<LoopPagingTrigger>(); paging.Configure(false, true);
            paging.LoadRequested += boundary => { if (loading == null) loading = StartCoroutine(LoadPage(paging, boundary)); };
            gallery.ScrollRect.viewport.gameObject.AddComponent<NestedScrollRouter>().Configure(gallery.ScrollRect, list.ScrollRect);
            ActionButton("previous", new Vector2(-205, 250), carousel.Previous);
            ActionButton("next", new Vector2(0, 250), carousel.Next);
            ActionButton("more", new Vector2(205, 250), () => list.ScrollToCell(items.Count - 1, ScrollAlignment.End));
            SetStatus("carouselDesc");
        }
        protected override void UpdateDataLanguage() { gallery?.RefreshCells(); list?.RefreshCells(); }
        private IEnumerator LoadPage(LoopPagingTrigger paging, PagingBoundary boundary)
        {
            SetStatus("loading"); yield return new WaitForSecondsRealtime(.4f);
            var first = items.Count;
            for (var i = 0; i < 10; i++) items.Add(new LoopSampleItem(first + i, "loaded"));
            list.Append(10); paging.Complete(boundary, true); SetStatus("loadedCount", items.Count); loading = null;
        }
        protected override void OnDestroy()
        { if (loading != null) StopCoroutine(loading); loading = null; base.OnDestroy(); }
    }
}
