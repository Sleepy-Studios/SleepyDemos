using System;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Core.Runtime
{
    public sealed class HotfixEntrySystem : StartupSystemBase
    {
        private const string EntryTypeName = "Hotfix.AppDelegate.HotfixEntry";
        private const string EntryMethodName = "Awake";

        public HotfixEntrySystem(StartupStateBase state) : base(state)
        {
        }

        public override async UniTask ExecuteAsync()
        {
            Report(0f, "查找热更入口");
            var entryType = FindEntryType(EntryTypeName);
            if (entryType == null)
            {
                Debug.LogError($"[Startup] 未找到热更入口类型: {EntryTypeName}");
                return;
            }

            var method = entryType.GetMethod(
                EntryMethodName,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (method == null)
            {
                Debug.LogError($"[Startup] 未找到热更入口方法: {EntryTypeName}.{EntryMethodName}");
                return;
            }

            Report(0.35f, "调用热更入口");
            var result = method.Invoke(null, new object[] { new HotfixStartupContext(Context) });
            await AwaitEntryResult(result);

            Report(1f, "热更入口执行完成");
        }

        private static async UniTask AwaitEntryResult(object result)
        {
            if (result is UniTask task)
            {
                await task;
                return;
            }

            if (result is System.Threading.Tasks.Task systemTask)
            {
                await systemTask.AsUniTask();
                return;
            }

            if (result is UniTaskVoid)
            {
                await UniTask.Yield();
            }
        }

        private Type FindEntryType(string entryTypeName)
        {
            foreach (var assembly in Context.HotfixAssemblies)
            {
                var type = assembly.GetType(entryTypeName);
                if (type != null)
                {
                    return type;
                }
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(entryTypeName);
                if (type != null)
                {
                    return type;
                }
            }

            return null;
        }
    }
}
