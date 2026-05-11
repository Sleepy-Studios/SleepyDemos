using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace SleepyDemos.Editor {
    public class HotReloadLicenseInfoWindow : EditorWindow {
        private const string HotReloadEditorAssemblyName = "SingularityGroup.HotReload.Editor";
        private const string HotReloadRuntimeAssemblyName = "SingularityGroup.HotReload.Runtime";
        private const string EditorCodePatcherTypeName = "SingularityGroup.HotReload.Editor.EditorCodePatcher";
        private const string RequestHelperTypeName = "SingularityGroup.HotReload.RequestHelper";
        private const string TrialKeyPath = @"C:\Users\User\AppData\Local\LicenseSpring\codepatchertrial\License.key";
        private const string SimulationModePrefsKey = "SleepyDemos.HotReloadLicenseInfoWindow.SimulationMode";
        private const string EditorCodePatcherRelativePath = "Packages/com.singularitygroup.hotreload/Editor/EditorCodePatcher.cs";
        private const string HandleStatusSignature = "internal static void HandleStatus(LoginStatusResponse resp)";
        private const string SimulationBlockStartMarker = "            // SleepyDemos.HandleStatusSimulationBlockStart";
        private const string SimulationBlockEndMarker = "            // SleepyDemos.HandleStatusSimulationBlockEnd";

        private enum SimulationMode {
            Off = 0,
            Expired = 1,
            LongTrial = 2
        }

        private static readonly string[] BoolNames = {
            "isLicensed",
            "isTrial",
            "isFree",
            "isIndieLicense",
            "isBusinessLicense",
            "freeSessionRunning",
            "freeSessionFinished"
        };

        private Vector2 _scroll;
        private Snapshot _snapshot;

        [MenuItem("Tools/Hot Reload/授权信息查看器")]
        public static void Open() {
            var window = GetWindow<HotReloadLicenseInfoWindow>();
            window.titleContent = new GUIContent("Hot Reload 授权");
            window.minSize = new Vector2(560f, 520f);
            window.Show();
        }

        private void OnEnable() {
            RefreshSnapshot();
        }

        private void OnInspectorUpdate() {
            ApplyPersistentSimulation();
            Repaint();
        }

        private void OnGUI() {
            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Hot Reload 授权信息", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("如果授权字段为空或全是“否”，先点“从服务器刷新状态”。这个按钮会直接请求 Hot Reload 的 /status 接口，并把返回值写回插件内部 Status。", MessageType.Info);

            EditorGUILayout.Space(6f);
            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("从服务器刷新状态", GUILayout.Height(28f))) {
                    RequestStatusRefresh();
                }

                if (GUILayout.Button("尝试启动 Hot Reload", GUILayout.Height(28f))) {
                    StartHotReload();
                }

                if (GUILayout.Button("仅重读本地状态", GUILayout.Height(28f))) {
                    RefreshSnapshot();
                }
            }

            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("持续模拟试用很久", GUILayout.Height(28f))) {
                    SetSimulationMode(SimulationMode.LongTrial);
                }

                if (GUILayout.Button("持续模拟已过期", GUILayout.Height(28f))) {
                    SetSimulationMode(SimulationMode.Expired);
                }
            }

            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("停止模拟并恢复真实状态", GUILayout.Height(28f))) {
                    SetSimulationMode(SimulationMode.Off);
                    RequestStatusRefresh();
                }
            }

            EditorGUILayout.Space(10f);
            DrawSection("插件状态", new[] {
                Line("编辑器程序集", _snapshot.editorAssemblyFound ? "已加载" : "未加载"),
                Line("运行时程序集", _snapshot.runtimeAssemblyFound ? "已加载" : "未加载"),
                Line("Hot Reload 运行中", ToYesNo(_snapshot.running)),
                Line("当前状态对象", _snapshot.statusExists ? "已获取" : "未获取"),
                Line("状态来源", _snapshot.statusSource),
                Line("持续模拟", GetSimulationModeLabel(GetSimulationMode())),
                Line("最后刷新", _snapshot.lastRefreshText)
            });

            DrawSection("授权概览", new[] {
                Line("授权状态", _snapshot.licenseSummary),
                Line("是否已授权", ToYesNo(_snapshot.GetBool("isLicensed"))),
                Line("是否试用", ToYesNo(_snapshot.GetBool("isTrial"))),
                Line("是否免费时段", ToYesNo(_snapshot.GetBool("isFree"))),
                Line("是否 Indie 授权", ToYesNo(_snapshot.GetBool("isIndieLicense"))),
                Line("是否 Business 授权", ToYesNo(_snapshot.GetBool("isBusinessLicense"))),
                Line("免费时段运行中", ToYesNo(_snapshot.GetBool("freeSessionRunning"))),
                Line("免费时段已结束", ToYesNo(_snapshot.GetBool("freeSessionFinished"))),
                Line("到期时间", _snapshot.licenseExpiresAtText),
                Line("剩余时间", _snapshot.remainingText),
                Line("免费时段结束", _snapshot.freeSessionEndTimeText),
                Line("免费时段剩余", _snapshot.freeSessionRemainingText)
            });

            DrawSection("本机试用 Key", new[] {
                Line("本机试用 Key", _snapshot.trialKeyExists ? "已存在" : "不存在"),
                Line("推测开始时间", _snapshot.trialKeyCreatedAtText),
                Line("Key 路径", TrialKeyPath)
            });

            if (!string.IsNullOrEmpty(_snapshot.lastLicenseError)) {
                EditorGUILayout.Space(8f);
                EditorGUILayout.HelpBox("授权错误：" + _snapshot.lastLicenseError, MessageType.Warning);
            }

            if (!string.IsNullOrEmpty(_snapshot.requestError)) {
                EditorGUILayout.Space(4f);
                EditorGUILayout.HelpBox("请求错误：" + _snapshot.requestError, MessageType.Warning);
            }

            if (!string.IsNullOrEmpty(_snapshot.note)) {
                EditorGUILayout.Space(4f);
                EditorGUILayout.HelpBox(_snapshot.note, MessageType.None);
            }

            if (!string.IsNullOrEmpty(_snapshot.rawStatusDump)) {
                EditorGUILayout.Space(8f);
                EditorGUILayout.LabelField("原始状态字段", EditorStyles.boldLabel);
                EditorGUILayout.TextArea(_snapshot.rawStatusDump, GUILayout.MinHeight(130f));
            }

            EditorGUILayout.Space(12f);
            EditorGUILayout.EndScrollView();
        }

        private void DrawSection(string title, string[] lines) {
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox)) {
                foreach (var line in lines) {
                    EditorGUILayout.SelectableLabel(line, EditorStyles.label, GUILayout.Height(18f));
                }
            }
            EditorGUILayout.Space(8f);
        }

        private static string Line(string label, string value) {
            return label + "：" + value;
        }

        private static string ToYesNo(bool value) {
            return value ? "是" : "否";
        }

        private void RefreshSnapshot() {
            var snapshot = CreateBaseSnapshot();
            var editorCodePatcherType = GetTypeFromAssembly(HotReloadEditorAssemblyName, EditorCodePatcherTypeName);

            if (editorCodePatcherType == null) {
                snapshot.note = "未找到 Hot Reload 的 EditorCodePatcher 类型。请确认插件已被 Unity 正常加载。";
                SetUnknownLicenseFields(ref snapshot);
                _snapshot = snapshot;
                return;
            }

            snapshot.running = GetStaticBool(editorCodePatcherType, "Running");
            var status = GetStaticMemberValue(editorCodePatcherType, "Status");
            ApplyStatus(ref snapshot, status, "插件内部 Status");
            _snapshot = snapshot;
        }

        private Snapshot CreateBaseSnapshot() {
            var snapshot = new Snapshot {
                lastRefreshText = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                editorAssemblyFound = FindAssembly(HotReloadEditorAssemblyName) != null,
                runtimeAssemblyFound = FindAssembly(HotReloadRuntimeAssemblyName) != null,
                trialKeyExists = File.Exists(TrialKeyPath)
            };

            snapshot.trialKeyCreatedAtText = snapshot.trialKeyExists
                ? File.GetCreationTime(TrialKeyPath).ToString("yyyy-MM-dd HH:mm:ss")
                : "未找到";

            return snapshot;
        }

        private void RequestStatusRefresh() {
            var taskObject = InvokeRequestHelperMethod("GetLoginStatus", new object[] { 10 }) as Task;
            if (taskObject == null) {
                InvokeEditorCodePatcherMethod("RequestServerInfo");
                RefreshSnapshot();
                return;
            }

            taskObject.ContinueWith(task => {
                EditorApplication.delayCall += () => {
                    var snapshot = CreateBaseSnapshot();
                    snapshot.running = GetStaticBool(GetTypeFromAssembly(HotReloadEditorAssemblyName, EditorCodePatcherTypeName), "Running");

                    object status = null;
                    if (task.Exception == null) {
                        status = GetTaskResult(task);
                        var editorCodePatcherType = GetTypeFromAssembly(HotReloadEditorAssemblyName, EditorCodePatcherTypeName);
                        InvokeEditorCodePatcherMethod("HandleStatus", new[] { status });
                        ApplyPersistentSimulation();
                        status = GetStaticMemberValue(editorCodePatcherType, "Status");
                    } else {
                        snapshot.requestError = task.Exception.GetBaseException().Message;
                    }

                    ApplyStatus(ref snapshot, status, "服务器 /status");
                    _snapshot = snapshot;
                    Repaint();
                };
            });
        }

        private void StartHotReload() {
            var taskObject = InvokeEditorCodePatcherMethod("DownloadAndRun", new object[] { null, false }) as Task;
            if (taskObject == null) {
                RefreshSnapshot();
                return;
            }

            taskObject.ContinueWith(_ => {
                EditorApplication.delayCall += () => {
                    RequestStatusRefresh();
                    RefreshSnapshot();
                };
            });
        }

        private void SetSimulationMode(SimulationMode mode) {
            EditorPrefs.SetInt(SimulationModePrefsKey, (int)mode);
            if (mode == SimulationMode.Off) {
                RestoreHandleStatusLogPatch();
                RefreshSnapshot();
                _snapshot.note = "已停止持续模拟。点击“从服务器刷新状态”可立即恢复真实状态。";
                Repaint();
                return;
            }

            ApplyHandleStatusLogPatch(mode);
            ApplyPersistentSimulation();
        }

        private static SimulationMode GetSimulationMode() {
            return (SimulationMode)EditorPrefs.GetInt(SimulationModePrefsKey, (int)SimulationMode.Off);
        }

        private static string GetSimulationModeLabel(SimulationMode mode) {
            switch (mode) {
                case SimulationMode.Expired:
                    return "试用已过期";
                case SimulationMode.LongTrial:
                    return "试用很久";
                default:
                    return "关闭";
            }
        }

        private void ApplyPersistentSimulation() {
            var mode = GetSimulationMode();
            if (mode == SimulationMode.Off) {
                return;
            }

            ApplySimulationMode(mode, true);
        }

        private void ApplySimulationMode(SimulationMode mode, bool silent) {
            var editorCodePatcherType = GetTypeFromAssembly(HotReloadEditorAssemblyName, EditorCodePatcherTypeName);
            var status = GetStaticMemberValue(editorCodePatcherType, "Status");
            if (status == null) {
                if (!silent) {
                    RefreshSnapshot();
                    _snapshot.note = "当前没有可修改的 Status。先启动 Hot Reload 并刷新一次状态，再启用持续模拟。";
                    Repaint();
                }
                return;
            }

            if (mode == SimulationMode.LongTrial) {
                SetLongTrialStatus(status);
            } else if (mode == SimulationMode.Expired) {
                SetExpiredStatus(status);
            }

            InvokeEditorCodePatcherMethod("HandleStatus", new[] { status });
            if (!silent) {
                RefreshSnapshot();
                _snapshot.note = "已启用持续模拟：" + GetSimulationModeLabel(mode) + "。该模拟会在当前 Unity 会话内反复覆盖 Status，直到停止模拟。";
                Repaint();
            }
        }

        private static void ApplyHandleStatusLogPatch(SimulationMode mode) {
            var sourcePath = GetEditorCodePatcherPath();
            if (!File.Exists(sourcePath)) {
                return;
            }

            var source = File.ReadAllText(sourcePath);
            var lineEnding = source.Contains("\r\n") ? "\r\n" : "\n";
            source = RemoveSimulationBlock(source);

            var insertIndex = FindHandleStatusInsertIndex(source);
            if (insertIndex < 0) {
                return;
            }

            source = source.Insert(insertIndex, BuildHandleStatusSimulationBlock(mode, lineEnding));
            WriteSourceAndRecompile(sourcePath, source);
        }

        private static void RestoreHandleStatusLogPatch() {
            var sourcePath = GetEditorCodePatcherPath();
            if (!File.Exists(sourcePath)) {
                return;
            }

            var source = File.ReadAllText(sourcePath);
            if (!source.Contains(SimulationBlockStartMarker)) {
                return;
            }

            source = RemoveSimulationBlock(source);
            WriteSourceAndRecompile(sourcePath, source);
        }

        private static string GetEditorCodePatcherPath() {
            return Path.Combine(Directory.GetCurrentDirectory(), EditorCodePatcherRelativePath);
        }

        private static int FindHandleStatusInsertIndex(string source) {
            var methodIndex = source.IndexOf(HandleStatusSignature, StringComparison.Ordinal);
            if (methodIndex < 0) {
                return -1;
            }

            var nullCheckIndex = source.IndexOf("if (resp == null)", methodIndex, StringComparison.Ordinal);
            if (nullCheckIndex < 0) {
                return -1;
            }

            var lineStart = source.LastIndexOf('\n', nullCheckIndex);
            return lineStart < 0 ? nullCheckIndex : lineStart + 1;
        }

        private static string BuildHandleStatusSimulationBlock(SimulationMode mode, string lineEnding) {
            var expiresAtExpression = mode == SimulationMode.Expired
                ? "DateTime.Now.AddDays(-1)"
                : "DateTime.Now.AddYears(100)";

            if (mode == SimulationMode.Expired) {
                return BuildLoginStatusResponseBlock(lineEnding, "Expired simulation block.", expiresAtExpression);
            }

            return BuildLoginStatusResponseBlock(lineEnding, "Long-trial simulation block.", expiresAtExpression);
        }

        private static string BuildLoginStatusResponseBlock(string lineEnding, string title, string expiresAtExpression) {
            return string.Join(lineEnding, new[] {
                SimulationBlockStartMarker,
                "            // " + title + " Replace everything between the markers if needed.",
                "            resp = new LoginStatusResponse(",
                "                isLicensed: true,",
                "                lastLicenseError: null,",
                "                isBusinessLicense: false,",
                "                isTrial: true,",
                "                isExpired: false,",
                "                licenseExpiresAt: " + expiresAtExpression + ",",
                "                startupProgress: 1f,",
                "                startupStatus: null,",
                "                isIndieLicense: true,",
                "                isFree: false,",
                "                consumptionsUnavailableReason: default,",
                "                freeSessionRunning: false,",
                "                freeSessionEndTime: null,",
                "                hardwareId: \"simulated-license\",",
                "                requestError: null,",
                "                initialPassword: null,",
                "                canResetRemotely: false",
                "            );",
                SimulationBlockEndMarker,
                string.Empty
            });
        }

        private static string RemoveSimulationBlock(string source) {
            var startIndex = source.IndexOf(SimulationBlockStartMarker, StringComparison.Ordinal);
            if (startIndex < 0) {
                return source;
            }

            var lineStart = source.LastIndexOf('\n', startIndex);
            lineStart = lineStart < 0 ? 0 : lineStart + 1;

            var endIndex = source.IndexOf(SimulationBlockEndMarker, startIndex, StringComparison.Ordinal);
            if (endIndex < 0) {
                return source.Substring(0, lineStart);
            }

            var lineEnd = source.IndexOf('\n', endIndex);
            return lineEnd < 0
                ? source.Substring(0, lineStart)
                : source.Remove(lineStart, lineEnd - lineStart + 1);
        }

        private static void WriteSourceAndRecompile(string sourcePath, string source) {
            File.WriteAllText(sourcePath, source);
            AssetDatabase.ImportAsset(EditorCodePatcherRelativePath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        }

        private static void SetExpiredStatus(object status) {
            SetMemberValue(status, "isLicensed", false);
            SetMemberValue(status, "isTrial", true);
            SetMemberValue(status, "isFree", false);
            SetMemberValue(status, "isIndieLicense", false);
            SetMemberValue(status, "isBusinessLicense", false);
            SetMemberValue(status, "freeSessionRunning", false);
            SetMemberValue(status, "freeSessionFinished", true);
            SetMemberValue(status, "licenseExpiresAt", DateTime.Now.AddDays(-1));
            SetMemberValue(status, "freeSessionEndTime", DateTime.Now.AddMinutes(-1));
            SetMemberValue(status, "lastLicenseError", "TrialLicenseExpiredException");
            SetMemberValue(status, "requestError", null);
        }

        private static void SetLongTrialStatus(object status) {
            SetMemberValue(status, "isLicensed", true);
            SetMemberValue(status, "isTrial", true);
            SetMemberValue(status, "isFree", false);
            SetMemberValue(status, "isIndieLicense", false);
            SetMemberValue(status, "isBusinessLicense", false);
            SetMemberValue(status, "freeSessionRunning", false);
            SetMemberValue(status, "freeSessionFinished", false);
            SetMemberValue(status, "licenseExpiresAt", DateTime.Now.AddYears(100));
            SetMemberValue(status, "freeSessionEndTime", null);
            SetMemberValue(status, "lastLicenseError", null);
            SetMemberValue(status, "requestError", null);
        }

        private static void ApplyStatus(ref Snapshot snapshot, object status, string source) {
            snapshot.statusSource = source;
            snapshot.statusExists = status != null;

            if (status == null) {
                snapshot.licenseSummary = snapshot.running ? "未获取授权状态" : "Hot Reload 未启动";
                snapshot.licenseExpiresAtText = "未获取";
                snapshot.remainingText = "未获取";
                snapshot.freeSessionEndTimeText = "未获取";
                snapshot.freeSessionRemainingText = "未获取";
                snapshot.note = "当前没有可读取的 loginStatus。请先启动 Hot Reload，或点击“从服务器刷新状态”。";
                return;
            }

            foreach (var name in BoolNames) {
                snapshot.SetBool(name, GetBool(status, name));
            }

            snapshot.lastLicenseError = GetString(status, "lastLicenseError");
            snapshot.requestError = GetString(status, "requestError");

            var licenseExpiresAt = GetDateTime(status, "licenseExpiresAt");
            var freeSessionEndTime = GetDateTime(status, "freeSessionEndTime");

            snapshot.licenseSummary = BuildLicenseSummary(snapshot);
            snapshot.licenseExpiresAtText = FormatDateTime(licenseExpiresAt);
            snapshot.remainingText = FormatRemaining(licenseExpiresAt);
            snapshot.freeSessionEndTimeText = FormatDateTime(freeSessionEndTime);
            snapshot.freeSessionRemainingText = FormatRemaining(freeSessionEndTime);
            snapshot.rawStatusDump = DumpObject(status);
            snapshot.note = "这里显示的是 Hot Reload 当前运行时授权状态；原始状态字段也已展开，方便继续定位插件实际返回了什么。";
        }

        private static void SetUnknownLicenseFields(ref Snapshot snapshot) {
            snapshot.licenseSummary = "未知";
            snapshot.licenseExpiresAtText = "未知";
            snapshot.remainingText = "未知";
            snapshot.freeSessionEndTimeText = "未知";
            snapshot.freeSessionRemainingText = "未知";
        }

        private object InvokeEditorCodePatcherMethod(string methodName, object[] args = null) {
            var type = GetTypeFromAssembly(HotReloadEditorAssemblyName, EditorCodePatcherTypeName);
            return InvokeStaticMethod(type, methodName, args);
        }

        private object InvokeRequestHelperMethod(string methodName, object[] args = null) {
            var type = GetTypeFromAssembly(HotReloadRuntimeAssemblyName, RequestHelperTypeName);
            return InvokeStaticMethod(type, methodName, args);
        }

        private static object InvokeStaticMethod(Type type, string methodName, object[] args = null) {
            if (type == null) {
                return null;
            }

            var method = type.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(candidate => candidate.Name == methodName && candidate.GetParameters().Length == (args == null ? 0 : args.Length));
            return method == null ? null : method.Invoke(null, args);
        }

        private static Assembly FindAssembly(string assemblyName) {
            return AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => assembly.GetName().Name == assemblyName);
        }

        private static Type GetTypeFromAssembly(string assemblyName, string typeName) {
            return FindAssembly(assemblyName)?.GetType(typeName);
        }

        private static object GetStaticMemberValue(Type type, string memberName) {
            if (type == null) {
                return null;
            }

            var property = type.GetProperty(memberName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null) {
                return property.GetValue(null);
            }

            var field = type.GetField(memberName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            return field == null ? null : field.GetValue(null);
        }

        private static bool GetStaticBool(Type type, string memberName) {
            var value = GetStaticMemberValue(type, memberName);
            return value is bool flag && flag;
        }

        private static object GetMemberValue(object instance, string memberName) {
            if (instance == null) {
                return null;
            }

            var type = instance.GetType();
            var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null) {
                return property.GetValue(instance);
            }

            var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field == null ? null : field.GetValue(instance);
        }

        private static bool SetMemberValue(object instance, string memberName, object value) {
            if (instance == null) {
                return false;
            }

            var type = instance.GetType();
            var property = type.GetProperty(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.CanWrite) {
                property.SetValue(instance, ConvertValue(value, property.PropertyType));
                return true;
            }

            var field = type.GetField(memberName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) {
                return false;
            }

            field.SetValue(instance, ConvertValue(value, field.FieldType));
            return true;
        }

        private static object ConvertValue(object value, Type targetType) {
            if (value == null) {
                return null;
            }

            var nullableType = Nullable.GetUnderlyingType(targetType);
            if (nullableType != null) {
                targetType = nullableType;
            }

            if (targetType.IsInstanceOfType(value)) {
                return value;
            }

            return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
        }

        private static bool GetBool(object instance, string memberName) {
            var value = GetMemberValue(instance, memberName);
            return value is bool flag && flag;
        }

        private static string GetString(object instance, string memberName) {
            return GetMemberValue(instance, memberName) as string;
        }

        private static DateTime? GetDateTime(object instance, string memberName) {
            var value = GetMemberValue(instance, memberName);
            if (value == null) {
                return null;
            }

            if (value is DateTime dateTime) {
                return dateTime;
            }

            if (DateTime.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out var parsed)) {
                return parsed;
            }

            return null;
        }

        private static object GetTaskResult(Task task) {
            var resultProperty = task.GetType().GetProperty("Result", BindingFlags.Instance | BindingFlags.Public);
            return resultProperty == null ? null : resultProperty.GetValue(task);
        }

        private static string BuildLicenseSummary(Snapshot snapshot) {
            if (snapshot.GetBool("isBusinessLicense")) {
                return "Business 版";
            }

            if (snapshot.GetBool("isIndieLicense")) {
                return "Indie 版";
            }

            if (snapshot.GetBool("isTrial")) {
                return "试用版";
            }

            if (snapshot.GetBool("isFree")) {
                return "免费时段";
            }

            if (!string.IsNullOrEmpty(snapshot.lastLicenseError)) {
                return "授权异常";
            }

            if (snapshot.GetBool("isLicensed")) {
                return "已授权";
            }

            return "未授权";
        }

        private static string FormatDateTime(DateTime? value) {
            return value.HasValue ? value.Value.ToString("yyyy-MM-dd HH:mm:ss") : "未提供";
        }

        private static string FormatRemaining(DateTime? endTime) {
            if (!endTime.HasValue) {
                return "未提供";
            }

            var remaining = endTime.Value - DateTime.Now;
            if (remaining.TotalSeconds <= 0d) {
                return "已到期";
            }

            if (remaining.TotalDays >= 1d) {
                return string.Format("{0}天 {1}小时", Math.Floor(remaining.TotalDays), remaining.Hours);
            }

            if (remaining.TotalHours >= 1d) {
                return string.Format("{0}小时 {1}分钟", Math.Floor(remaining.TotalHours), remaining.Minutes);
            }

            return string.Format("{0}分钟", Math.Max(1d, Math.Ceiling(remaining.TotalMinutes)));
        }

        private static string DumpObject(object instance) {
            if (instance == null) {
                return string.Empty;
            }

            var builder = new StringBuilder();
            var type = instance.GetType();
            builder.AppendLine(type.FullName);

            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).OrderBy(field => field.Name)) {
                builder.AppendLine(field.Name + " = " + FormatValue(field.GetValue(instance)));
            }

            foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).OrderBy(property => property.Name)) {
                if (property.GetIndexParameters().Length != 0) {
                    continue;
                }

                try {
                    builder.AppendLine(property.Name + " = " + FormatValue(property.GetValue(instance)));
                } catch {
                    builder.AppendLine(property.Name + " = <读取失败>");
                }
            }

            return builder.ToString();
        }

        private static string FormatValue(object value) {
            if (value == null) {
                return "null";
            }

            if (value is string text) {
                return text;
            }

            if (value is IEnumerable enumerable && !(value is string)) {
                return string.Join(", ", enumerable.Cast<object>().Select(item => item == null ? "null" : item.ToString()).ToArray());
            }

            return value.ToString();
        }

        private struct Snapshot {
            public bool editorAssemblyFound;
            public bool runtimeAssemblyFound;
            public bool running;
            public bool statusExists;
            public bool trialKeyExists;
            public string licenseSummary;
            public string licenseExpiresAtText;
            public string remainingText;
            public string freeSessionEndTimeText;
            public string freeSessionRemainingText;
            public string trialKeyCreatedAtText;
            public string lastLicenseError;
            public string requestError;
            public string note;
            public string lastRefreshText;
            public string rawStatusDump;
            public string statusSource;
            private bool _isLicensed;
            private bool _isTrial;
            private bool _isFree;
            private bool _isIndieLicense;
            private bool _isBusinessLicense;
            private bool _freeSessionRunning;
            private bool _freeSessionFinished;

            public bool GetBool(string name) {
                switch (name) {
                    case "isLicensed":
                        return _isLicensed;
                    case "isTrial":
                        return _isTrial;
                    case "isFree":
                        return _isFree;
                    case "isIndieLicense":
                        return _isIndieLicense;
                    case "isBusinessLicense":
                        return _isBusinessLicense;
                    case "freeSessionRunning":
                        return _freeSessionRunning;
                    case "freeSessionFinished":
                        return _freeSessionFinished;
                    default:
                        return false;
                }
            }

            public void SetBool(string name, bool value) {
                switch (name) {
                    case "isLicensed":
                        _isLicensed = value;
                        break;
                    case "isTrial":
                        _isTrial = value;
                        break;
                    case "isFree":
                        _isFree = value;
                        break;
                    case "isIndieLicense":
                        _isIndieLicense = value;
                        break;
                    case "isBusinessLicense":
                        _isBusinessLicense = value;
                        break;
                    case "freeSessionRunning":
                        _freeSessionRunning = value;
                        break;
                    case "freeSessionFinished":
                        _freeSessionFinished = value;
                        break;
                }
            }
        }
    }
}
