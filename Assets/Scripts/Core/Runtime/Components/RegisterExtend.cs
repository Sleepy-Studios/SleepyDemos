using System;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Core.Runtime
{
    public static class RegisterExtend
    {
        /// <summary>绑定设备无关命令点击；随 View 根对象销毁解除。</summary>
        /// <param name="view">拥有此按钮的页面。</param>
        /// <param name="button">已保存的输入按钮。</param>
        /// <param name="onClick">业务命令回调。</param>
        [ComponentAttribute("On{0}Click", true)]
        public static void RegisterInputCommandButton(this View view, Inputs.InputCommandButton button, Action<string> onClick)
        {
            button.Clicked += onClick;
            view.AddBinding(new CallbackBinding(() => { if ((object)button != null) button.Clicked -= onClick; }));
        }

        /// <summary>绑定按下与释放；公共按钮处理指针归属与禁用释放。</summary>
        /// <param name="view">拥有此按钮的页面。</param>
        /// <param name="button">已保存的输入按钮。</param>
        /// <param name="onHold">命令名与保持状态。</param>
        [ComponentAttribute("On{0}HoldChanged")]
        public static void RegisterInputCommandHold(this View view, Inputs.InputCommandButton button, Action<string, bool> onHold)
        {
            button.HoldChanged += onHold;
            view.AddBinding(new CallbackBinding(() => { if ((object)button != null) button.HoldChanged -= onHold; }));
        }
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
        /// 注册手风琴 Tab 的叶子页签点击回调，供 UIBind 生成 View 绑定代码使用。
        /// </summary>
        [ComponentAttribute("On{0}Click")]
        public static void RegisterAccordionTab(this View view, AccordionTab tab, Action<int> action)
        {
            tab.Register(action);
        }

        /// <summary>
        /// 注册手风琴 ViewTab 的叶子页签点击回调，供 UIBind 生成 View 绑定代码使用。
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
        /// 注册手风琴 Tab 的叶子页签点击回调，供 UIBind 生成 ItemView 绑定代码使用。
        /// </summary>
        [ComponentAttribute("On{0}Click")]
        public static void RegisterAccordionTab(this ItemView view, AccordionTab tab, Action<int> action)
        {
            tab.Register(action);
        }

        /// <summary>
        /// 注册手风琴 ViewTab 的叶子页签点击回调，供 UIBind 生成 ItemView 绑定代码使用。
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
