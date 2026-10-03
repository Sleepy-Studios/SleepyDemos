using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Core.Runtime.Inputs
{
    /// 借用Core现有EventSystem导航。只改变导航门闩与选中项，不接管指针、不新增Module。
    public sealed class MenuInputScope : IDisposable
    {
        private static readonly List<MenuInputScope> Owners = new();
        private readonly Transform selectionRoot;
        private readonly EventSystem eventSystem;
        private readonly InputSystemUIInputModule module;
        private bool originalNavigation;
        private GameObject originalSelection;
        private readonly GameplayInputRouter router;
        private readonly InputAction uiMove;
        private readonly InputAction uiSubmit;
        private readonly InputAction uiCancel;
        private GameplayInputContext context;
        private GameObject initialSelection;
        private bool awaitingNeutral;
        private int enteredFrame;
        private bool disposed;

        /// <summary>页面或玩法接管输入时建立作用域，退出时恢复公共EventSystem原导航状态。</summary>
        /// <param name="existing">Core已有的EventSystem，必须包含InputSystemUIInputModule。</param>
        /// <param name="inputRouter">本输入会话路由器；提供时导航回调仅更新设备来源，不重复执行动作。</param>
        /// <param name="selectionRoot">页面允许的焦点范围，null 表示由宿主维护。</param>
        public MenuInputScope(EventSystem existing, GameplayInputRouter inputRouter = null, Transform selectionRoot = null)
        {
            eventSystem = existing != null ? existing : throw new ArgumentNullException(nameof(existing));
            module = existing.GetComponent<InputSystemUIInputModule>();
            if (module == null) throw new ArgumentException("需要复用Core的InputSystemUIInputModule。", nameof(existing));
            this.selectionRoot = selectionRoot;
            InputDeviceState.Initialize();
            Owners.Add(this);
            originalNavigation = existing.sendNavigationEvents; originalSelection = existing.currentSelectedGameObject;
            router = inputRouter; uiMove = module.move?.action;
            uiSubmit = module.submit?.action; uiCancel = module.cancel?.action;
            if (uiMove != null) uiMove.performed += OnNavigationDevice;
            if (uiSubmit != null) uiSubmit.performed += OnButtonDevice;
            if (uiCancel != null) uiCancel.performed += OnButtonDevice;
        }

        /// <summary>菜单焦点仅归Core；桌面/探索关闭UI导航并清选中项，指针交互仍由Core运行。</summary>
        /// <param name="context">仅Menu允许公共Submit/Cancel/Navigate；Interaction焦点由宿主物件控制器维护。</param>
        /// <param name="firstMenuSelection">保存的可用菜单控件；恢复后选中，不创建UI或主动提交。</param>
        public void SetContext(GameplayInputContext context, GameObject firstMenuSelection = null)
        {
            if (disposed) throw new ObjectDisposedException(nameof(MenuInputScope));
            if (!Enum.IsDefined(typeof(GameplayInputContext), context)) throw new ArgumentOutOfRangeException(nameof(context));
            this.context = context;
            if (IsOwner) { eventSystem.sendNavigationEvents = false; eventSystem.SetSelectedGameObject(null); }
            awaitingNeutral = context == GameplayInputContext.Menu;
            initialSelection = firstMenuSelection; enteredFrame = Time.frameCount;
        }

        /// 每帧调用；有Router时在它之后驱动，等切换帧及UI键释放，防止A/Enter重复提交。
        public void Update()
        {
            if (!IsOwner || disposed || eventSystem == null || module == null) return;
            if (!awaitingNeutral)
            {
                if (eventSystem.currentSelectedGameObject != null && !IsSelectionValid()) RestoreMenuFocus();
                return;
            }
            if (Time.frameCount <= enteredFrame) return;
            if (Pressed(module.submit?.action) || Pressed(module.cancel?.action)
                || module.move?.action != null && module.move.action.ReadValue<Vector2>().sqrMagnitude > 0.01f) return;
            awaitingNeutral = false; eventSystem.sendNavigationEvents = true;
            RestoreMenuFocus();
        }

        /// <summary>刷新虚拟列表当前可用焦点，再驱动原输入门闩；不重建作用域或重新等待按键释放。</summary>
        /// <param name="firstMenuSelection">当前数据身份对应的可用菜单控件；为空时不恢复过期物理按钮。</param>
        public void Update(GameObject firstMenuSelection)
        {
            bool targetChanged = initialSelection != firstMenuSelection;
            initialSelection = firstMenuSelection;
            Update();
            if (targetChanged && context == GameplayInputContext.Menu)
                RestoreMenuFocus();
        }

        /// 页面隐藏或玩法退出时恢复公共导航门闩及仍有效的原焦点，重复调用无副作用。
        public void Dispose()
        {
            if (disposed) return;
            bool wasOwner = IsOwner;
            int ownerIndex = Owners.IndexOf(this);
            // 下层页面先销毁时，把其原始快照转交给上层，避免恢复已失效页面。
            if (ownerIndex >= 0 && ownerIndex + 1 < Owners.Count && Owners[ownerIndex + 1].eventSystem == eventSystem)
            {
                Owners[ownerIndex + 1].originalSelection = originalSelection;
                Owners[ownerIndex + 1].originalNavigation = originalNavigation;
            }
            Owners.Remove(this);
            disposed = true;
            if (uiMove != null) uiMove.performed -= OnNavigationDevice;
            if (uiSubmit != null) uiSubmit.performed -= OnButtonDevice;
            if (uiCancel != null) uiCancel.performed -= OnButtonDevice;
            if (eventSystem == null || !wasOwner) return;
            eventSystem.sendNavigationEvents = originalNavigation;
            eventSystem.SetSelectedGameObject(IsAvailable(originalSelection) ? originalSelection : null);
        }

        private static bool Pressed(InputAction action)
        {
            if (action == null) return false;
            foreach (var control in action.controls) if (control is ButtonControl button && button.isPressed) return true;
            return false;
        }

        private void OnNavigationDevice(InputAction.CallbackContext callback)
        {
            if (IsOwner && context == GameplayInputContext.Menu && callback.ReadValue<Vector2>().sqrMagnitude > 0.01f)
            {
                InputDeviceState.Notify(callback.control.device);
                router?.NotifyMenuDevice(callback.control.device);
                RestoreMenuFocus();
            }
        }

        private void OnButtonDevice(InputAction.CallbackContext callback)
        {
            if (!IsOwner || context != GameplayInputContext.Menu) return;
            InputDeviceState.Notify(callback.control.device);
            router?.NotifyMenuDevice(callback.control.device);
            RestoreMenuFocus();
        }

        private void RestoreMenuFocus()
        {
            // 点击空白会清焦点；后续键盘/手柄操作仅补回可用控件，提交仍由现有UI模块派发。
            if (!IsOwner || disposed || awaitingNeutral || eventSystem == null || !eventSystem.sendNavigationEvents ||
                IsSelectionValid() || !IsAvailable(initialSelection)) return;
            var selectable = initialSelection.GetComponent<Selectable>();
            if (selectable != null && (!selectable.IsActive() || !selectable.IsInteractable())) return;
            eventSystem.SetSelectedGameObject(initialSelection);
        }

        private bool IsOwner
        {
            get
            {
                for (int i = Owners.Count - 1; i >= 0; i--)
                    if (!Owners[i].disposed && Owners[i].eventSystem == eventSystem) return Owners[i] == this;
                return false;
            }
        }

        private bool IsSelectionValid()
        {
            var selected = eventSystem.currentSelectedGameObject;
            return IsAvailable(selected) && (selectionRoot == null || selected.transform.IsChildOf(selectionRoot));
        }

        private static bool IsAvailable(GameObject value)
        {
            if (value == null || !value.activeInHierarchy) return false;
            var target = value.GetComponent<Selectable>();
            return target == null || target.IsActive() && target.IsInteractable();
        }

    }
}
