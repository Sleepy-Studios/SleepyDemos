using System;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Core.Runtime
{
    public static class RegisterExtend
    {
        [ComponentAttribute("On{0}Click", true)]
        public static void RegisterButton(this View view, Button button, UnityAction onClick)
        {
            button.onClick.AddListener(onClick);
        }

        [ComponentAttribute("On{0}Click", true)]
        public static void RegisterToggle(this View view, Toggle toggle, UnityAction<bool> onClick)
        {
            toggle.onValueChanged.AddListener(onClick);
        }

        [ComponentAttribute("On{0}ValueChanged", true)]
        public static void RegisterInputField(this View view, InputField inputField, UnityAction<string> onValue)
        {
            inputField.onValueChanged.AddListener(onValue);
        }

        [ComponentAttribute("On{0}ValueChanged", true)]
        public static void RegisterSlider(this View view, Slider slider, UnityAction<float> onValue)
        {
            slider.onValueChanged.AddListener(onValue);
        }

        [ComponentAttribute("On{0}ValueChanged", true)]
        public static void RegisterDropDown(this View view, Dropdown dropdown, UnityAction<int> onValue)
        {
            dropdown.onValueChanged.AddListener(onValue);
        }

        [ComponentAttribute("On{0}Click", true)]
        public static void RegisterUITab(this View view, UITab tab, Action<int> action)
        {
            tab.Register(action);
        }

        [ComponentAttribute("On{0}Click")]
        public static void RegisterViewTab(this View view, ViewTab tab, Action<int> action)
        {
            tab.Register(action);
        }

        /// <summary>
        /// 注册手风琴 Tab 的叶子页签点击回调，供 MvcBind 生成 View 绑定代码使用。
        /// </summary>
        [ComponentAttribute("On{0}Click")]
        public static void RegisterAccordionTab(this View view, AccordionTab tab, Action<int> action)
        {
            tab.Register(action);
        }

        /// <summary>
        /// 注册手风琴 ViewTab 的叶子页签点击回调，供 MvcBind 生成 View 绑定代码使用。
        /// </summary>
        [ComponentAttribute("On{0}Click")]
        public static void RegisterAccordionViewTab(this View view, AccordionViewTab tab, Action<int> action)
        {
            tab.Register(action);
        }

        [ComponentAttribute("On{0}Click")]
        public static void RegisterViewList(this View view, ViewList list, Action<int> action)
        {
            list.Register(action);
        }

        /// <summary>
        /// 注册循环列表的数据填充回调，供 MvcBind 生成的非泛型绑定使用。
        /// </summary>
        [ComponentAttribute("On{0}RectData")]
        public static void RegisterLoopScrollRect(this View view, LoopScrollRect rect, Action<ItemView, int> action)
        {
            rect.RegisterItemSource(action);
        }

        /// <summary>
        /// 注册循环列表的强类型数据填充回调。
        /// </summary>
        public static void RegisterLoopScrollRect<TView>(this View view, LoopScrollRect rect, Action<TView, int> action)
            where TView : ItemView, new()
        {
            rect.RegisterItemSource(action);
        }

        /// <summary>
        /// 注册循环列表 item 点击回调，供 MvcBind 生成的非泛型绑定使用。
        /// </summary>
        [ComponentAttribute("On{0}Click")]
        public static void RegisterLoopScrollClick(this View view, LoopScrollRect rect, Action<ItemView, int> action)
        {
            rect.RegisterItemClick<ItemView>(action);
        }

        /// <summary>
        /// 注册循环列表 item 的强类型点击回调。
        /// </summary>
        public static void RegisterLoopScrollClick<TView>(this View view, LoopScrollRect rect, Action<TView, int> action)
            where TView : ItemView, new()
        {
            rect.RegisterItemClick(action);
        }

        /// <summary>
        /// 注册循环列表 item 隐藏回收回调，供 MvcBind 生成的非泛型绑定使用。
        /// </summary>
        [ComponentAttribute("On{0}ItemHide")]
        public static void RegisterLoopScrollItemHide(this View view, LoopScrollRect rect, Action<ItemView, int> action)
        {
            rect.RegisterItemHide<ItemView>(action);
        }

        /// <summary>
        /// 注册循环列表 item 的强类型隐藏回收回调。
        /// </summary>
        public static void RegisterLoopScrollItemHide<TView>(this View view, LoopScrollRect rect, Action<TView, int> action)
            where TView : ItemView, new()
        {
            rect.RegisterItemHide(action);
        }

        /// <summary>
        /// 注册多预制体/多类型循环列表的数据填充回调。
        /// </summary>
        [ComponentAttribute("On{0}RectDataMulti")]
        public static void RegisterLoopScrollRectMulti(this View view, LoopScrollRect rect, Action<ItemView, int> action)
        {
            rect.RegisterMultiItemSource(action);
        }

        /// <summary>
        /// 注册多预制体/多类型循环列表的 item 点击回调。
        /// </summary>
        [ComponentAttribute("On{0}MultiClick")]
        public static void RegisterLoopScrollMultiClick(this View view, LoopScrollRect rect, Action<ItemView, int> action)
        {
            rect.RegisterMultiItemClick(action);
        }

        /// <summary>
        /// 注册多预制体/多类型循环列表的 item 隐藏回收回调。
        /// </summary>
        [ComponentAttribute("On{0}MultiItemHide")]
        public static void RegisterLoopScrollMultiItemHide(this View view, LoopScrollRect rect, Action<ItemView, int> action)
        {
            rect.RegisterMultiItemHide(action);
        }

        [ComponentAttribute("On{0}ValueChanged", true)]
        public static void RegisterUIDropdown(this View view, UIDropdown dropdown, Action<int> action)
        {
            dropdown.Register(action);
        }

        [ComponentAttribute("On{0}ValueChanged", true)]
        public static void RegisterUIBtnSwitch(this View view, UIBtnSwitch btnSwitch, Action<bool> action)
        {
            btnSwitch.Register(action);
        }

        [ComponentAttribute("On{0}Click", true)]
        public static void RegisterButton(this ItemView view, Button button, UnityAction onClick)
        {
            button.onClick.AddListener(onClick);
        }

        [ComponentAttribute("On{0}Click", true)]
        public static void RegisterToggle(this ItemView view, Toggle toggle, UnityAction<bool> onClick)
        {
            toggle.onValueChanged.AddListener(onClick);
        }

        [ComponentAttribute("On{0}ValueChanged", true)]
        public static void RegisterInputField(this ItemView view, InputField inputField, UnityAction<string> onValue)
        {
            inputField.onValueChanged.AddListener(onValue);
        }

        [ComponentAttribute("On{0}ValueChanged", true)]
        public static void RegisterSlider(this ItemView view, Slider slider, UnityAction<float> onValue)
        {
            slider.onValueChanged.AddListener(onValue);
        }

        [ComponentAttribute("On{0}ValueChanged", true)]
        public static void RegisterDropDown(this ItemView view, Dropdown dropdown, UnityAction<int> onValue)
        {
            dropdown.onValueChanged.AddListener(onValue);
        }

        [ComponentAttribute("On{0}Click", true)]
        public static void RegisterUITab(this ItemView view, UITab tab, Action<int> action)
        {
            tab.Register(action);
        }

        [ComponentAttribute("On{0}Click")]
        public static void RegisterViewTab(this ItemView view, ViewTab tab, Action<int> action)
        {
            tab.Register(action);
        }

        /// <summary>
        /// 注册手风琴 Tab 的叶子页签点击回调，供 MvcBind 生成 ItemView 绑定代码使用。
        /// </summary>
        [ComponentAttribute("On{0}Click")]
        public static void RegisterAccordionTab(this ItemView view, AccordionTab tab, Action<int> action)
        {
            tab.Register(action);
        }

        /// <summary>
        /// 注册手风琴 ViewTab 的叶子页签点击回调，供 MvcBind 生成 ItemView 绑定代码使用。
        /// </summary>
        [ComponentAttribute("On{0}Click")]
        public static void RegisterAccordionViewTab(this ItemView view, AccordionViewTab tab, Action<int> action)
        {
            tab.Register(action);
        }

        [ComponentAttribute("On{0}Click")]
        public static void RegisterViewList(this ItemView view, ViewList list, Action<int> action)
        {
            list.Register(action);
        }

        /// <summary>
        /// 注册循环列表的数据填充回调，供 MvcBind 生成的非泛型绑定使用。
        /// </summary>
        [ComponentAttribute("On{0}RectData")]
        public static void RegisterLoopScrollRect(this ItemView view, LoopScrollRect rect, Action<ItemView, int> action)
        {
            rect.RegisterItemSource(action);
        }

        /// <summary>
        /// 注册循环列表的强类型数据填充回调。
        /// </summary>
        public static void RegisterLoopScrollRect<TView>(this ItemView view, LoopScrollRect rect, Action<TView, int> action)
            where TView : ItemView, new()
        {
            rect.RegisterItemSource(action);
        }

        /// <summary>
        /// 注册循环列表 item 点击回调，供 MvcBind 生成的非泛型绑定使用。
        /// </summary>
        [ComponentAttribute("On{0}Click")]
        public static void RegisterLoopScrollClick(this ItemView view, LoopScrollRect rect, Action<ItemView, int> action)
        {
            rect.RegisterItemClick<ItemView>(action);
        }

        /// <summary>
        /// 注册循环列表 item 的强类型点击回调。
        /// </summary>
        public static void RegisterLoopScrollClick<TView>(this ItemView view, LoopScrollRect rect, Action<TView, int> action)
            where TView : ItemView, new()
        {
            rect.RegisterItemClick(action);
        }

        /// <summary>
        /// 注册循环列表 item 隐藏回收回调，供 MvcBind 生成的非泛型绑定使用。
        /// </summary>
        [ComponentAttribute("On{0}ItemHide")]
        public static void RegisterLoopScrollItemHide(this ItemView view, LoopScrollRect rect, Action<ItemView, int> action)
        {
            rect.RegisterItemHide<ItemView>(action);
        }

        /// <summary>
        /// 注册循环列表 item 的强类型隐藏回收回调。
        /// </summary>
        public static void RegisterLoopScrollItemHide<TView>(this ItemView view, LoopScrollRect rect, Action<TView, int> action)
            where TView : ItemView, new()
        {
            rect.RegisterItemHide(action);
        }

        /// <summary>
        /// 注册多预制体/多类型循环列表的数据填充回调。
        /// </summary>
        [ComponentAttribute("On{0}RectDataMulti")]
        public static void RegisterLoopScrollRectMulti(this ItemView view, LoopScrollRect rect, Action<ItemView, int> action)
        {
            rect.RegisterMultiItemSource(action);
        }

        /// <summary>
        /// 注册多预制体/多类型循环列表的 item 点击回调。
        /// </summary>
        [ComponentAttribute("On{0}MultiClick")]
        public static void RegisterLoopScrollMultiClick(this ItemView view, LoopScrollRect rect, Action<ItemView, int> action)
        {
            rect.RegisterMultiItemClick(action);
        }

        /// <summary>
        /// 注册多预制体/多类型循环列表的 item 隐藏回收回调。
        /// </summary>
        [ComponentAttribute("On{0}MultiItemHide")]
        public static void RegisterLoopScrollMultiItemHide(this ItemView view, LoopScrollRect rect, Action<ItemView, int> action)
        {
            rect.RegisterMultiItemHide(action);
        }

        [ComponentAttribute("On{0}ValueChanged", true)]
        public static void RegisterUIDropdown(this ItemView view, UIDropdown dropdown, Action<int> action)
        {
            dropdown.Register(action);
        }

        [ComponentAttribute("On{0}ValueChanged", true)]
        public static void RegisterUIBtnSwitch(this ItemView view, UIBtnSwitch btnSwitch, Action<bool> action)
        {
            btnSwitch.Register(action);
        }
    }
}
