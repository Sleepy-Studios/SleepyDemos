using System;
using System.Collections.Generic;
using Core.Runtime;
using Cysharp.Threading.Tasks;

namespace Hotfix.AppDelegate
{
    public sealed class HotfixWorldTransitionProvider : IUIWorldTransitionProvider
    {
        private readonly Dictionary<Type, Func<IUIWorldTransition>> factories =
            new Dictionary<Type, Func<IUIWorldTransition>>();

        /// <summary>
        /// 注册 View 类型对应的世界过渡工厂；重复注册会替换原工厂。
        /// </summary>
        /// <typeparam name="TView">需要世界表现过渡的 View 类型。</typeparam>
        /// <param name="factory">每次导航事务创建独立过渡实例的工厂。</param>
        public void Register<TView>(Func<IUIWorldTransition> factory) where TView : View
        {
            Register(typeof(TView), factory);
        }

        /// <summary>
        /// 注册 View 类型对应的世界过渡工厂；只按精确类型匹配，重复注册会替换原工厂。
        /// </summary>
        /// <param name="viewType">需要世界表现过渡的具体 View 类型。</param>
        /// <param name="factory">每次导航事务创建独立过渡实例的工厂；不可为 null。</param>
        public void Register(Type viewType, Func<IUIWorldTransition> factory)
        {
            EnsureMainThread();
            if (viewType == null)
            {
                throw new ArgumentNullException(nameof(viewType));
            }

            if (!typeof(View).IsAssignableFrom(viewType) || viewType.IsAbstract)
            {
                throw new ArgumentException("World Transition 只能注册具体 View 类型。", nameof(viewType));
            }

            factories[viewType] = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        /// <summary>
        /// 移除指定 View 类型的世界过渡工厂。
        /// </summary>
        /// <param name="viewType">需要移除注册的具体 View 类型。</param>
        /// <returns>存在并移除注册时返回 true。</returns>
        public bool Unregister(Type viewType)
        {
            EnsureMainThread();
            return viewType != null && factories.Remove(viewType);
        }

        /// <summary>
        /// 按 View 精确类型创建本次事务使用的世界过渡；未注册时返回 null。
        /// </summary>
        /// <param name="view">需要解析世界过渡的 View。</param>
        /// <returns>工厂创建的世界过渡；未注册或工厂选择空实现时为 null。</returns>
        public IUIWorldTransition Resolve(View view)
        {
            EnsureMainThread();
            if (view == null)
            {
                throw new ArgumentNullException(nameof(view));
            }

            return factories.TryGetValue(view.GetType(), out var factory)
                ? factory()
                : null;
        }

        private static void EnsureMainThread()
        {
            if (!PlayerLoopHelper.IsMainThread)
            {
                throw new InvalidOperationException("World Transition Provider 只能在 Unity 主线程注册和解析。");
            }
        }
    }
}
