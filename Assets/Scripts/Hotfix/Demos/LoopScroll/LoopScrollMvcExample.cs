using System.Collections.Generic;
using Core.Runtime;
using Cysharp.Threading.Tasks;
using SleepyStudios.LoopScroll;
using UnityEngine;
using UnityEngine.UI;

namespace Hotfix.Demos.LoopScroll
{
    /// 独立本地示例入口，直接打开 loop_scroll 示例场景即可，不依赖 Hub 导航。
    public sealed class LoopScrollMvcExample : MonoBehaviour
    {
        private LoopScrollExampleView view;
        private void Start()
        {
            view = new LoopScrollExampleView();
            view.InitWithGameObject(gameObject);
        }
        private void OnDestroy() { if (view != null) view.DestroyAsync().Forget(); }
    }

    public sealed class LoopScrollExampleItem : ItemView<string>
    {
        private Text label;
        protected override void InitComponent() { label = gameObject.GetComponentInChildren<Text>(true); }
        /// <summary>显示当前数据，ItemView 在物理 Cell 复用时继续使用。</summary>
        /// <param name="data">当前业务项文本。</param>
        /// <returns>当前 ItemView。</returns>
        public override ItemView<string> SetData(string data) { base.SetData(data); if (label != null) label.text = data; return this; }
    }

    public sealed class LoopScrollExampleView : View
    {
        private LoopScrollView messages;
        private readonly List<string> items = new List<string>();
        protected override IUITransition CreateUITransition() => new EmptyUITransition();
        protected override void InitComponent()
        {
            messages = gameObject.GetComponent<LoopScrollView>();
            // 与 MvcBind 生成的三种注册方法完全一致；订阅交由 View.AddBinding 清理。
            this.RegisterLoopCellBind(messages, OnMessagesCellBind);
            this.RegisterLoopCellUnbind(messages, OnMessagesCellUnbind);
            this.RegisterLoopCellClick(messages, OnMessagesCellClick);
        }
        protected override void OnGameObjectInitialize()
        {
            for (var i = 0; i < 1000; i++) items.Add("MvcBind ItemView  /  " + i);
            messages.ItemViews().SetItems<string, LoopScrollExampleItem>(items, (cell, item, context) => cell.SetData(item), null, item => item);
            gameObject.SetActive(true);
        }
        private void OnMessagesCellBind(ItemView cell, int index, CellBindContext context) { cell.SetIndex(index); }
        private void OnMessagesCellUnbind(ItemView cell, CellBindContext context)
        {
            // 异步资源加载应绑定 context.CancellationToken，并在写入前检查 IsCurrent。
        }
        private void OnMessagesCellClick(ItemView cell, int index, CellBindContext context)
        { if (context.IsCurrent) Debug.Log($"LoopScroll MvcBind click: key={context.Key}, index={index}"); }
    }
}
