using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hotfix.BlockPorters;
using UnityEditor;
using UnityEngine;

namespace Hotfix.Editor.BlockPorters
{
    public sealed class BlockPortersLevelEditorWindow : EditorWindow
    {
        private sealed class WorkResult
        {
            public PorterImageResult Image;
            public PorterGeneratedLevel Generated;
            public PorterAnalysis Analysis;
        }
        [SerializeField] private BlockPortersRecipe recipe;
        [SerializeField] private BlockPortersLevelCatalog catalog;
        private Vector2 scroll;
        private PorterBrush brush;
        private int selectedColor;
        private int mergeTarget;
        private int lastBrushIndex = -1;
        private bool highlight = true;
        private PorterAnalysis analysis;
        private int analysisRevision = -1;
        private int editEpoch;
        private int workEpoch;
        private int workRevision;
        private BlockPortersRecipe workRecipe;
        private Task<WorkResult> work;
        private CancellationTokenSource cancellation;
        private readonly Queue<string> batch = new();
        private readonly List<string> batchReport = new();
        private bool isBatch;
        private bool isCanceling;
        private string message = "导入图片或打开已有配方开始编辑。";

        [MenuItem("Tools/SleepyDemos/小人搬砖/关卡编辑器")]
        private static void Open()
        {
            var window = GetWindow<BlockPortersLevelEditorWindow>("搬砖关卡工作台");
            if (Selection.activeObject is BlockPortersRecipe selected)
            { window.recipe = selected; window.editEpoch++; window.analysis = null; }
        }
        private void OnEnable() { minSize = new Vector2(820, 640); Undo.undoRedoPerformed += OnUndo; EditorApplication.update += Poll; }
        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndo; EditorApplication.update -= Poll;
            cancellation?.Cancel(); cancellation?.Dispose();
            // 关闭窗口后任务只持有像素与规则快照，不会写入资产。
            if (work != null) _ = work.ContinueWith(t => { _ = t.Exception; });
        }
        private void OnUndo() { editEpoch++; analysis = null; analysisRevision = -1; Repaint(); }
        private void Changed() { recipe.Invalidate(); editEpoch++; analysis = null; analysisRevision = -1; EditorUtility.SetDirty(recipe); }

