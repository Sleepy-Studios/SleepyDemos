using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Core.Runtime.Inputs
{
    /// 重绑定候选副本；导出覆盖记录后由设置系统应用，源资产始终不被写入。
    public sealed class InputRebindSession : IDisposable
    {
        [Serializable]
        private sealed class OverrideList
        {
            public OverrideIdentity[] bindings = null;
        }

        [Serializable]
        private sealed class OverrideIdentity
        {
            public string id = null;
        }

        private InputActionRebindingExtensions.RebindingOperation operation;
        private Action<bool, string> completion;
        private bool disposed;

        /// 未启用的候选动作资产，供设置页显示实际候选绑定。
        public InputActionAsset Asset { get; }
        public string SourceId { get; }
        public bool IsListening => operation != null;
        /// 最近校验冲突动作的稳定身份；界面通过制作词条表达，不解析诊断字符串。
        public string ConflictingActionId { get; private set; }

        /// <summary>查询单个实际绑定能否编辑；复合根和菜单安全入口保留固定状态。</summary>
        /// <param name="actionPath">源资产中的 Map/Action 路径。</param>
        /// <param name="bindingIndex">实际绑定或复合子绑定序号。</param>
        /// <param name="reason">不可编辑的原因；可编辑时为空。</param>
        /// <returns>可编辑时为 true；销毁后的会话仍抛出生命周期异常。</returns>
        public bool CanRebind(string actionPath, int bindingIndex, out string reason)
        {
            ThrowIfDisposed();
            try
            {
                GetEditable(actionPath, bindingIndex);
                reason = null;
                return true;
            }
            catch (ArgumentException exception)
            {
                reason = exception.Message;
                return false;
            }
        }

        /// <summary>克隆源资产并加载可选的候选覆盖；无效记录拒绝创建。</summary>
        /// <param name="source">正式源资产或会话副本，不修改原对象。</param>
        /// <param name="overrides">已保存或编辑中的 override JSON；为空恢复制作绑定。</param>
        public InputRebindSession(InputActionAsset source, string overrides = null)
        {
            SourceId = InputActionSession.GetSourceId(source);
            Asset = UnityEngine.Object.Instantiate(source);
            Asset.Disable();
            try
            {
                Asset.RemoveAllBindingOverrides();
                if (!string.IsNullOrEmpty(overrides))
                {
                    ValidateIdentities(overrides);
                    Asset.LoadBindingOverridesFromJson(overrides);
                }
                ValidateFixedActions();
                ValidateOverrides();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        /// <summary>开始监听一个普通或复合子绑定；确认返回与整个复合绑定不可修改。</summary>
        /// <param name="actionPath">Map/Action 路径。</param>
        /// <param name="bindingIndex">绑定序号，复合动作需逐个子绑定重绑。</param>
        /// <param name="completed">成功、取消或冲突时调用一次；第二参数为失败原因。</param>
        public void Begin(string actionPath, int bindingIndex, Action<bool, string> completed)
        {
            ThrowIfDisposed();
            if (IsListening)
            {
                throw new InvalidOperationException("已有重绑定监听，请先取消。");
            }
            var action = GetEditable(actionPath, bindingIndex);
            ConflictingActionId = null;
            var previous = action.bindings[bindingIndex];
            completion = completed;
            operation = action.PerformInteractiveRebinding(bindingIndex)
                .WithCancelingThrough("*/{Cancel}")
                .WithControlsExcluding("<Pointer>/position")
                .WithControlsExcluding("<Pointer>/delta")
                .OnMatchWaitForAnother(0.1f)
                .OnCancel(_ => Finish(false, "已取消重绑定。"))
                .OnComplete(_ =>
                {
                    try
                    {
                        ValidateConflict(action, bindingIndex);
                        Finish(true, null);
                    }
                    catch (ArgumentException exception)
                    {
                        action.ApplyBindingOverride(bindingIndex, previous);
                        Finish(false, exception.Message);
                    }
                });
            string path = previous.effectivePath;
            if (path.StartsWith("<Gamepad>", StringComparison.Ordinal))
            {
                operation.WithControlsHavingToMatchPath("<Gamepad>");
            }
            else if (path.StartsWith("<Keyboard>", StringComparison.Ordinal) || path.StartsWith("<Mouse>", StringComparison.Ordinal))
            {
                operation.WithControlsHavingToMatchPath("<Keyboard>").WithControlsHavingToMatchPath("<Mouse>");
            }
            try
            {
                operation.Start();
            }
            catch
            {
                Finish(false, "无法开始重绑定。");
                throw;
            }
        }

        /// <summary>显式配置候选绑定，供按钮选键或制作工具使用。</summary>
        /// <param name="actionPath">可编辑的 Map/Action。</param>
        /// <param name="bindingIndex">单个绑定或复合子绑定序号。</param>
        /// <param name="path">Input System 控件路径，不能解绑为空。</param>
        public void SetBinding(string actionPath, int bindingIndex, string path)
        {
            ThrowIfDisposed();
            if (IsListening)
            {
                throw new InvalidOperationException("监听期间不能另写候选绑定。");
            }
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("<", StringComparison.Ordinal) || !path.Contains("/"))
            {
                throw new ArgumentException("绑定必须提供设备控件路径。", nameof(path));
            }
            var action = GetEditable(actionPath, bindingIndex);
            var previous = action.bindings[bindingIndex];
            action.ApplyBindingOverride(bindingIndex, path);
            try
            {
                ValidateConflict(action, bindingIndex);
            }
            catch
            {
                action.ApplyBindingOverride(bindingIndex, previous);
                throw;
            }
        }

        /// <summary>恢复一个动作的制作绑定，不影响其它候选动作。</summary>
        /// <param name="actionPath">可编辑的 Map/Action。</param>
        public void ResetAction(string actionPath)
        {
            ThrowIfDisposed();
            if (IsListening)
            {
                Cancel();
            }
            var action = Asset.FindAction(actionPath, true);
            if (IsFixed(action))
            {
                throw new ArgumentException("公共菜单确认与返回不可重绑定。", nameof(actionPath));
            }
            action.RemoveAllBindingOverrides();
        }

        /// 取消监听，不保存候选或改变正在运行的会话。
        public void Cancel() => operation?.Cancel();

        /// 导出候选覆盖；设置系统按 SourceId 保存，监听未结束时拒绝提交。
        public string ExportOverrides()
        {
            ThrowIfDisposed();
            if (IsListening)
            {
                throw new InvalidOperationException("重绑定监听尚未结束。");
            }
            ValidateFixedActions();
            ValidateOverrides();
            return Asset.SaveBindingOverridesAsJson();
        }

        private InputAction GetEditable(string actionPath, int index)
        {
            var action = Asset.FindAction(actionPath, true);
            if (IsFixed(action) || index < 0 || index >= action.bindings.Count || action.bindings[index].isComposite)
            {
                throw new ArgumentException("请选择可编辑的单个绑定，公共菜单绑定不可修改。");
            }
            return action;
        }

        private void ValidateFixedActions()
        {
            foreach (var action in Asset)
            {
                if (!IsFixed(action))
                {
                    continue;
                }
                foreach (var binding in action.bindings)
                {
                    if (binding.hasOverrides)
                    {
                        throw new ArgumentException("覆盖记录不能改变公共菜单的固定确认和返回。");
                    }
                }
            }
        }

        private void ValidateIdentities(string json)
        {
            var records = JsonUtility.FromJson<OverrideList>(json);
            if (records?.bindings == null)
            {
                throw new ArgumentException("按键覆盖 JSON 缺少绑定记录。");
            }
            var known = new HashSet<Guid>();
            foreach (var action in Asset)
            {
                foreach (var binding in action.bindings)
                {
                    known.Add(binding.id);
                }
            }
            var seen = new HashSet<Guid>();
            foreach (var record in records.bindings)
            {
                if (record == null || !Guid.TryParse(record.id, out var id) || !known.Contains(id) || !seen.Add(id))
                {
                    throw new ArgumentException("覆盖记录包含未知或重复绑定身份。");
                }
            }
        }

        private void ValidateOverrides()
        {
            foreach (var action in Asset)
            {
                for (int i = 0; i < action.bindings.Count; i++)
                {
                    var binding = action.bindings[i];
                    if (binding.hasOverrides && !binding.isComposite)
                    {
                        ValidateConflict(action, i);
                    }
                }
            }
        }

        private static bool IsFixed(InputAction action)
        {
            return action.actionMap.name == "UI" || action.actionMap.name == "Menu"
                || action.name is "Submit" or "Cancel" or "Point" or "Click";
        }

        private void ValidateConflict(InputAction action, int index)
        {
            ConflictingActionId = null;
            var selected = action.bindings[index];
            string selectedChord = GetChord(action, index);
            if (string.IsNullOrEmpty(selected.effectivePath))
            {
                throw new ArgumentException("不能移除动作的绑定。");
            }
            foreach (var other in action.actionMap.actions)
            {
                for (int i = 0; i < other.bindings.Count; i++)
                {
                    if (other == action && i == index || other.bindings[i].isComposite)
                    {
                        continue;
                    }
                    var binding = other.bindings[i];
                    string otherChord = GetChord(other, i);
                    if (other == action && selectedChord != null && selectedChord == otherChord)
                    {
                        continue;
                    }
                    bool sameInput = selectedChord != null || otherChord != null
                        ? selectedChord != null && otherChord != null && selectedChord == otherChord
                        : string.Equals(binding.effectivePath, selected.effectivePath, StringComparison.OrdinalIgnoreCase);
                    if (GroupsOverlap(binding.groups, selected.groups) && sameInput)
                    {
                        ConflictingActionId = other.id.ToString();
                        throw new ArgumentException("按键已用于同一上下文的动作：" + other.name);
                    }
                }
            }
        }

        private static string GetChord(InputAction action, int index)
        {
            if (!action.bindings[index].isPartOfComposite)
            {
                return null;
            }
            int header = index - 1;
            while (header >= 0 && action.bindings[header].isPartOfComposite)
            {
                header--;
            }
            if (header < 0 || action.bindings[header].effectivePath.IndexOf("Modifier", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return null;
            }
            var parts = new List<string>();
            for (int i = header + 1; i < action.bindings.Count && action.bindings[i].isPartOfComposite; i++)
            {
                parts.Add(action.bindings[i].name.ToLowerInvariant() + ":" + action.bindings[i].effectivePath.ToLowerInvariant());
            }
            parts.Sort(StringComparer.Ordinal);
            return string.Join("|", parts);
        }

        private static bool GroupsOverlap(string first, string second)
        {
            if (string.IsNullOrEmpty(first) || string.IsNullOrEmpty(second))
            {
                return true;
            }
            foreach (string group in first.Split(';'))
            {
                foreach (string other in second.Split(';'))
                {
                    if (string.Equals(group, other, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void Finish(bool succeeded, string message)
        {
            var oldOperation = operation;
            var callback = completion;
            operation = null;
            completion = null;
            oldOperation?.Dispose();
            callback?.Invoke(succeeded, message);
        }

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(InputRebindSession));
            }
        }

        /// 释放监听与候选，退出页面后不继续响应硬件。
        public void Dispose()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            Cancel();
            operation?.Dispose();
            operation = null;
            completion = null;
            if (Asset != null)
            {
                if (Application.isPlaying)
                {
                    UnityEngine.Object.Destroy(Asset);
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(Asset);
                }
            }
        }
    }
}
