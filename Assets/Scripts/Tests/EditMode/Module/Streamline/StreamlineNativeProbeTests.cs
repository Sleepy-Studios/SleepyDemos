using System;
using System.Collections;
using System.IO;
using Core.Runtime.Rendering.Streamline;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Experimental.Rendering;
using UnityEngine.TestTools;

namespace Tests.Module
{
    public sealed class StreamlineNativeProbeTests
    {
        [Serializable]
        private sealed class ProbeReport
        {
            public int schemaVersion;
            public string state;
            public bool deviceAvailable;
            public bool renderingEnabled;
            public bool vulkanInitializationObserved;
            public bool vulkanInterposerActive;
            public uint vulkanPresentCount;
        }

        [Serializable]
        private sealed class PresentEvidence
        {
            public uint before;
            public uint after;
        }

        [UnityTest]
        public IEnumerator VulkanPresentInterceptionAdvances()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor || SystemInfo.graphicsDeviceType != GraphicsDeviceType.Vulkan)
                Assert.Ignore("Vulkan Editor startup interception validation.");
            yield return ProbeOnRenderThreadReportsDeviceWithoutEnablingEffects();
            Assert.IsTrue(StreamlineDiagnostics.TryGetReport(out string beforeJson, out string reason), reason);
            var before = JsonUtility.FromJson<ProbeReport>(beforeJson);
            Assert.IsTrue(before.vulkanInitializationObserved, "Start with the preloaded bridge and -streamline-interpose.");
            Assert.IsTrue(before.vulkanInterposerActive);
            double deadline = UnityEditor.EditorApplication.timeSinceStartup + 1;
            while (UnityEditor.EditorApplication.timeSinceStartup < deadline)
            {
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
                yield return null;
            }
            yield return ProbeOnRenderThreadReportsDeviceWithoutEnablingEffects();
            Assert.IsTrue(StreamlineDiagnostics.TryGetReport(out string afterJson, out reason), reason);
            var after = JsonUtility.FromJson<ProbeReport>(afterJson);
            var evidence = new PresentEvidence { before = before.vulkanPresentCount, after = after.vulkanPresentCount };
            File.WriteAllText(Path.GetFullPath("Library/Streamline/evidence/vulkan-present.json"), JsonUtility.ToJson(evidence, true));
            Assert.That(after.vulkanPresentCount, Is.GreaterThan(before.vulkanPresentCount), "No advancing Vulkan Present interception.");
        }

        [Serializable]
        private sealed class ResourceReport
        {
            public string state;
            public int width;
            public int height;
            public uint nativeFormat;
            public bool commandBufferAvailable;
        }

        [UnityTest]
        public IEnumerator NativeTextureAccessPreservesGpuWrittenPixels()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor ||
                (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D12 && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Vulkan))
                Assert.Ignore("Windows Editor DX12/Vulkan native resource validation.");
            Assert.IsTrue(SystemInfo.supportsAsyncGPUReadback);
            var target = new RenderTexture(new RenderTextureDescriptor(16, 16)
            {
                graphicsFormat = GraphicsFormat.R8G8B8A8_UNorm, depthBufferBits = 0, msaaSamples = 1, sRGB = false
            });
            Assert.IsTrue(target.Create());
            bool completed = false, failed = false;
            Color[] pixels = null;
            try
            {
                using (var commands = new CommandBuffer { name = "Streamline native resource roundtrip" })
                {
                    commands.SetRenderTarget(target);
                    commands.ClearRenderTarget(false, true, new Color(0.25f, 0.5f, 0.75f, 1));
                    Assert.IsTrue(StreamlineDiagnostics.TryEnqueueResourceProbe(commands, target, out string reason), reason);
                    commands.RequestAsyncReadback(target, 0, TextureFormat.RGBAFloat, request =>
                    {
                        failed = request.hasError;
                        if (!failed) pixels = request.GetData<Color>().ToArray();
                        completed = true;
                    });
                    Graphics.ExecuteCommandBuffer(commands);
                }
                double deadline = UnityEditor.EditorApplication.timeSinceStartup + 60;
                while (!completed && UnityEditor.EditorApplication.timeSinceStartup < deadline) yield return null;
                Assert.IsTrue(completed, "Resource readback timed out; texture retained until Editor restart.");
                Assert.IsFalse(failed);
                Assert.IsTrue(StreamlineDiagnostics.TryGetResourceReport(out string json, out string error), error);
                string folder = Path.GetFullPath("Library/Streamline/evidence");
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "resource-" + SystemInfo.graphicsDeviceType + ".json"), json);
                var report = JsonUtility.FromJson<ResourceReport>(json);
                Assert.That(report.state, Is.EqualTo("ResourceAccessible"), json);
                Assert.That(report.width, Is.EqualTo(16));
                Assert.That(report.height, Is.EqualTo(16));
                Assert.That(report.nativeFormat, Is.Not.Zero);
                Assert.IsTrue(report.commandBufferAvailable);
                Assert.That(pixels.Length, Is.EqualTo(256));
                foreach (Color pixel in pixels)
                {
                    Assert.That(pixel.r, Is.EqualTo(0.25f).Within(0.005f));
                    Assert.That(pixel.g, Is.EqualTo(0.5f).Within(0.005f));
                    Assert.That(pixel.b, Is.EqualTo(0.75f).Within(0.005f));
                }
            }
            finally
            {
                if (completed) { target.Release(); UnityEngine.Object.DestroyImmediate(target); }
            }
        }

        [UnityTest]
        public IEnumerator ProbeOnRenderThreadReportsDeviceWithoutEnablingEffects()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor) Assert.Ignore("Windows x64 native integration test.");
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D12 && SystemInfo.graphicsDeviceType != GraphicsDeviceType.Vulkan)
                Assert.Ignore("Start Unity with -force-d3d12 or -force-vulkan for this integration test.");

            Assert.IsTrue(SystemInfo.supportsAsyncGPUReadback);
            bool completed = false;
            bool readbackFailed = false;
            using (var commands = new CommandBuffer { name = "Streamline integration test" })
            {
                Assert.IsTrue(StreamlineDiagnostics.TryEnqueueProbe(commands, out string reason), reason);
                // 本机 Vulkan 没有开放 async compute，不能查询 AsyncQueue fence。
                // 同一命令流中的只读回读完成后，前面的原生探测回调也已经完成。
                commands.RequestAsyncReadback(Texture2D.blackTexture, 0, request =>
                {
                    readbackFailed = request.hasError;
                    completed = true;
                });
                Graphics.ExecuteCommandBuffer(commands);
            }

            double deadline = UnityEditor.EditorApplication.timeSinceStartup + 60;
            // 等本次探测事件完成，避免重复运行时把上一轮报告当成本轮结果。
            while (!completed && UnityEditor.EditorApplication.timeSinceStartup < deadline) yield return null;
            Assert.IsTrue(completed, "Probe completion readback timed out.");
            Assert.IsFalse(readbackFailed, "Probe completion readback failed.");
            Assert.IsTrue(StreamlineDiagnostics.TryGetReport(out string json, out string error), error);
            var report = JsonUtility.FromJson<ProbeReport>(json);

            string folder = Path.Combine(Application.dataPath, "../Library/Streamline/evidence");
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "probe-" + SystemInfo.graphicsDeviceType + ".json");
            File.WriteAllText(path, json ?? "null");
            TestContext.WriteLine(json);
            Assert.That(report, Is.Not.Null);
            Assert.That(report.schemaVersion, Is.EqualTo(1));
            Assert.That(report.state, Is.EqualTo("RequirementsProbed"), json);
            Assert.That(report.deviceAvailable, Is.True, json);
            Assert.That(report.renderingEnabled, Is.False, "A diagnostic probe must not enable unvalidated rendering.");
        }
    }
}
