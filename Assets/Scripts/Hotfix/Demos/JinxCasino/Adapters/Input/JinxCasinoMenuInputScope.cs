using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;

namespace Hotfix.JinxCasino.Adapters.Input
{
    /// 借用Core现有EventSystem导航。只改变导航门闩与选中项，不接管指针、不新增Module。
    public sealed class JinxCasinoMenuInputScope : IDisposable
    {
        private readonly EventSystem eventSystem;
        private readonly InputSystemUIInputModule module;
        private readonly bool originalNavigation;
        private readonly GameObject originalSelection;
        private readonly JinxCasinoInputRouter router;
        private readonly InputAction uiMove;
        private readonly InputAction uiPoint;
        private JinxCasinoInputContext context;
        private GameObject initialSelection;
        private bool awaitingNeutral;
        private int enteredFrame;
        private bool disposed;

        /// <summary>在Demo接管输入时建立作用域，退出时恢复公共EventSystem原导航状态。</summary>
        /// <param name="existing">Core已有的EventSystem，必须包含InputSystemUIInputModule。</param>
        /// <param name="inputRouter">本Demo路由器；提供时Core导航/指针回调仅更新设备提示，不重复执行动作。</param>
        public JinxCasinoMenuInputScope(EventSystem existing, JinxCasinoInputRouter inputRouter = null)
        {
            eventSystem = existing != null ? existing : throw new ArgumentNullException(nameof(existing));
            module = existing.GetComponent<InputSystemUIInputModule>();
            if (module == null) throw new ArgumentException("需要复用Core的InputSystemUIInputModule。", nameof(existing));
            originalNavigation = existing.sendNavigationEvents; originalSelection = existing.currentSelectedGameObject;
            router = inputRouter; uiMove = module.move?.action; uiPoint = module.point?.action;
            if (uiMove != null) uiMove.performed += OnNavigationDevice;
            if (uiPoint != null) uiPoint.performed += OnPointerDevice;
        }

        /// <summary>菜单焦点仅归Core；桌面/探索关闭UI导航并清选中项，指针交互仍由Core运行。</summary>
        /// <param name="context">仅Menu允许公共Submit/Cancel/Navigate；Table焦点由桌面控制器自行维护。</param>
        /// <param name="firstMenuSelection">保存的可用菜单控件；恢复后选中，不创建UI或主动提交。</param>
        public void SetContext(JinxCasinoInputContext context, GameObject firstMenuSelection = null)
        {
            if (disposed) throw new ObjectDisposedException(nameof(JinxCasinoMenuInputScope));
            if (!Enum.IsDefined(typeof(JinxCasinoInputContext), context)) throw new ArgumentOutOfRangeException(nameof(context));
            eventSystem.sendNavigationEvents = false; eventSystem.SetSelectedGameObject(null);
            this.context = context;
            awaitingNeutral = context == JinxCasinoInputContext.Menu;
            initialSelection = firstMenuSelection; enteredFrame = Time.frameCount;
        }

        /// 每帧在Router之后调用；等切换帧过去且UI键/导航释放，防止开菜单的A/Enter重复提交。
        public void Update()
        {
            if (disposed || !awaitingNeutral || eventSystem == null || module == null || Time.frameCount <= enteredFrame) return;
            if (Pressed(module.submit?.action) || Pressed(module.cancel?.action)
                || module.move?.action != null && module.move.action.ReadValue<Vector2>().sqrMagnitude > 0.01f) return;
            awaitingNeutral = false; eventSystem.sendNavigationEvents = true;
            if (initialSelection != null && initialSelection.activeInHierarchy) eventSystem.SetSelectedGameObject(initialSelection);
        }

        /// 退出Demo恢复公共导航门闩及仍有效的原焦点，重复调用无副作用。
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (uiMove != null) uiMove.performed -= OnNavigationDevice;
            if (uiPoint != null) uiPoint.performed -= OnPointerDevice;
            if (eventSystem == null) return;
            eventSystem.sendNavigationEvents = originalNavigation;
            eventSystem.SetSelectedGameObject(originalSelection != null && originalSelection.activeInHierarchy ? originalSelection : null);
        }

        private static bool Pressed(InputAction action)
        {
            if (action == null) return false;
            foreach (var control in action.controls) if (control is ButtonControl button && button.isPressed) return true;
            return false;
        }

        private void OnNavigationDevice(InputAction.CallbackContext callback)
        {
            if (context == JinxCasinoInputContext.Menu && callback.ReadValue<Vector2>().sqrMagnitude > 0.01f)
                router?.NotifyMenuDevice(callback.control.device);
        }

        private void OnPointerDevice(InputAction.CallbackContext callback)
        {
            if (context == JinxCasinoInputContext.Menu) router?.NotifyMenuDevice(callback.control.device);
        }
    }
}
