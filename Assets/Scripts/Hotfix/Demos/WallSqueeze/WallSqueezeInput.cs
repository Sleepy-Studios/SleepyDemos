using System;
using System.Collections.Generic;
using Core.Runtime.Inputs;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace Hotfix.WallSqueeze
{
    /// 消费公共路由器，只将指针与轴输入转换为选墙和坐标目标。
    public sealed class WallSqueezeInput : IDisposable
    {
        private readonly Camera camera;
        private readonly List<RaycastResult> uiHits = new();
        private int pointerId = -1;
        private int heldWall = -1;
        private float offset;
        private bool waitForRelease;
        /// 公共设备、暂停、断连与震动入口。
        public GameplayInputRouter Router { get; }
        /// 当前选中的可移动墙索引；没有可移动墙时为 -1。
        public int Selected { get; private set; }
        /// 当前指针持有墙，-1 表示未持有。
        public int HeldWall => heldWall;

        /// <summary>使用已保存动作模板，独占公共路由器的私有会话。</summary>
        /// <param name="template">三张公共语义映射的动作资产。</param>
        /// <param name="worldCamera">屏幕坐标转世界的正交相机。</param>
        public WallSqueezeInput(InputActionAsset template, Camera worldCamera)
        {
            camera = worldCamera;
            Router = new GameplayInputRouter(template);
            Router.SetContext(GameplayInputContext.Interaction);
            Router.PauseState.Changed += CancelHold;
            Router.DeviceChanged += OnDeviceChanged;
        }

        /// <summary>每帧只消费一次路由器。真实指针按下前排除所有可射线 UI。</summary>
        /// <param name="simulation">当前关卡规则。</param>
        /// <param name="seconds">本帧未缩放时长。</param>
        /// <param name="wallSpeed">保存参数里的最大墙速，保留摇杆幅度。</param>
        /// <param name="wall">输出墙命令索引，未操作为 -1。</param>
        /// <param name="target">输出允许轴上的绝对坐标。</param>
        /// <returns>本帧是否请求暂停。</returns>
        public bool Read(WallSqueezeSimulation simulation, float seconds, float wallSpeed, out int wall, out float target)
        {
            var frame = Router.ReadFrame(seconds);
            var actions = Router.ConsumeActions();
            wall = -1;
            target = 0;
            if (Router.PauseState.IsPaused || Router.Context == GameplayInputContext.Menu)
            {
                CancelHold();
                return (actions & GameplayInputActions.Pause) != 0;
            }
            if ((uint)Selected >= simulation.Walls.Length || simulation.Walls[Selected].IsFixed)
            {
                Selected = FindMovable(simulation, -1, 1);
                CancelHold();
            }
            if (Selected < 0)
            {
                CancelHold();
                return (actions & GameplayInputActions.Pause) != 0;
            }
            if ((actions & GameplayInputActions.PreviousGroup) != 0)
            {
                Selected = FindMovable(simulation, Selected, -1);
                CancelHold();
            }
            if ((actions & GameplayInputActions.NextGroup) != 0)
            {
                Selected = FindMovable(simulation, Selected, 1);
                CancelHold();
            }
            bool down = ReadPointer(out Vector2 point, out int id, out bool pressed);
            if (!down)
            {
                heldWall = -1;
                pointerId = -1;
                waitForRelease = false;
            }
            if (pressed && !waitForRelease && heldWall < 0 && !HitsUi(point))
            {
                Vector2 position = camera.ScreenToWorldPoint(point);
                for (int i = 0; i < simulation.Walls.Length; i++)
                {
                    var item = simulation.Walls[i];
                    if (item.IsFixed)
                    {
                        continue;
                    }
                    Vector2 distance = position - item.Center;
                    bool wallHit = Mathf.Abs(distance.x) <= item.Size.x / 2 + .16f && Mathf.Abs(distance.y) <= item.Size.y / 2 + .16f;
                    bool handleHit = Mathf.Abs(distance.x) <= .55f && Mathf.Abs(distance.y) <= .55f;
                    if (wallHit || handleHit)
                    {
                        Selected = heldWall = i;
                        pointerId = id;
                        offset = item.Center[item.Axis] - position[item.Axis];
                        break;
                    }
                }
            }
            var selected = simulation.Walls[Selected];
            if (heldWall >= 0 && down && id == pointerId)
            {
                wall = heldWall;
                target = ((Vector2)camera.ScreenToWorldPoint(point))[selected.Axis] + offset;
            }
            else if (Mathf.Abs(frame.InteractionNavigation[selected.Axis]) > 0.001f)
            {
                wall = Selected;
                target = selected.Center[selected.Axis] + frame.InteractionNavigation[selected.Axis] * wallSpeed * seconds;
            }
            return (actions & GameplayInputActions.Pause) != 0;
        }

        /// 取消拖动，直到当前指针释放才接受下一次按下。
        public void CancelHold()
        {
            heldWall = -1;
            pointerId = -1;
            waitForRelease = true;
        }

        /// 重试和换关恢复首墙并使用公共上下文门闩等待旧轴、按钮释放。
        public void ResetForLevel()
        {
            CancelHold();
            Selected = 0;
            Router.SetContext(GameplayInputContext.Menu);
            if (!Router.PauseState.IsPaused)
            {
                Router.SetContext(GameplayInputContext.Interaction);
            }
        }

        /// 清理本次会话事件及震动。
        public void Dispose()
        {
            CancelHold();
            Router.PauseState.Changed -= CancelHold;
            Router.DeviceChanged -= OnDeviceChanged;
            Router.Dispose();
        }

        private void OnDeviceChanged(InputDeviceKind kind)
        {
            if (heldWall >= 0)
            {
                CancelHold();
            }
            if (Router.Context == GameplayInputContext.Interaction)
            {
                Router.SetContext(GameplayInputContext.Menu);
                Router.SetContext(GameplayInputContext.Interaction);
            }
        }

        private static int FindMovable(WallSqueezeSimulation simulation, int start, int direction)
        {
            int count = simulation.Walls.Length;
            for (int step = 1; step <= count; step++)
            {
                int index = (start + direction * step + count * 2) % count;
                if (!simulation.Walls[index].IsFixed)
                {
                    return index;
                }
            }
            return -1;
        }

        private bool ReadPointer(out Vector2 point, out int id, out bool pressed)
        {
            point = Vector2.zero;
            id = -1;
            pressed = false;
            var screen = Touchscreen.current;
            if (screen != null)
            {
                foreach (var touch in screen.touches)
                {
                    if (!touch.press.isPressed || pointerId >= 0 && touch.touchId.ReadValue() != pointerId)
                    {
                        continue;
                    }
                    id = touch.touchId.ReadValue();
                    point = touch.position.ReadValue();
                    pressed = touch.press.wasPressedThisFrame;
                    return true;
                }
            }
            if (pointerId >= 0)
            {
                return false;
            }
            var mouse = Mouse.current;
            if (mouse == null)
            {
                return false;
            }
            point = mouse.position.ReadValue();
            pressed = mouse.leftButton.wasPressedThisFrame;
            return mouse.leftButton.isPressed;
        }

        private bool HitsUi(Vector2 point)
        {
            if (EventSystem.current == null)
            {
                return false;
            }
            uiHits.Clear();
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = point }, uiHits);
            return uiHits.Count > 0;
        }

    }
}
