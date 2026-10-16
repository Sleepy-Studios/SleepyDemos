using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.Rendering;

namespace Core.Editor.Hotfix
{
    /// 创意工坊 H5 发布：使用标准 Unity WebGL 页面，不经过小游戏宿主转换。
    internal static class TapTapBuildAdapter
    {
        internal const string Format = "H5";
        private const long MaximumArchiveBytes = 300L * 1000 * 1000;

        internal static void ConfigurePlayerSettings()
        {
            PlayerSettings.SetIl2CppCodeGeneration(NamedBuildTarget.WebGL, Il2CppCodeGeneration.OptimizeSpeed);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.WebGL, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.WebGL, new[] { GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.WebGL.threadsSupport = false;
            // 宿主焦点变化不能冻结启动 await；玩法、输入与音频由既有生命周期入口暂停。
            PlayerSettings.runInBackground = true;
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            // 平台静态托管的响应头不可控，使用 Unity 官方解压回退。
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.template = "APPLICATION:Default";
            PlayerSettings.WebGL.emscriptenArgs = string.Empty;
            PlayerSettings.WebGL.debugSymbolMode = WebGLDebugSymbolMode.Off;
        }

        /// <summary>确认网页实际游戏入口，拒绝只有堆栈函数的空壳 wasm。</summary>
        /// <param name="path">Unity 生成的未压缩 wasm。</param>
        internal static void ValidateWasm(string path)
        {
            using var reader = new BinaryReader(File.OpenRead(path), new UTF8Encoding(false, true));
            var functions = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                if (reader.ReadUInt32() != 0x6d736100 || reader.ReadUInt32() != 1)
                {
                    throw new InvalidDataException("无效的 wasm 文件头。");
                }
                while (reader.BaseStream.Position < reader.BaseStream.Length)
                {
                    byte section = reader.ReadByte();
                    uint sectionSize = ReadUnsigned();
                    long end = reader.BaseStream.Position + sectionSize;
                    if (end > reader.BaseStream.Length)
                    {
                        throw new InvalidDataException("wasm 段长度越界。");
                    }
                    if (section != 7)
                    {
                        reader.BaseStream.Position = end;
                        continue;
                    }
                    uint count = ReadUnsigned();
                    for (uint index = 0; index < count; index++)
                    {
                        uint size = ReadUnsigned();
                        if (size > end - reader.BaseStream.Position)
                        {
                            throw new InvalidDataException("wasm 导出名越界。");
                        }
                        string name = new UTF8Encoding(false, true).GetString(reader.ReadBytes((int)size));
                        byte kind = reader.ReadByte();
                        ReadUnsigned();
                        if (kind == 0) functions.Add(name);
                    }
                    if (reader.BaseStream.Position != end)
                    {
                        throw new InvalidDataException("wasm 导出段不完整。");
                    }
                    break;
                }
            }
            catch (Exception exception) when (exception is EndOfStreamException || exception is InvalidDataException || exception is DecoderFallbackException)
            {
                throw new InvalidOperationException("TapTap wasm 文件结构无效。", exception);
            }
            if ((!functions.Contains("__main_argc_argv") && !functions.Contains("main")) || !functions.Contains("SendMessage"))
            {
                throw new InvalidOperationException("TapTap wasm 缺少 Unity 游戏入口或 SendMessage，拒绝压缩和归档空壳。");
            }

            uint ReadUnsigned()
            {
                uint value = 0;
                for (int shift = 0; shift < 35; shift += 7)
                {
                    byte next = reader.ReadByte();
                    if (shift == 28 && (next & 0xf0) != 0) break;
                    value |= (uint)(next & 0x7f) << shift;
                    if ((next & 0x80) == 0) return value;
                }
                throw new InvalidDataException("无效的 wasm 变长整数。");
            }
        }

