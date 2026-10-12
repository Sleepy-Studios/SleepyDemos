using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Hotfix.KagitKart;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Tests.Demo
{
    public sealed class KagitKartPostProcessPlayModeTests
    {
        [Test]
        public void PipelineSwitchDisableAndDestroyRestoreOnlyOwnFeatureAndDepthMode()
        {
            var root = new GameObject("纸印生命周期测试");
            var camera = root.AddComponent<Camera>();
            camera.enabled = false;
            var host = root.AddComponent<KagitKartPostProcess>();
            var template = new Material(Shader.Find("KagitKart/Post"));
            var a = ScriptableObject.CreateInstance<UniversalRendererData>();
            var b = ScriptableObject.CreateInstance<UniversalRendererData>();
            var first = UniversalRenderPipelineAsset.Create(a);
            var second = UniversalRenderPipelineAsset.Create(b);
            a.copyDepthMode = CopyDepthMode.AfterOpaques;
            b.copyDepthMode = CopyDepthMode.ForcePrepass;
            var existing = ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();
            a.rendererFeatures.Add(existing);
            try
            {
                host.Initialize(camera, template);
                SetPipeline(host, first);
                Assert.That(a.rendererFeatures.Count, Is.EqualTo(2));
                Assert.That(a.copyDepthMode, Is.EqualTo(CopyDepthMode.AfterTransparents));
                Assert.That(a.rendererFeatures.Last().hideFlags, Is.EqualTo(HideFlags.HideAndDontSave));
                SetPipeline(host, second);
                Assert.That(a.rendererFeatures, Is.EquivalentTo(new[] { existing }));
                Assert.That(a.copyDepthMode, Is.EqualTo(CopyDepthMode.AfterOpaques));
                Assert.That(b.rendererFeatures.Count, Is.EqualTo(1));
                host.enabled = false;
                Assert.That(b.rendererFeatures, Is.Empty);
                Assert.That(b.copyDepthMode, Is.EqualTo(CopyDepthMode.ForcePrepass));
                host.enabled = true;
                SetPipeline(host, first);
                Object.DestroyImmediate(root);
                Assert.That(a.rendererFeatures, Is.EquivalentTo(new[] { existing }));
                Assert.That(a.copyDepthMode, Is.EqualTo(CopyDepthMode.AfterOpaques));
                Assert.That(template.GetFloat("uOutlinePx"), Is.EqualTo(1));
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(existing);
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(second);
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
            }
        }

        [Test]
        public void CameraFilterAndWholePixelTapsUseActualTargetHeight()
        {
            var root = new GameObject("纸印相机过滤测试");
            var camera = root.AddComponent<Camera>();
            camera.enabled = false;
            var host = root.AddComponent<KagitKartPostProcess>();
            var template = new Material(Shader.Find("KagitKart/Post"));
            var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
            var pipeline = UniversalRenderPipelineAsset.Create(rendererData);
            try
            {
                host.Initialize(camera, template);
                SetPipeline(host, pipeline);
                var renderer = pipeline.GetRenderer(0);
                var feature = (FullScreenPassRendererFeature)rendererData.rendererFeatures.Single();
                var queue = (IList)typeof(ScriptableRenderer).GetProperty("activeRenderPassQueue", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(renderer);
                using (var frame = new ContextContainer())
                {
                    var cameraData = frame.Create<UniversalCameraData>();
                    cameraData.camera = camera;
                    cameraData.cameraType = CameraType.Game;
                    cameraData.renderType = CameraRenderType.Overlay;
                    var rendering = (RenderingData)Activator.CreateInstance(typeof(RenderingData), BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { frame }, null);
                    feature.AddRenderPasses(renderer, ref rendering);
                    Assert.That(queue, Is.Empty, "Overlay不应进入纸印Pass");
                    cameraData.renderType = CameraRenderType.Base;
                    cameraData.camera = null;
                    feature.AddRenderPasses(renderer, ref rendering);
                    Assert.That(queue, Is.Empty, "非世界相机不应进入纸印Pass");
                    cameraData.camera = camera;
                    cameraData.cameraType = CameraType.SceneView;
                    feature.AddRenderPasses(renderer, ref rendering);
                    Assert.That(queue, Is.Empty, "SceneView不应进入纸印Pass");
                    cameraData.cameraType = CameraType.Game;
                    foreach (var height in new[] { 720, 1080, 1800 })
                    {
                        queue.Clear();
                        cameraData.cameraTargetDescriptor = new RenderTextureDescriptor(1280, height);
                        feature.AddRenderPasses(renderer, ref rendering);
                        Assert.That(queue.Count, Is.EqualTo(2), "最终深度和纸印Pass均应入队");
                        Assert.That(feature.passMaterial.GetFloat("uThick"), Is.EqualTo(height == 720 ? 1 : height == 1080 ? 2 : 3));
                        Assert.That(feature.passMaterial.GetVector("uRes").y, Is.EqualTo(height));
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
                Object.DestroyImmediate(template);
                Object.DestroyImmediate(pipeline);
                Object.DestroyImmediate(rendererData);
            }
        }

        [Test]
        [Category("RequiresGPU")]
        public void GpuPointDepthPreservesAffineInvZAndInksStepsAndPhysicalMisregistration()
        {
            const int size = 16;
            var shader = Shader.Find("KagitKart/Post");
            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.isSupported, Is.True);
            var material = new Material(shader);
            var color = new Texture2D(size, size, TextureFormat.RGBAFloat, false, true);
            var depth = new Texture2D(size, size, TextureFormat.RFloat, false, true) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            var grain = new Texture2D(1, 1, TextureFormat.RFloat, false, true);
            var target = new RenderTexture(size, size, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
            var readback = new Texture2D(size, size, TextureFormat.RGBAFloat, false, true);
            var savedDepth = Shader.GetGlobalTexture("_CameraDepthTexture");
            var savedDepthSize = Shader.GetGlobalVector("_CameraDepthTexture_TexelSize");
            var savedZ = Shader.GetGlobalVector("_ZBufferParams");
            var savedScale = Shader.GetGlobalVector("_RTHandleScale");
            var previous = RenderTexture.active;
            try
            {
                grain.SetPixel(0, 0, new Color(0.5f, 0, 0, 1));
                grain.Apply();
                material.SetTexture("tGrain", grain);
                material.SetTexture("_BlitTexture", color);
                material.SetVector("_BlitScaleBias", new Vector4(1, 1, 0, 0));
                material.SetVector("uRes", new Vector4(size, size, 1f / size, 1f / size));
                material.SetFloat("uPR", 1);
                material.SetFloat("uGrain", 0);
                material.SetFloat("uVignette", 0);
                material.SetVector("uInk", Vector4.zero);
                var z = SystemInfo.usesReversedZBuffer ? new Vector4(0, 0, 19.999f, 0.001f) : new Vector4(0, 0, -19.999f, 20f);
                Shader.SetGlobalVector("_ZBufferParams", z);
                Shader.SetGlobalVector("_RTHandleScale", Vector4.one);
                Shader.SetGlobalTexture("_CameraDepthTexture", depth);
                Shader.SetGlobalVector("_CameraDepthTexture_TexelSize", new Vector4(1f / size, 1f / size, size, size));
                var colors = new Color[size * size];
                var depths = new Color[size * size];
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        colors[y * size + x] = new Color(x / 15f, y / 15f, 0.4f, 1);
                        float invZ = 0.03f + x * 0.001f;
                        depths[y * size + x] = new Color((invZ - z.w) / z.z, 0, 0, 1);
                    }
                }
                color.SetPixels(colors);
                color.Apply();
                depth.SetPixels(depths);
                depth.Apply();
                material.SetFloat("uThick", 0);
                Color[] reference = Draw(material, target, readback);
                material.SetFloat("uThick", 1);
                Color[] plane = Draw(material, target, readback);
                for (int y = 2; y < size - 2; y++)
                {
                    for (int x = 2; x < size - 2; x++)
                    {
                        var expected = reference[(y - 1) * size + x + 1];
                        Assert.That(plane[y * size + x].r, Is.EqualTo(expected.r).Within(0.0001f), "1/z 平面不应描边");
                        Assert.That(plane[y * size + x].g, Is.EqualTo(expected.g).Within(0.0001f), "颜色必须向原作屏幕下方错一像素");
                    }
                }
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        colors[y * size + x] = Color.white;
                        depths[y * size + x] = new Color(((x < 8 ? 0.1f : 0.01f) - z.w) / z.z, 0, 0, 1);
                    }
                }
                color.SetPixels(colors);
                color.Apply();
                depth.SetPixels(depths);
                depth.Apply();
                Color[] step = Draw(material, target, readback);
                Assert.That(step[8 * size + 7].r, Is.EqualTo(0.2f).Within(0.0001f));
                Assert.That(step[8 * size + 8].r, Is.EqualTo(0.2f).Within(0.0001f));
                Assert.That(step[8 * size + 5].r, Is.EqualTo(1f).Within(0.0001f));
            }
            finally
            {
                Shader.SetGlobalTexture("_CameraDepthTexture", savedDepth);
                Shader.SetGlobalVector("_CameraDepthTexture_TexelSize", savedDepthSize);
                Shader.SetGlobalVector("_ZBufferParams", savedZ);
                Shader.SetGlobalVector("_RTHandleScale", savedScale);
                RenderTexture.active = previous;
                Object.DestroyImmediate(material);
                Object.DestroyImmediate(color);
                Object.DestroyImmediate(depth);
                Object.DestroyImmediate(grain);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(readback);
            }
        }

        private static void SetPipeline(KagitKartPostProcess host, RenderPipelineAsset pipeline)
        {
            typeof(KagitKartPostProcess).GetMethod("SetPipeline", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(host, new object[] { pipeline });
        }

        private static Color[] Draw(Material material, RenderTexture target, Texture2D readback)
        {
            using (var command = new CommandBuffer())
            {
                command.SetRenderTarget(target);
                command.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3);
                Graphics.ExecuteCommandBuffer(command);
            }
            RenderTexture.active = target;
            readback.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            readback.Apply();
            return readback.GetPixels();
        }
    }
}