        private void OnGUI()
        {
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.LabelField("图片 → 图案编辑 → 队伍策略 → 验证导出", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("原图与配方保存在 Assets/Settings/BlockPorters，不进入运行包。重新转换会替换手工图案；仅安排队伍保留图案和锁定区域。", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope())
            {
                var nextRecipe = (BlockPortersRecipe)EditorGUILayout.ObjectField("编辑配方", recipe, typeof(BlockPortersRecipe), false);
                if (nextRecipe != recipe) { recipe = nextRecipe; editEpoch++; analysis = null; }
                if (GUILayout.Button("导入单图", GUILayout.Width(90)))
                {
                    string path = EditorUtility.OpenFilePanel("导入 PNG/JPG", "", "png,jpg,jpeg");
                    if (!string.IsNullOrEmpty(path)) RunSafe(() => { recipe = BlockPortersWorkbenchIO.Import(path); editEpoch++; analysis = null; });
                }
                if (GUILayout.Button("批量目录", GUILayout.Width(90)))
                {
                    string folder = EditorUtility.OpenFolderPanel("批量图片目录", "", "");
                    if (!string.IsNullOrEmpty(folder)) BeginBatch(Directory.GetFiles(folder).Where(IsImage));
                }
            }
            catalog = (BlockPortersLevelCatalog)EditorGUILayout.ObjectField("导出关卡集", catalog, typeof(BlockPortersLevelCatalog), false);
            var dropRect = GUILayoutUtility.GetRect(0, 38, GUILayout.ExpandWidth(true));
            GUI.Box(dropRect, "拖入一张或多张项目图片 / 外部 PNG、JPG（多张逐图生成并导出）");
            HandleDrop(dropRect);
            if (recipe != null)
            {
                var source = (Texture2D)EditorGUILayout.ObjectField("原图", recipe.Source, typeof(Texture2D), false);
                if (source != recipe.Source) RunSafe(() =>
                {
                    Undo.RecordObject(recipe, "更换原图");
                    recipe.SetSource(source == null ? null : BlockPortersWorkbenchIO.CopySource(AssetDatabase.GetAssetPath(source))); Changed();
                });
                var serialized = new SerializedObject(recipe); serialized.Update();
                EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(serialized.FindProperty("displayName"), new GUIContent("关卡显示名"));
                EditorGUILayout.PropertyField(serialized.FindProperty("settings"), new GUIContent("裁剪 / 网格 / 颜色 / 难度参数"), true);
                if (EditorGUI.EndChangeCheck()) { serialized.ApplyModifiedProperties(); Changed(); }
                using (new EditorGUILayout.HorizontalScope())
                {
                    foreach (int size in new[] { 16, 24, 32 })
                        if (GUILayout.Button($"{size}×{size}")) { Undo.RecordObject(recipe, "棋盘尺寸"); recipe.Settings.Width = recipe.Settings.Height = size; Changed(); }
                    foreach (PorterDifficulty difficulty in Enum.GetValues(typeof(PorterDifficulty)))
                        if (GUILayout.Button(difficulty == PorterDifficulty.Easy ? "简单预设" : difficulty == PorterDifficulty.Normal ? "普通预设" : "困难预设")) SetPreset(difficulty);
                }
                using (new EditorGUI.DisabledScope(work != null))
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("重新转换图片")) RunSafe(() => StartWork(0));
                    if (GUILayout.Button("仅重新安排队伍")) RunSafe(() => StartWork(1));
                    if (GUILayout.Button("分析当前队列")) RunSafe(() => StartWork(2));
                    if (GUILayout.Button("保存配方")) { EditorUtility.SetDirty(recipe); AssetDatabase.SaveAssets(); }
                }
                DrawPreviews();
                if (recipe.Cells.Length > 0)
                {
                    DrawPalette(); DrawQueues();
                    if (analysis != null && analysisRevision == recipe.Revision)
                    {
                        EditorGUILayout.HelpBox($"{analysis.State} · {analysis.Message}\n状态 {analysis.States}，关键选择 {analysis.CriticalChoices}，峰值等待队伍 {analysis.MaxWaitingTeams}，立即堵塞分支 {analysis.DeadlockChoices}\n随机 {analysis.RandomWins}/{analysis.PolicyRuns / 2}，贪心 {analysis.GreedyWins}/{analysis.PolicyRuns / 2}（模拟估计）\n参考解：{string.Join(" → ", analysis.Solution.Select(c => c + 1))}", analysis.State == PorterSolvability.Solvable ? MessageType.Info : MessageType.Warning);
                    }
                    else EditorGUILayout.HelpBox("当前内容未验证。手工改色、队列或参数变化后需重新分析。", MessageType.Warning);
                    using (new EditorGUI.DisabledScope(work != null || analysis == null || analysisRevision != recipe.Revision || !BlockPortersLevelGenerator.Matches(recipe.Settings.Difficulty, analysis)))
                        if (GUILayout.Button("导出已验证关卡并加入关卡集")) RunSafe(ExportCurrent);
                }
            }
            if (work != null)
            {
                EditorGUILayout.HelpBox($"正在后台计算 {workRecipe.name}；剩余批量 {batch.Count} 张。每候选最多 200 次策略回放。", MessageType.Info);
                if (GUILayout.Button("取消当前 / 剩余批量")) { isCanceling = true; cancellation.Cancel(); batch.Clear(); message = "取消请求已发送，等待计算退出。"; }
            }
            EditorGUILayout.HelpBox(message, MessageType.None);
            foreach (string line in batchReport) EditorGUILayout.LabelField(line, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndScrollView();
        }