        internal static void Build(string output, bool development)
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
            {
                throw new InvalidOperationException("当前 Unity 未安装 WebGL 构建模块。");
            }
            output = Path.GetFullPath(output);
            GitHubPagesDeployment.EnsureNoLinks(output);
            string webgl = Path.Combine(output, "webgl");
            GitHubPagesDeployment.EnsureNoLinks(webgl);
            Directory.CreateDirectory(webgl);
            GetFiles(webgl);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/AppEntrance.unity" },
                target = BuildTarget.WebGL,
                locationPathName = webgl,
                options = development ? BuildOptions.Development : BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new InvalidOperationException("H5 WebGL Player 构建失败。");
            }
            string entry = Path.Combine(webgl, "index.html");
            if (!File.Exists(entry))
            {
                throw new InvalidOperationException("H5 构建缺少 index.html。");
            }
            string html = File.ReadAllText(entry);
            if (!html.Contains("</head>") || !html.Contains("var config = {"))
            {
                throw new InvalidOperationException("Unity 默认网页模板结构已改变。");
            }
            // 保留 Unity 默认加载页，只让游戏画布适应宿主全屏尺寸。
            html = html.Replace("</head>", "<style>html,body{margin:0;width:100%;height:100%;overflow:hidden}#unity-container.unity-desktop,#unity-container.unity-mobile{position:fixed;inset:0;transform:none;width:100%;height:100%}#unity-canvas{width:100%!important;height:100%!important;touch-action:none}#unity-footer{display:none}</style></head>");
            // Bundle 复用宿主与 HTTP 缓存，避开移动 WebView 的额外 IndexedDB、流克隆与跨域 HEAD 链。
            html = html.Replace("var config = {", "var config = {\n        cacheControl: function (url) { return url === this.dataUrl ? 'must-revalidate' : 'no-store'; },");
            File.WriteAllText(entry, html, new UTF8Encoding(false));
            ValidateBuildWasm(webgl);
            string archive = Path.Combine(output, "h5.zip");
            GitHubPagesDeployment.EnsureNoLinks(archive);
            if (File.Exists(archive))
            {
                File.Delete(archive);
            }
            CreateArchive(webgl, archive);
        }

        internal static void CreateArchive(string webgl, string archive)
        {
            webgl = Path.GetFullPath(webgl);
            GitHubPagesDeployment.EnsureNoLinks(webgl);
            GitHubPagesDeployment.EnsureNoLinks(archive);
            if (Path.GetFullPath(archive).StartsWith(webgl.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("H5 ZIP 不能写入待打包的网页目录。");
            }
            string[] files = GetFiles(webgl);
            if (!files.Contains(Path.Combine(webgl, "index.html"), StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("H5 包必须包含 index.html。");
            }
            using (var zip = ZipFile.Open(archive, ZipArchiveMode.Create))
            {
                foreach (string file in files.OrderBy(path => path, StringComparer.Ordinal))
                {
                    string relative = Path.GetRelativePath(webgl, file).Replace('\\', '/');
                    // 压缩包只有一个顶层目录，符合创意工坊 H5 上传契约。
                    zip.CreateEntryFromFile(file, "game/" + relative, CompressionLevel.Optimal);
                }
            }
            if (new FileInfo(archive).Length > MaximumArchiveBytes)
            {
                throw new InvalidOperationException("H5 ZIP 超过平台 300 MB 限制，拒绝归档为成功。");
            }
        }

        private static string[] GetFiles(string root)
        {
            var pending = new Stack<string>();
            var files = new List<string>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                foreach (string entry in Directory.GetFileSystemEntries(pending.Pop()))
                {
                    GitHubPagesDeployment.EnsureNoLinks(entry);
                    if (Directory.Exists(entry))
                    {
                        pending.Push(entry);
                    }
                    else
                    {
                        files.Add(entry);
                    }
                }
            }
            return files.ToArray();
        }

        private static void ValidateBuildWasm(string webgl)
        {
            string[] modules = Directory.GetFiles(Path.Combine(webgl, "Build"), "*.wasm*");
            if (modules.Length != 1)
            {
                throw new InvalidOperationException("H5 构建必须包含唯一 wasm 模块。");
            }
            if (Path.GetExtension(modules[0]) == ".wasm")
            {
                ValidateWasm(modules[0]);
                return;
            }
            string temporary = Path.Combine("Library", "h5-wasm-" + Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var input = File.OpenRead(modules[0]))
                using (var gzip = new GZipStream(input, CompressionMode.Decompress))
                using (var decoded = File.Open(temporary, FileMode.CreateNew))
                {
                    gzip.CopyTo(decoded);
                }
                ValidateWasm(temporary);
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }
    }
}
