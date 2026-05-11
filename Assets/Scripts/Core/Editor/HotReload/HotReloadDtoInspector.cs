using System;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace SleepyDemos.Editor {
    internal static class HotReloadDtoInspector {
        private const string HotReloadRuntimeDependenciesAssemblyName = "SingularityGroup.HotReload.RuntimeDependencies";
        private const string LoginStatusResponseTypeName = "SingularityGroup.HotReload.DTO.LoginStatusResponse";

        [MenuItem("Tools/Hot Reload/打印 LoginStatusResponse 签名")]
        private static void PrintLoginStatusResponseSignature() {
            var type = FindType(LoginStatusResponseTypeName);
            if (type == null) {
                Debug.LogWarning(
                    "未找到 LoginStatusResponse。请先让 Unity 完成编译，并确认 Hot Reload 插件已经被正常加载。");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine("LoginStatusResponse 反射信息");
            sb.AppendLine("程序集: " + type.Assembly.GetName().Name);
            sb.AppendLine("完整类型名: " + type.FullName);
            sb.AppendLine();

            var constructors = type
                .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .OrderByDescending(ctor => ctor.IsPublic)
                .ThenBy(ctor => ctor.GetParameters().Length)
                .ToArray();

            if (constructors.Length == 0) {
                sb.AppendLine("构造函数: 无");
            } else {
                sb.AppendLine("构造函数:");
                foreach (var ctor in constructors) {
                    var parameters = ctor.GetParameters();
                    var signature = string.Join(", ", parameters.Select(FormatParameter));
                    sb.AppendLine($"- {(ctor.IsPublic ? "public" : "non-public")} {type.Name}({signature})");
                }
            }

            sb.AppendLine();
            sb.AppendLine("字段:");
            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                         .OrderBy(field => field.Name)) {
                sb.AppendLine($"- {FormatTypeName(field.FieldType)} {field.Name}");
            }

            sb.AppendLine();
            sb.AppendLine("属性:");
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                         .OrderBy(property => property.Name)) {
                sb.AppendLine($"- {FormatTypeName(property.PropertyType)} {property.Name}");
            }

            Debug.Log(sb.ToString());
        }

        [MenuItem("Tools/Hot Reload/运行反射抓取 LoginStatusResponse")]
        private static void RunLiveReflectionProbe() {
            _ = RunLiveReflectionProbeAsync();
        }

        private static async Task RunLiveReflectionProbeAsync() {
            try {
                var requestHelperType = FindType("SingularityGroup.HotReload.RequestHelper");
                if (requestHelperType == null) {
                    Debug.LogWarning("未找到 RequestHelper，暂时无法发起运行时探测。");
                    return;
                }

                var method = requestHelperType.GetMethod(
                    "GetLoginStatus",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                    null,
                    new[] { typeof(int) },
                    null);

                if (method == null) {
                    Debug.LogWarning("未找到 RequestHelper.GetLoginStatus(int) 方法。");
                    return;
                }

                var taskObject = method.Invoke(null, new object[] { 10 });
                if (taskObject is not Task task) {
                    Debug.LogWarning("GetLoginStatus 没有返回 Task，无法继续探测。");
                    return;
                }

                await task;

                var result = GetTaskResult(task);
                if (result == null) {
                    Debug.LogWarning("GetLoginStatus 返回了 null，当前无法从实例反射构造签名。");
                    return;
                }

                var type = result.GetType();
                var sb = new StringBuilder();
                sb.AppendLine("LoginStatusResponse 实例反射结果");
                sb.AppendLine("程序集: " + type.Assembly.GetName().Name);
                sb.AppendLine("完整类型名: " + type.FullName);
                sb.AppendLine();

                var constructors = type
                    .GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .OrderByDescending(ctor => ctor.IsPublic)
                    .ThenBy(ctor => ctor.GetParameters().Length)
                    .ToArray();

                if (constructors.Length == 0) {
                    sb.AppendLine("构造函数: 无");
                } else {
                    sb.AppendLine("构造函数:");
                    foreach (var ctor in constructors) {
                        var parameters = string.Join(", ", ctor.GetParameters().Select(FormatParameter));
                        sb.AppendLine($"- {(ctor.IsPublic ? "public" : "non-public")} {type.Name}({parameters})");
                    }
                }

                sb.AppendLine();
                sb.AppendLine("字段当前值:");
                foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                             .OrderBy(field => field.Name)) {
                    object value;
                    try {
                        value = field.GetValue(result);
                    } catch (Exception ex) {
                        value = "<读取失败: " + ex.Message + ">";
                    }
                    sb.AppendLine($"- {FormatTypeName(field.FieldType)} {field.Name} = {FormatRuntimeValue(value)}");
                }

                Debug.Log(sb.ToString());
            } catch (Exception ex) {
                Debug.LogException(ex);
            }
        }

        private static Type FindType(string fullName) {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                var type = assembly.GetType(fullName, false);
                if (type != null) {
                    return type;
                }
            }

            var runtimeDependenciesAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => assembly.GetName().Name == HotReloadRuntimeDependenciesAssemblyName);

            return runtimeDependenciesAssembly?.GetType(fullName, false);
        }

        private static string FormatParameter(ParameterInfo parameter) {
            var defaultValueText = parameter.HasDefaultValue
                ? " = " + FormatDefaultValue(parameter.DefaultValue)
                : string.Empty;

            return $"{FormatTypeName(parameter.ParameterType)} {parameter.Name}{defaultValueText}";
        }

        private static string FormatTypeName(Type type) {
            if (type == typeof(void)) {
                return "void";
            }

            if (type == typeof(bool)) {
                return "bool";
            }

            if (type == typeof(int)) {
                return "int";
            }

            if (type == typeof(float)) {
                return "float";
            }

            if (type == typeof(string)) {
                return "string";
            }

            if (type == typeof(DateTime)) {
                return "DateTime";
            }

            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>)) {
                return FormatTypeName(type.GetGenericArguments()[0]) + "?";
            }

            if (type.IsArray) {
                return FormatTypeName(type.GetElementType() ?? typeof(object)) + "[]";
            }

            return type.Name;
        }

        private static string FormatDefaultValue(object value) {
            if (value == null || value == DBNull.Value) {
                return "null";
            }

            if (value is string text) {
                return "\"" + text + "\"";
            }

            if (value is bool flag) {
                return flag ? "true" : "false";
            }

            return value.ToString();
        }

        private static string FormatRuntimeValue(object value) {
            if (value == null) {
                return "null";
            }

            if (value is string text) {
                return "\"" + text + "\"";
            }

            if (value is bool flag) {
                return flag ? "true" : "false";
            }

            if (value is DateTime time) {
                return time.ToString("O");
            }

            return value.ToString();
        }

        private static object GetTaskResult(Task task) {
            var resultProperty = task.GetType().GetProperty("Result", BindingFlags.Public | BindingFlags.Instance);
            return resultProperty?.GetValue(task);
        }
    }
}
