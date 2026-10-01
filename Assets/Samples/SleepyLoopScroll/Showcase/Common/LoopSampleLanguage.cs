using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace SleepyStudios.LoopScroll.Samples
{
    /// <summary>只服务示例的中英文文本，不进入包运行时或宿主语言框架。</summary>
    public static class LoopSampleLanguage
    {
        public const string PreferenceKey = "SleepyLoopScroll.Showcase.Language";
        private static readonly Dictionary<string, string[]> texts = new Dictionary<string, string[]>
        {
            { "menu", new[] { "循环列表示例", "Loop Scroll Showcase" } },
            { "choose", new[] { "选择示例，体验功能并查看接入方式", "Choose a sample to explore its behavior" } },
            { "back", new[] { "返回主页", "Back" } },
            { "language", new[] { "中文 / English", "中文 / English" } },
            { "basic", new[] { "基础列表", "Basic lists" } },
            { "basicDesc", new[] { "十万条数据 · 纵向 / 横向 / 固定网格", "100,000 items · vertical / horizontal / fixed grid" } },
            { "multi", new[] { "多类型与选择", "Multiple types and selection" } },
            { "multiDesc", new[] { "独立对象池 · 稳定身份 · 点击选择", "Per-type pools · stable identity · click to select" } },
            { "chat", new[] { "动态聊天", "Dynamic chat" } },
            { "chatDesc", new[] { "加载历史不跳动 · 贴底 · 未读计数", "History anchors · follow latest · unread count" } },
            { "carousel", new[] { "轮播与分页", "Carousel and paging" } },
            { "carouselDesc", new[] { "循环翻页 · 自动播放 · 分页请求 · 嵌套拖动", "Looping pages · autoplay · paging · nested drag" } },
            { "mvc", new[] { "MvcBind / ItemView 接入", "MvcBind / ItemView integration" } },
            { "mvcDesc", new[] { "先注册回调，再提交集合、刷新与定位", "Register once, then submit, refresh and scroll" } },
            { "vertical", new[] { "纵向", "Vertical" } },
            { "horizontal", new[] { "横向", "Horizontal" } },
            { "grid", new[] { "固定网格", "Grid" } },
            { "goto50000", new[] { "定位第 50,000 项", "Go to 50,000" } },
            { "entry", new[] { "条目 {0}", "Entry {0}" } },
            { "equipment", new[] { "装备 {0}", "Equipment {0}" } },
            { "consumable", new[] { "消耗品 {0}", "Consumable {0}" } },
            { "selected", new[] { "已选择 {0} 项", "Selected: {0}" } },
            { "clearSelection", new[] { "清除选择", "Clear selection" } },
            { "refresh", new[] { "刷新可见项", "Refresh visible" } },
            { "goto150", new[] { "定位第 150 项", "Go to 150" } },
            { "history", new[] { "加载历史", "Load history" } },
            { "newMessage", new[] { "新消息", "New message" } },
            { "latest", new[] { "回到最新", "Jump to latest" } },
            { "unread", new[] { "未读 {0} 条 · 向上滚动查看历史", "Unread: {0} · scroll upward to browse history" } },
            { "message", new[] { "消息 {0}\n{1}", "Message {0}\n{1}" } },
            { "body0", new[] { "今天去海边钓鱼吧。", "Let's go fishing by the sea today." } },
            { "body1", new[] { "刚刚钓到一条很大的鱼！你们那边情况怎么样？", "I just caught a big fish! How is everyone doing?" } },
            { "body2", new[] { "出发前记得检查鱼竿、鱼线和鱼饵。\n天气预报说下午会有阵雨，我们可以先去码头等一会儿。", "Check your rod, line and bait before leaving.\nRain is expected this afternoon, so let's wait at the dock." } },
            { "body3", new[] { "这是一条比较长的聊天消息，用来展示自动换行后的动态高度。列表回收和重新绑定以后，消息身份仍然保持不变。向顶部加载更早的记录时，当前看到的内容应当留在原来的位置。", "This longer message demonstrates wrapping and dynamic height. Its identity remains stable when cells are recycled. Loading older messages should keep the current content in place." } },
            { "body4", new[] { "收到。\n等你。\n一起出发！", "Got it.\nSee you soon.\nLet's go!" } },
            { "ocean", new[] { "海洋\n第 1 页", "OCEAN\nPage 1" } },
            { "island", new[] { "岛屿\n第 2 页", "ISLAND\nPage 2" } },
            { "sky", new[] { "天空\n第 3 页", "SKY\nPage 3" } },
            { "page", new[] { "轮播第 {0} / 3 页", "Carousel page: {0} / 3" } },
            { "previous", new[] { "上一页", "Previous page" } },
            { "next", new[] { "下一页", "Next page" } },
            { "more", new[] { "加载更多", "More entries" } },
            { "loaded", new[] { "已加载条目 {0}", "Loaded entry {0}" } },
            { "loading", new[] { "正在加载下一页…", "Loading the next page…" } },
            { "loadedCount", new[] { "已加载 {0} 项", "Loaded {0} items" } },
            { "mvcItem", new[] { "ItemView 条目 {0}", "ItemView entry {0}" } },
            { "goto500", new[] { "定位第 500 项", "Go to 500" } },
            { "reset", new[] { "重新填充", "Refill" } },
            { "clicked", new[] { "点击条目 {0} · 当前身份 {1}", "Clicked item {0} · current key {1}" } },
            { "offsetPositive", new[] { "第500项 · 偏移 +60", "Item 500 · offset +60" } },
            { "offsetNegative", new[] { "第500项 · 偏移 -60", "Item 500 · offset -60" } },
            { "cancelScroll", new[] { "取消定位", "Cancel scroll" } },
            { "scrollPending", new[] { "定位中 · 可再次定位、取消或拖动打断", "Scrolling · locate again, cancel, or drag to interrupt" } },
            { "scrollCompleted", new[] { "定位完成", "Scroll completed" } },
            { "scrollCanceled", new[] { "定位已取消 · {0}", "Scroll canceled · {0}" } },
            { "scrollReasonReplaced", new[] { "已被新定位替代", "Replaced by another scroll" } },
            { "scrollReasonDragStarted", new[] { "开始拖动", "Drag started" } },
            { "scrollReasonExplicitCancel", new[] { "手动取消", "Manually canceled" } },
            { "scrollReasonDataChanged", new[] { "列表数据已更新", "List data changed" } },
            { "scrollReasonDisabled", new[] { "列表已隐藏", "List disabled" } },
            { "scrollReasonDestroyed", new[] { "列表已关闭", "List destroyed" } },
            { "scrollReasonViewportUnavailable", new[] { "显示区域暂不可用", "Viewport unavailable" } },
            { "missingScene", new[] { "找不到示例场景：{0}。请重新构建示例。", "Sample scene missing: {0}. Rebuild the showcase." } },
            { "missingBuild", new[] { "场景未加入独立演示构建：{0}", "Scene not included in the standalone showcase build: {0}" } },
            { "loadError", new[] { "场景加载失败：{0}", "Scene loading failed: {0}" } }
        };
        public static bool IsEnglish => PlayerPrefs.GetInt(PreferenceKey, 0) == 1;
        public static event Action Changed;
        /// <summary>立即更新当前示例，并在之后的场景和运行中保留选择。</summary>
        public static void SetEnglish(bool english)
        {
            if (IsEnglish == english) return;
            PlayerPrefs.SetInt(PreferenceKey, english ? 1 : 0); PlayerPrefs.Save(); Changed?.Invoke();
        }
        public static string Get(string key, params object[] args)
        {
            if (!texts.TryGetValue(key, out var pair)) throw new ArgumentException("Unknown sample text: " + key, nameof(key));
            return args.Length == 0 ? pair[IsEnglish ? 1 : 0] : string.Format(CultureInfo.InvariantCulture, pair[IsEnglish ? 1 : 0], args);
        }
    }
    /// <summary>显示语言与业务身份分离，翻译不会改变 Key 或消息类型。</summary>
    public sealed class LoopSampleItem
    {
        public readonly int Id;
        public readonly int Type;
        public readonly string TextKey;
        public string Key => Id.ToString(CultureInfo.InvariantCulture);
        public LoopSampleItem(int id, string textKey = "entry", int type = 0) { Id = id; TextKey = textKey; Type = type; }
        public string Display => TextKey == "message"
            ? LoopSampleLanguage.Get("message", Id, LoopSampleLanguage.Get("body" + Math.Abs(Id % 5)))
            : LoopSampleLanguage.Get(TextKey, Id);
    }
}