        private void SetPreset(PorterDifficulty difficulty)
        {
            Undo.RecordObject(recipe, "难度预设");
            recipe.Settings.Difficulty = difficulty;
            recipe.Settings.ColorBudget = difficulty == PorterDifficulty.Easy ? 5 : difficulty == PorterDifficulty.Normal ? 8 : 12;
            recipe.Settings.NearGroups = difficulty == PorterDifficulty.Easy ? 0 : difficulty == PorterDifficulty.Normal ? 1 : 2;
            Changed();
        }
        private void DrawPreviews()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope())
                {
                    GUILayout.Label("原图"); var area = GUILayoutUtility.GetRect(230, 250);
                    if (recipe.Source != null) GUI.DrawTexture(area, recipe.Source, ScaleMode.ScaleToFit, true);
                }
                using (new EditorGUILayout.VerticalScope()) { GUILayout.Label("像素化"); DrawGrid(recipe.PixelCells, recipe.PixelPalette, false); }
                using (new EditorGUILayout.VerticalScope()) { GUILayout.Label("难度调整 / 人工编辑"); DrawGrid(recipe.Cells, recipe.Palette, true); }
            }
            highlight = EditorGUILayout.Toggle("高亮改色区域", highlight);
            brush = (PorterBrush)EditorGUILayout.Popup("画笔", (int)brush, new[] { "涂色", "填充", "拾色", "锁定 / 解锁", "擦除背景" });
        }
        private void DrawGrid(int[] cells, Color[] palette, bool editable)
        {
            var area = GUILayoutUtility.GetRect(230, 250);
            int width = recipe.Settings.Width, height = recipe.Settings.Height;
            if (width < 1 || height < 1 || cells.Length != width * height) return;
            float size = Mathf.Min(area.width / width, area.height / height);
            for (int i = 0; i < cells.Length; i++)
            {
                var rect = new Rect(area.x + i % width * size, area.y + (height - 1 - i / width) * size, size - .5f, size - .5f);
                EditorGUI.DrawRect(rect, cells[i] >= 0 && cells[i] < palette.Length ? palette[cells[i]] : new Color(.13f, .13f, .13f));
                if (editable && i < recipe.Locked.Length && recipe.Locked[i]) EditorGUI.DrawRect(new Rect(rect.x, rect.y, size * .3f, size * .3f), Color.black);
                if (editable && highlight && i < recipe.PixelCells.Length && cells[i] >= 0 && recipe.PixelCells[i] >= 0 && cells[i] < palette.Length && recipe.PixelCells[i] < recipe.PixelPalette.Length && BlockPortersImagePipeline.Delta(palette[cells[i]], recipe.PixelPalette[recipe.PixelCells[i]]) > 1)
                    EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 2, rect.width, 2), Color.yellow);
            }
            var ev = Event.current;
            if (!editable || !area.Contains(ev.mousePosition) || ev.button != 0 || (ev.type != EventType.MouseDown && ev.type != EventType.MouseDrag)) return;
            int x = Mathf.FloorToInt((ev.mousePosition.x - area.x) / size), y = height - 1 - Mathf.FloorToInt((ev.mousePosition.y - area.y) / size);
            if (x < 0 || x >= width || y < 0 || y >= height) return;
            int index = y * width + x;
            if (ev.type == EventType.MouseDown) lastBrushIndex = -1;
            if (brush == PorterBrush.Lock && index == lastBrushIndex) { ev.Use(); return; }
            lastBrushIndex = index;
            if (brush == PorterBrush.Pick) { selectedColor = Math.Max(0, cells[index]); ev.Use(); return; }
            Undo.RecordObject(recipe, "编辑搬砖图案");
            if (recipe.EditCell(index, selectedColor, brush)) Changed();
            ev.Use();
        }
        private void DrawPalette()
        {
            EditorGUILayout.LabelField("色号、人数守恒与近色距离", EditorStyles.boldLabel);
            var serialized = new SerializedObject(recipe); serialized.Update();
            var colors = serialized.FindProperty("palette"); var names = serialized.FindProperty("labels");
            EditorGUI.BeginChangeCheck();
            for (int i = 0; i < recipe.Palette.Length; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Toggle(selectedColor == i, $"{i + 1}号", "Button", GUILayout.Width(50))) selectedColor = i;
                    EditorGUILayout.PropertyField(colors.GetArrayElementAtIndex(i), GUIContent.none, GUILayout.Width(90));
                    if (i < names.arraySize) EditorGUILayout.PropertyField(names.GetArrayElementAtIndex(i), GUIContent.none, GUILayout.Width(90));
                    int bricks = recipe.Cells.Count(c => c == i);
                    int people = recipe.Columns.Sum(c => c.Teams.Where(t => t.Color == i).Sum(t => t.Count));
                    float nearest = recipe.Palette.Length < 2 ? 0 : Enumerable.Range(0, recipe.Palette.Length).Where(c => c != i).Min(c => BlockPortersImagePipeline.Delta(recipe.Palette[i], recipe.Palette[c]));
                    GUILayout.Label($"方块 {bricks} / 人数 {people} · 最近 ΔE {nearest:F1}");
                }
            }
            if (EditorGUI.EndChangeCheck()) { serialized.ApplyModifiedProperties(); Changed(); }
            using (new EditorGUILayout.HorizontalScope())
            {
                mergeTarget = EditorGUILayout.IntSlider("合并至色号", mergeTarget + 1, 1, Math.Max(1, recipe.Palette.Length)) - 1;
                if (GUILayout.Button("合并选中色号")) RunSafe(() => { Undo.RecordObject(recipe, "合并色号"); recipe.Merge(selectedColor, mergeTarget); selectedColor = 0; Changed(); });
                if (GUILayout.Button("新增色号 / 手工拆色")) RunSafe(() => { Undo.RecordObject(recipe, "拆分色号"); recipe.AddColor(Color.white); selectedColor = recipe.Palette.Length - 1; Changed(); });
            }
        }
        private void DrawQueues()
        {
            EditorGUILayout.LabelField("四列队伍（Color 从 0 开始，Count 为 1–8；可直接拖动数组项排序）", EditorStyles.boldLabel);
            var serialized = new SerializedObject(recipe); serialized.Update();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(serialized.FindProperty("columns"), new GUIContent("列与配额"), true);
            if (EditorGUI.EndChangeCheck()) { serialized.ApplyModifiedProperties(); Changed(); }
        }

        private void StartWork(int kind)
        {
            if (work != null) throw new InvalidOperationException("已有计算任务。");
            if (recipe == null) throw new InvalidOperationException("请先导入图片。");
            var settings = JsonUtility.FromJson<PorterRecipeSettings>(JsonUtility.ToJson(recipe.Settings));
            workRecipe = recipe; workRevision = recipe.Revision; workEpoch = editEpoch;
            var cells = (int[])recipe.Cells.Clone(); int colorCount = recipe.Palette.Length;
            BlockPortersLevelData data = kind == 2 ? recipe.CreateData() : null;
            Color32[] pixels = null; int sourceWidth = 0, sourceHeight = 0;
            if (kind == 0) pixels = BlockPortersWorkbenchIO.ReadPixels(recipe.Source, out sourceWidth, out sourceHeight);
            cancellation?.Dispose(); cancellation = new CancellationTokenSource(); var token = cancellation.Token;
            isCanceling = false;
            work = Task.Run(() =>
            {
                if (kind == 2)
                {
                    var report = BlockPortersAnalysis.Solve(data, settings.MaxStates, settings.SearchSeconds, token);
                    BlockPortersAnalysis.Measure(data, report, settings.Seed, settings.PolicyRuns, token);
                    return new WorkResult { Analysis = report };
                }
                PorterImageResult image = kind == 0 ? BlockPortersImagePipeline.Convert(pixels, sourceWidth, sourceHeight, settings, token) : null;
                var generated = BlockPortersLevelGenerator.Generate(settings.Width, settings.Height, image?.Cells ?? cells, image?.Palette.Length ?? colorCount, settings, token);
                return new WorkResult { Image = image, Generated = generated, Analysis = generated.Analysis };
            }, token);
        }
        private void Poll()
        {
            if (work == null || !work.IsCompleted) return;
            var finished = work; work = null;
            try
            {
                var result = finished.GetAwaiter().GetResult();
                if (isCanceling || recipe != workRecipe || workRecipe.Revision != workRevision || workEpoch != editEpoch)
                { message = "结果已取消或过期，未写入配方。"; if (isBatch) batchReport.Add(workRecipe.name + "：取消 / 结果过期"); }
                else
                {
                    Undo.RecordObject(recipe, "关卡计算结果");
                    if (result.Image != null) recipe.SetImage(result.Image);
                    if (result.Generated != null) recipe.SetQueues(result.Generated.Columns);
                    analysis = result.Analysis; analysisRevision = recipe.Revision; EditorUtility.SetDirty(recipe);
                    message = result.Generated?.Diagnostic ?? analysis.Message;
                    AssetDatabase.SaveAssets();
                    if (isBatch)
                    {
                        if (BlockPortersLevelGenerator.Matches(recipe.Settings.Difficulty, analysis))
                        { ExportCurrent(); batchReport.Add(recipe.name + "：成功"); }
                        else batchReport.Add(recipe.name + "：失败 · " + message);
                    }
                }
            }
            catch (Exception exception) { message = exception is OperationCanceledException ? "已取消，未保存计算结果。" : exception.Message; if (isBatch) batchReport.Add(workRecipe.name + "：失败 · " + message); }
            finally
            {
                cancellation.Dispose(); cancellation = null;
                if (isBatch) NextBatch();
                Repaint();
            }
        }
        private void ExportCurrent()
        {
            string path = recipe.ExportedLevel != null ? AssetDatabase.GetAssetPath(recipe.ExportedLevel) : AssetDatabase.GenerateUniqueAssetPath(BlockPortersWorkbenchIO.DataRoot + "/" + SafeName(recipe.name) + ".asset");
            var level = BlockPortersWorkbenchIO.Export(recipe, analysis, analysisRevision, path, catalog);
            message = "已导出：" + AssetDatabase.GetAssetPath(level); EditorGUIUtility.PingObject(level);
        }
        private void BeginBatch(IEnumerable<string> paths)
        {
            if (work != null) { message = "请先取消当前计算。"; return; }
            batch.Clear(); batchReport.Clear(); foreach (string path in paths.OrderBy(p => p, StringComparer.Ordinal)) batch.Enqueue(path);
            isBatch = true; NextBatch();
        }
        private void NextBatch()
        {
            while (batch.Count > 0)
            {
                string path = batch.Dequeue();
                try
                {
                    var settings = recipe != null ? JsonUtility.ToJson(recipe.Settings) : null;
                    recipe = BlockPortersWorkbenchIO.Import(path);
                    if (settings != null) JsonUtility.FromJsonOverwrite(settings, recipe.Settings);
                    editEpoch++; analysis = null; StartWork(0); return;
                }
                catch (Exception exception) { batchReport.Add(Path.GetFileName(path) + "：失败 · " + exception.Message); }
            }
            isBatch = false; message = "批量已完成 / 取消，结果见下方。";
        }
        private void HandleDrop(Rect area)
        {
            var ev = Event.current;
            if (!area.Contains(ev.mousePosition) || (ev.type != EventType.DragUpdated && ev.type != EventType.DragPerform)) return;
            var paths = DragAndDrop.paths.Where(IsImage).Distinct().ToArray();
            if (paths.Length == 0) return;
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (ev.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                if (paths.Length == 1) RunSafe(() => { recipe = BlockPortersWorkbenchIO.Import(paths[0]); editEpoch++; analysis = null; });
                else BeginBatch(paths);
            }
            ev.Use();
        }
        private void RunSafe(Action action) { try { action(); } catch (Exception exception) { message = exception.Message; } }
        private static bool IsImage(string path) => new[] { ".png", ".jpg", ".jpeg" }.Contains(Path.GetExtension(path).ToLowerInvariant());
        private static string SafeName(string name) => string.Concat(name.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_'));
    }
}
