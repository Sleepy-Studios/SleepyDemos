#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Core.Runtime.Rendering.Streamline;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace Tests.Module
{
    public sealed class StreamlineDlssImageTests
    {
        [Serializable]
        private sealed class FrameReport
        {
            public ulong requestId;
            public int result;
            public string state;
            public string stage;
            public uint viewport;
        }

        [Serializable]
        private sealed class InputEvidence
        {
            public float depthMinimum;
            public float depthMaximum;
            public float maximumMotion;
            public Vector2 movingObjectMeanMotion;
            public Vector2 expectedMovingObjectMotion;
            public float staticGeometryMaximumMotion;
            public Vector2 staticGeometryMeanMotion;
            public Vector2 expectedStaticGeometryMotion;
            public int[] resetFrames;
            public Vector2[] jitter;
        }

        [Serializable]
        private sealed class CutEvidence
        {
            public int frame;
            public Vector2 inputCentroid;
            public Vector2 outputCentroid;
            public float outputPixelError;
        }

        private readonly List<RenderTexture> resources = new List<RenderTexture>();
        private readonly List<Texture2D> sources = new List<Texture2D>();
        private GraphicsFence lastFence;
        private bool fenceIssued;
        private bool submittedDlss;
        private readonly List<GameObject> sceneObjects = new List<GameObject>();
        private readonly List<Material> materials = new List<Material>();
        private readonly List<StreamlineCaptureFeature.CaptureSession> captureSessions = new List<StreamlineCaptureFeature.CaptureSession>();
        private UniversalRenderPipelineAsset validationPipeline;
        private ScriptableRendererData validationRenderer;
        private StreamlineCaptureFeature captureFeature;
        private VolumeProfile validationVolumeProfile;
        private RenderPipelineAsset originalGraphicsPipeline;
        private RenderPipelineAsset originalQualityPipeline;

        [UnityTest]
        [Category("StreamlineIntegration")]
        public IEnumerator UrpSceneInputsProduceQualityAndDlaaImages()
        {
            return RunUrpScene(SceneScenario.Static);
        }

        [UnityTest]
        [Category("StreamlineIntegration")]
        public IEnumerator CameraMovementAndCutProduceValidTemporalInputs()
        {
            return RunUrpScene(SceneScenario.CameraCut);
        }

        [UnityTest]
        [Category("StreamlineIntegration")]
        public IEnumerator SameViewportHandlesModeAndOutputResize()
        {
            return RunUrpScene(SceneScenario.Resize);
        }

        [UnityTest]
        [Category("StreamlineIntegration")]
        public IEnumerator DlssOutputReachesCameraTarget()
        {
            return RunUrpScene(SceneScenario.CameraOutput);
        }

        private enum SceneScenario { Static, CameraCut, Resize, CameraOutput, MissingRelease }

        [UnityTest]
        [Category("StreamlineIntegration")]
        public IEnumerator UnsafeViewportReconfigurationIsRejected()
        {
            return RunUrpScene(SceneScenario.MissingRelease);
        }

        private IEnumerator RunUrpScene(SceneScenario scenario)
        {
            bool moveCamera = scenario == SceneScenario.CameraCut;
            bool resize = scenario == SceneScenario.Resize || scenario == SceneScenario.MissingRelease;
            bool publish = scenario == SceneScenario.CameraOutput;
            bool missingRelease = scenario == SceneScenario.MissingRelease;
            if (Application.platform != RuntimePlatform.WindowsEditor || SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D12)
                Assert.Ignore("URP image validation currently requires Windows Editor + DX12.");
            originalGraphicsPipeline = GraphicsSettings.defaultRenderPipeline;
            originalQualityPipeline = QualitySettings.renderPipeline;
            var sourcePipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Assert.IsNotNull(sourcePipeline);
            var pipelineData = new SerializedObject(sourcePipeline);
            var rendererProperty = pipelineData.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0);
            validationRenderer = Object.Instantiate((ScriptableRendererData)rendererProperty.objectReferenceValue);
            validationRenderer.rendererFeatures.Clear();
            captureFeature = ScriptableObject.CreateInstance<StreamlineCaptureFeature>();
            captureFeature.Create();
            validationRenderer.rendererFeatures.Add(captureFeature);
            validationRenderer.SetDirty();
            validationPipeline = Object.Instantiate(sourcePipeline);
            var cloneData = new SerializedObject(validationPipeline);
            cloneData.FindProperty("m_RendererDataList").GetArrayElementAtIndex(0).objectReferenceValue = validationRenderer;
            cloneData.ApplyModifiedPropertiesWithoutUndo();
            validationPipeline.msaaSampleCount = 1;
            validationPipeline.upscalingFilter = UpscalingFilterSelection.Linear;
            validationPipeline.supportsCameraDepthTexture = true;
            validationPipeline.supportsCameraOpaqueTexture = true;
            if (publish)
            {
                validationVolumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
                validationVolumeProfile.Add<Tonemapping>(true).mode.value = TonemappingMode.None;
                validationPipeline.volumeProfile = validationVolumeProfile;
            }
            GraphicsSettings.defaultRenderPipeline = validationPipeline;
            QualitySettings.renderPipeline = validationPipeline;

            var cameraObject = new GameObject("DLSS validation camera", typeof(Camera));
            sceneObjects.Add(cameraObject);
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.enabled = false;
            camera.cullingMask = 1 << 30;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.02f, 0.04f, 0.07f);
            camera.allowHDR = true;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 100;
            camera.fieldOfView = 60;
            camera.transform.SetPositionAndRotation(new Vector3(0, 2, -6), Quaternion.LookRotation(new Vector3(0, -1.2f, 8)));
            UniversalAdditionalCameraData cameraData = camera.GetUniversalAdditionalCameraData();
            // 仅借用 URP 的公开 TAA 配置生成抖动；捕获发生在 TAA 之前，DLSS 输入未经 TAA 处理。
            cameraData.renderPostProcessing = true;
            if (publish) cameraData.volumeLayerMask = 0;
            cameraData.antialiasing = AntialiasingMode.TemporalAntiAliasing;
            cameraData.requiresDepthTexture = true;
            cameraData.SetRenderer(0);

            MakePrimitive(PrimitiveType.Plane, new Vector3(0, -0.02f, 2), Vector3.one, new Color(0.12f, 0.22f, 0.28f));
            GameObject moving = MakePrimitive(PrimitiveType.Cube, new Vector3(-1, 0.65f, 1.7f), new Vector3(1, 1.3f, 1), new Color(0.8f, 0.17f, 0.08f));
            MakePrimitive(PrimitiveType.Sphere, new Vector3(1, 0.85f, 2.5f), Vector3.one * 1.7f, new Color(0.1f, 0.65f, 0.3f));
            for (int index = 0; index < 9; index++)
                MakePrimitive(PrimitiveType.Cube, new Vector3(-2 + index * 0.5f, 1.2f, 4), new Vector3(0.035f, 2.4f, 0.035f), new Color(0.95f, 0.7f, 0.1f));

            int outputWidth = 768;
            int outputHeight = 432;
            camera.targetTexture = MakeTexture(outputWidth, outputHeight, GraphicsFormat.R16G16B16A16_SFloat, 24);
            // 使用正常相机渲染更新引擎的 previous model matrices；手动 RenderRequest
            // 不能作为物体运动历史正确性的验收入口。
            camera.enabled = true;
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();
            camera.enabled = false;
            string folder = Path.GetFullPath(missingRelease ? "Library/Streamline/evidence/reconfiguration-rejection" : publish ? "Library/Streamline/evidence/camera-output" : resize ? "Library/Streamline/evidence/resize-images" : moveCamera ? "Library/Streamline/evidence/camera-images" : "Library/Streamline/evidence/urp-images");
            Directory.CreateDirectory(folder);
            uint frameIndex = 0;
            int step = 0;
            var sharedHistory = new StreamlineCameraHistory();
            StreamlineDlssMode[] modes = resize
                ? new[] { StreamlineDlssMode.Quality, StreamlineDlssMode.Dlaa, StreamlineDlssMode.Quality }
                : new[] { StreamlineDlssMode.Quality, StreamlineDlssMode.Dlaa };
            foreach (StreamlineDlssMode mode in modes)
            {
                if (resize && step > 0 && !missingRelease)
                {
                    Assert.IsTrue(lastFence.passed, "Previous viewport GPU work must finish before resize.");
                    using (var release = new CommandBuffer { name = "Release DLSS viewport before resize" })
                    {
                        Assert.IsTrue(StreamlineDlssValidation.TryReleaseViewport(release, 31, out string reason), reason);
                        lastFence = release.CreateGraphicsFence(GraphicsFenceType.AsyncQueueSynchronisation, SynchronisationStageFlags.AllGPUOperations);
                        Graphics.ExecuteCommandBuffer(release);
                        double releaseDeadline = EditorApplication.timeSinceStartup + 30;
                        while (!lastFence.passed && EditorApplication.timeSinceStartup < releaseDeadline) yield return null;
                        Assert.IsTrue(lastFence.passed);
                        Assert.IsTrue(StreamlineDlssValidation.TryGetReport(out string released, out reason), reason);
                        File.WriteAllText(Path.Combine(folder, "release-before-" + step + ".json"), released);
                        var releasedReport = JsonUtility.FromJson<FrameReport>(released);
                        Assert.That(releasedReport.state, Is.EqualTo("ViewportReleased"), released);
                        Assert.That(releasedReport.viewport, Is.EqualTo(31), released);
                        Assert.That(releasedReport.result, Is.Zero, released);
                    }
                }
                if (resize)
                {
                    outputWidth = new[] { 768, 960, 1152 }[step];
                    outputHeight = outputWidth * 9 / 16;
                    camera.targetTexture = MakeTexture(outputWidth, outputHeight, GraphicsFormat.R16G16B16A16_SFloat, 24);
                    cameraData.resetHistory = true;
                }
                string sampleName = resize ? step + "-" + mode + "-" + outputWidth + "x" + outputHeight : mode.ToString();
                int inputWidth = mode == StreamlineDlssMode.Quality ? outputWidth * 2 / 3 : outputWidth;
                int inputHeight = mode == StreamlineDlssMode.Quality ? outputHeight * 2 / 3 : outputHeight;
                validationPipeline.renderScale = mode == StreamlineDlssMode.Quality ? 2f / 3f : 1f;
                RenderTexture color = MakeTexture(inputWidth, inputHeight, GraphicsFormat.R16G16B16A16_SFloat);
                RenderTexture depth = MakeTexture(inputWidth, inputHeight, GraphicsFormat.R32_SFloat);
                RenderTexture motion = MakeTexture(inputWidth, inputHeight, GraphicsFormat.R16G16_SFloat);
                RenderTexture output = MakeTexture(outputWidth, outputHeight, GraphicsFormat.R16G16B16A16_SFloat);
                var capture = new StreamlineCaptureFeature.CaptureSession(camera, color, depth, motion, output);
                capture.PublishToCamera = publish;
                captureSessions.Add(capture);
                StreamlineCaptureFeature.Session = capture;
                var cameraHistory = resize ? sharedHistory : new StreamlineCameraHistory();
                int frameCount = missingRelease && step == 1 ? 1 : moveCamera ? 24 : 8;
                var jitterEvidence = new Vector2[frameCount];
                var resetFrames = new List<int>();
                Matrix4x4 previousViewProjection = Matrix4x4.identity;
                Matrix4x4 lastPreviousViewProjection = Matrix4x4.identity;
                Matrix4x4 lastViewProjection = Matrix4x4.identity;
                string lastReport = null;
                ulong lastRequest = 0;
                var cutInputs = new Color[3][];
                var cutOutputs = new Color[3][];
                string readbackError = null;
                capture.AfterCapture = (commands, session) =>
                {
                    int historyFrame = session.CapturedFrames;
                    Assert.That(capture.InputWidth, Is.EqualTo(inputWidth));
                    Assert.That(capture.InputHeight, Is.EqualTo(inputHeight));
                    Matrix4x4 projection = capture.Projection;
                    Assert.That(Mathf.Abs(projection.determinant), Is.GreaterThan(0.00001f), "Projection must be invertible.");
                    Matrix4x4 viewProjection = projection * camera.worldToCameraMatrix;
                    jitterEvidence[historyFrame] = capture.Jitter;
                    commands.SetRenderTarget(output);
                    commands.ClearRenderTarget(false, true, Color.magenta);
                    var frame = new StreamlineDlssFrame
                    {
                        Viewport = resize ? 31u : mode == StreamlineDlssMode.Quality ? 11u : 12u, FrameIndex = frameIndex++, Mode = mode,
                        InputWidth = (uint)inputWidth, InputHeight = (uint)inputHeight, OutputWidth = (uint)outputWidth, OutputHeight = (uint)outputHeight,
                        Color = color.GetNativeTexturePtr(), Depth = depth.GetNativeTexturePtr(), Motion = motion.GetNativeTexturePtr(), Output = output.GetNativeTexturePtr()
                    };
                    cameraHistory.Apply(ref frame, camera, projection, capture.Jitter);
                    if (frame.Reset != 0) resetFrames.Add(historyFrame);
                    Assert.IsTrue(StreamlineDlssValidation.TryEnqueue(commands, frame, out lastRequest, out string reason), reason);
                    if ((moveCamera && historyFrame >= 11 && historyFrame <= 13) ||
                        (resize && (historyFrame == 0 || historyFrame == 1 || historyFrame == 7)))
                    {
                        int snapshot = resize ? (historyFrame == 7 ? 2 : historyFrame) : historyFrame - 11;
                        commands.RequestAsyncReadback(color, 0, TextureFormat.RGBAFloat, request =>
                        {
                            if (request.hasError) readbackError = "Cut input readback failed.";
                            else cutInputs[snapshot] = request.GetData<Color>().ToArray();
                        });
                        commands.RequestAsyncReadback(output, 0, TextureFormat.RGBAFloat, request =>
                        {
                            if (request.hasError) readbackError = "Cut output readback failed.";
                            else cutOutputs[snapshot] = request.GetData<Color>().ToArray();
                        });
                    }
                    lastFence = commands.CreateGraphicsFence(GraphicsFenceType.AsyncQueueSynchronisation, SynchronisationStageFlags.AllGPUOperations);
                    fenceIssued = submittedDlss = true;
                    lastPreviousViewProjection = previousViewProjection;
                    lastViewProjection = viewProjection;
                    previousViewProjection = viewProjection;
                };
                for (int historyFrame = 0; historyFrame < frameCount; historyFrame++)
                {
                    yield return null;
                    moving.transform.position = new Vector3(-1 + historyFrame * 0.035f, 0.65f, 1.7f);
                    if (moveCamera)
                    {
                        camera.transform.position = new Vector3(historyFrame * 0.015f + (historyFrame >= 12 ? 0.5f : 0), 2, -6);
                        if (historyFrame == 12)
                        {
                            cameraHistory.Reset();
                            cameraData.resetHistory = true;
                        }
                    }
                    camera.enabled = true;
                    yield return new WaitForEndOfFrame();
                    Assert.IsNull(capture.Error, capture.Error);
                    Assert.That(capture.CapturedFrames, Is.EqualTo(historyFrame + 1));
                }
                camera.enabled = false;
                capture.AfterCapture = null;
                double deadline = EditorApplication.timeSinceStartup + 60;
                while (!lastFence.passed && EditorApplication.timeSinceStartup < deadline) yield return null;
                Assert.IsTrue(lastFence.passed);
                Assert.IsTrue(StreamlineDlssValidation.TryGetReport(out lastReport, out string reportError), reportError);
                var result = JsonUtility.FromJson<FrameReport>(lastReport);
                Assert.That(result.requestId, Is.EqualTo(lastRequest), lastReport);
                if (missingRelease && step == 1)
                {
                    File.WriteAllText(Path.Combine(folder, "rejected.json"), lastReport);
                    Assert.That(result.result, Is.EqualTo(19), lastReport);
                    Assert.That(result.stage, Is.EqualTo("ReleaseViewportBeforeReconfigure"), lastReport);
                    yield break;
                }
                Assert.That(result.result, Is.Zero, lastReport);
                if (moveCamera || resize)
                {
                    while (readbackError == null && (Array.Exists(cutInputs, item => item == null) || Array.Exists(cutOutputs, item => item == null)) && EditorApplication.timeSinceStartup < deadline)
                        yield return null;
                    Assert.IsNull(readbackError, readbackError);
                    for (int snapshot = 0; snapshot < 3; snapshot++)
                    {
                        Assert.IsNotNull(cutInputs[snapshot], "Cut input callback timed out.");
                        Assert.IsNotNull(cutOutputs[snapshot], "Cut output callback timed out.");
                        int snapshotFrame = resize ? (snapshot == 2 ? 7 : snapshot) : snapshot + 11;
                        var evidence = new CutEvidence
                        {
                            frame = snapshotFrame,
                            inputCentroid = SaveCutImage(cutInputs[snapshot], inputWidth, inputHeight, Path.Combine(folder, sampleName + "-frame-" + snapshotFrame + "-input.png")),
                            outputCentroid = SaveCutImage(cutOutputs[snapshot], outputWidth, outputHeight, Path.Combine(folder, sampleName + "-frame-" + snapshotFrame + ".png"))
                        };
                        Vector2 delta = evidence.outputCentroid - evidence.inputCentroid;
                        evidence.outputPixelError = new Vector2(delta.x * outputWidth, delta.y * outputHeight).magnitude;
                        File.WriteAllText(Path.Combine(folder, sampleName + "-frame-" + snapshotFrame + ".json"), JsonUtility.ToJson(evidence, true));
                        Assert.That(evidence.outputPixelError, Is.LessThan(3f), "Cut output retains a displaced object history.");
                    }
                }
                if (publish)
                {
                    TestContext.WriteLine(capture.InputConfiguration);
                    Assert.IsTrue(capture.PublishedToCamera);
                    Assert.That(cameraData.antialiasing, Is.EqualTo(AntialiasingMode.TemporalAntiAliasing), "Persistent jitter configuration was modified.");
                    SaveAndCheckImage(camera.targetTexture, Path.Combine(folder, sampleName + "-camera.png"));
                    SaveAndCheckImage(output, Path.Combine(folder, sampleName + ".png"));
                    CheckCameraOutput(output, camera.targetTexture, Path.Combine(folder, sampleName + "-camera.json"));
                }
                SaveAndCheckImage(output, Path.Combine(folder, sampleName + ".png"));
                SaveAndCheckImage(color, Path.Combine(folder, sampleName + "-input.png"));
                File.WriteAllText(Path.Combine(folder, sampleName + ".json"), lastReport);
                Vector3 frontCenter = moving.transform.position + Vector3.back * 0.5f;
                Vector2 expectedMotion = ProjectMotion(lastViewProjection, lastPreviousViewProjection, frontCenter, frontCenter - Vector3.right * 0.035f);
                Vector3 sphereFront = new Vector3(1, 0.85f, 1.65f);
                Vector2 expectedStatic = ProjectMotion(lastViewProjection, lastPreviousViewProjection, sphereFront, sphereFront);
                CollectionAssert.AreEqual(moveCamera ? new[] { 0, 12 } : new[] { 0 }, resetFrames);
                SaveInputEvidence(color, depth, motion, jitterEvidence, expectedMotion, expectedStatic, resetFrames.ToArray(), folder, sampleName);
                TestContext.WriteLine("URP " + sampleName + ": " + lastReport);
                step++;
            }
        }

        [UnityTest]
        [Category("StreamlineIntegration")]
        public IEnumerator QualityAndDlaaWriteFiniteNonBlankGpuImages()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor || SystemInfo.graphicsDeviceType != GraphicsDeviceType.Direct3D12)
                Assert.Ignore("This native image integration test requires Windows Editor + DX12.");
            Assert.IsTrue(SystemInfo.supportsGraphicsFence);
            string folder = Path.GetFullPath("Library/Streamline/evidence/images");
            Directory.CreateDirectory(folder);
            const int outputWidth = 768;
            const int outputHeight = 432;
            uint frameIndex = 0;

            foreach (StreamlineDlssMode mode in new[] { StreamlineDlssMode.Quality, StreamlineDlssMode.Dlaa })
            {
                int inputWidth = mode == StreamlineDlssMode.Quality ? 512 : outputWidth;
                int inputHeight = mode == StreamlineDlssMode.Quality ? 288 : outputHeight;
                RenderTexture color = MakeTexture(inputWidth, inputHeight, GraphicsFormat.R16G16B16A16_SFloat);
                RenderTexture depth = MakeTexture(inputWidth, inputHeight, GraphicsFormat.R32_SFloat);
                RenderTexture motion = MakeTexture(inputWidth, inputHeight, GraphicsFormat.R16G16_SFloat);
                RenderTexture output = MakeTexture(outputWidth, outputHeight, GraphicsFormat.R16G16B16A16_SFloat);
                Texture2D source = MakePattern(inputWidth, inputHeight);
                Matrix4x4 projection = GL.GetGPUProjectionMatrix(Matrix4x4.Perspective(60, outputWidth / (float)outputHeight, 0.1f, 100f), true);
                string lastReport = null;
                for (int historyFrame = 0; historyFrame < 8; historyFrame++)
                {
                    using (var commands = new CommandBuffer { name = "DLSS Editor image validation" })
                    {
                        commands.Blit(source, color);
                        commands.SetRenderTarget(depth);
                        commands.ClearRenderTarget(false, true, new Color(0.5f, 0, 0, 1));
                        commands.SetRenderTarget(motion);
                        commands.ClearRenderTarget(false, true, Color.clear);
                        commands.SetRenderTarget(output);
                        commands.ClearRenderTarget(false, true, Color.magenta);
                        var frame = new StreamlineDlssFrame
                        {
                            Viewport = mode == StreamlineDlssMode.Quality ? 1u : 2u,
                            FrameIndex = frameIndex++, Mode = mode,
                            InputWidth = (uint)inputWidth, InputHeight = (uint)inputHeight,
                            OutputWidth = outputWidth, OutputHeight = outputHeight,
                            Reset = historyFrame == 0 ? 1u : 0u,
                            DepthInverted = SystemInfo.usesReversedZBuffer ? 1u : 0u,
                            MotionScaleX = -1, MotionScaleY = -1,
                            NearPlane = 0.1f, FarPlane = 100, VerticalFov = Mathf.PI / 3, AspectRatio = outputWidth / (float)outputHeight,
                            CameraViewToClip = projection, ClipToCameraView = projection.inverse,
                            ClipToPreviousClip = Matrix4x4.identity, PreviousClipToClip = Matrix4x4.identity,
                            CameraUp = Vector3.up, CameraRight = Vector3.right, CameraForward = Vector3.forward,
                            Color = color.GetNativeTexturePtr(), Depth = depth.GetNativeTexturePtr(),
                            Motion = motion.GetNativeTexturePtr(), Output = output.GetNativeTexturePtr()
                        };
                        Assert.IsTrue(StreamlineDlssValidation.TryEnqueue(commands, frame, out ulong request, out string reason), reason);
                        lastFence = commands.CreateGraphicsFence(GraphicsFenceType.AsyncQueueSynchronisation, SynchronisationStageFlags.AllGPUOperations);
                        Graphics.ExecuteCommandBuffer(commands);
                        fenceIssued = submittedDlss = true;
                        double deadline = EditorApplication.timeSinceStartup + 60;
                        while (!lastFence.passed && EditorApplication.timeSinceStartup < deadline) yield return null;
                        Assert.IsTrue(lastFence.passed, "DLSS GPU fence timed out.");
                        Assert.IsTrue(StreamlineDlssValidation.TryGetReport(out lastReport, out reason), reason);
                        var report = JsonUtility.FromJson<FrameReport>(lastReport);
                        Assert.That(report.requestId, Is.EqualTo(request), lastReport);
                        Assert.That(report.result, Is.Zero, lastReport);
                        Assert.That(report.state, Is.EqualTo("Evaluated"), lastReport);
                    }
                }
                File.WriteAllText(Path.Combine(folder, mode + ".json"), lastReport);
                SaveAndCheckImage(output, Path.Combine(folder, mode + ".png"));
                SaveAndCheckImage(color, Path.Combine(folder, mode + "-input.png"));
                TestContext.WriteLine(mode + ": " + lastReport);
            }
        }

        [UnityTearDown]
        public IEnumerator ReleaseAfterGpuAndSdkComplete()
        {
            try
            {
                if (fenceIssued)
                {
                    double deadline = EditorApplication.timeSinceStartup + 10;
                    while (!lastFence.passed && EditorApplication.timeSinceStartup < deadline) yield return null;
                    if (!lastFence.passed) Assert.Fail("GPU did not complete; native textures intentionally retained for safety.");
                }
                if (submittedDlss)
                {
                    using (var commands = new CommandBuffer { name = "End DLSS validation session" })
                    {
                        Assert.IsTrue(StreamlineDlssValidation.TryEndSession(commands, out string reason), reason);
                        lastFence = commands.CreateGraphicsFence(GraphicsFenceType.AsyncQueueSynchronisation, SynchronisationStageFlags.AllGPUOperations);
                        Graphics.ExecuteCommandBuffer(commands);
                        double deadline = EditorApplication.timeSinceStartup + 30;
                        while (!lastFence.passed && EditorApplication.timeSinceStartup < deadline) yield return null;
                        Assert.IsTrue(lastFence.passed, "SDK cleanup fence timed out; textures retained.");
                        Assert.IsTrue(StreamlineDlssValidation.TryGetReport(out string cleanup, out reason), reason);
                        string cleanupFolder = Path.GetFullPath("Library/Streamline/evidence/cleanup");
                        Directory.CreateDirectory(cleanupFolder);
                        File.WriteAllText(Path.Combine(cleanupFolder, TestContext.CurrentContext.Test.Name + ".json"), cleanup);
                        var cleanupReport = JsonUtility.FromJson<FrameReport>(cleanup);
                        Assert.That(cleanupReport.state, Is.EqualTo("SessionEnded"), cleanup);
                        Assert.That(cleanupReport.result, Is.Zero, cleanup);
                    }
                }
                foreach (var capture in captureSessions) capture.Dispose();
                foreach (RenderTexture texture in resources) { texture.Release(); Object.DestroyImmediate(texture); }
                foreach (Texture2D texture in sources) Object.DestroyImmediate(texture);
                resources.Clear();
                sources.Clear();
                captureSessions.Clear();
            }
            finally
            {
                // 即使 SDK 清理失败，也恢复项目管线并移除验证相机；原生资源保留至重启。
                StreamlineCaptureFeature.Session = null;
                if (validationPipeline != null)
                {
                    QualitySettings.renderPipeline = originalQualityPipeline;
                    GraphicsSettings.defaultRenderPipeline = originalGraphicsPipeline;
                    Object.DestroyImmediate(validationPipeline);
                }
                if (validationRenderer != null) Object.DestroyImmediate(validationRenderer);
                if (captureFeature != null) Object.DestroyImmediate(captureFeature);
                if (validationVolumeProfile != null)
                {
                    foreach (VolumeComponent component in validationVolumeProfile.components) Object.DestroyImmediate(component);
                    Object.DestroyImmediate(validationVolumeProfile);
                }
                foreach (GameObject item in sceneObjects) Object.DestroyImmediate(item);
                foreach (Material item in materials) Object.DestroyImmediate(item);
                sceneObjects.Clear();
                materials.Clear();
                validationPipeline = null;
                validationRenderer = null;
                captureFeature = null;
                validationVolumeProfile = null;
                fenceIssued = submittedDlss = false;
            }
        }

        private GameObject MakePrimitive(PrimitiveType type, Vector3 position, Vector3 scale, Color color)
        {
            GameObject item = GameObject.CreatePrimitive(type);
            sceneObjects.Add(item);
            item.layer = 30;
            item.transform.position = position;
            item.transform.localScale = scale;
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            materials.Add(material);
            material.SetColor("_BaseColor", color);
            Renderer renderer = item.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.Object;
            return item;
        }

        private RenderTexture MakeTexture(int width, int height, GraphicsFormat format, int depthBits = 0)
        {
            var texture = new RenderTexture(new RenderTextureDescriptor(width, height)
            {
                graphicsFormat = format, depthBufferBits = depthBits, msaaSamples = 1, enableRandomWrite = true, sRGB = false
            });
            resources.Add(texture);
            Assert.IsTrue(texture.Create());
            return texture;
        }

        private Texture2D MakePattern(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBAFloat, false, true);
            sources.Add(texture);
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float checker = ((x / 12 + y / 12) & 1) == 0 ? 0.1f : 0.8f;
                float diagonal = Mathf.Abs(x / (float)width - y / (float)height) < 0.007f ? 0.95f : 0.15f;
                pixels[y * width + x] = new Color(checker, 0.15f + 0.6f * y / height, diagonal, 1);
            }
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        private static void SaveAndCheckImage(RenderTexture source, string path)
        {
            RenderTexture previous = RenderTexture.active;
            var readback = new Texture2D(source.width, source.height, TextureFormat.RGBAFloat, false, true);
            var display = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, false);
            try
            {
                RenderTexture.active = source;
                readback.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
                readback.Apply();
                Color[] pixels = readback.GetPixels();
                float min = float.MaxValue, max = float.MinValue;
                int nonDarkPixels = 0;
                int sentinelPixels = 0;
                for (int index = 0; index < pixels.Length; index++)
                {
                    Color pixel = pixels[index];
                    Assert.IsFalse(float.IsNaN(pixel.r) || float.IsInfinity(pixel.r) || float.IsNaN(pixel.g)
                        || float.IsInfinity(pixel.g) || float.IsNaN(pixel.b) || float.IsInfinity(pixel.b), "Non-finite DLSS output.");
                    min = Mathf.Min(min, pixel.r);
                    max = Mathf.Max(max, pixel.r);
                    if (Mathf.Max(pixel.r, pixel.g, pixel.b) > 0.03f) nonDarkPixels++;
                    if (pixel.r > 0.9f && pixel.b > 0.9f && pixel.g < 0.05f) sentinelPixels++;
                    Color encoded = pixel.gamma;
                    encoded.a = 1;
                    pixels[index] = encoded;
                }
                display.SetPixels(pixels);
                display.Apply();
                File.WriteAllBytes(path, display.EncodeToPNG());
                // 场景可以有大片暗背景，不能沿用彩色测试图的固定绿色均值。
                // 同时检查有效画面覆盖率、动态范围及未写入的洋红清屏色。
                Assert.That(max - min, Is.GreaterThan(0.2f), "Output is blank or a clear color: " + path);
                Assert.That(nonDarkPixels / (float)pixels.Length, Is.GreaterThan(0.01f), "No visible geometry: " + path);
                Assert.That(sentinelPixels / (float)pixels.Length, Is.LessThan(0.1f), "Output contains unwritten sentinel pixels: " + path);
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(readback);
                Object.DestroyImmediate(display);
            }
        }

        [Serializable]
        private sealed class CameraOutputEvidence
        {
            public float meanAbsoluteError;
            public float maximumAbsoluteError;
        }

        private static void CheckCameraOutput(RenderTexture dlss, RenderTexture camera, string path)
        {
            var readback = new Texture2D(dlss.width, dlss.height, TextureFormat.RGBAFloat, false, true);
            RenderTexture previous = RenderTexture.active;
            try
            {
                RenderTexture.active = dlss;
                readback.ReadPixels(new Rect(0, 0, dlss.width, dlss.height), 0, 0);
                readback.Apply();
                Color[] expected = readback.GetPixels();
                RenderTexture.active = camera;
                readback.ReadPixels(new Rect(0, 0, camera.width, camera.height), 0, 0);
                readback.Apply();
                Color[] actual = readback.GetPixels();
                var evidence = new CameraOutputEvidence();
                double sum = 0;
                for (int index = 0; index < actual.Length; index++)
                {
                    for (int channel = 0; channel < 3; channel++)
                    {
                        float difference = Mathf.Abs(actual[index][channel] - expected[index][channel]);
                        evidence.maximumAbsoluteError = Mathf.Max(evidence.maximumAbsoluteError, difference);
                        sum += difference;
                    }
                }
                evidence.meanAbsoluteError = (float)(sum / (actual.Length * 3));
                File.WriteAllText(path, JsonUtility.ToJson(evidence, true));
                Assert.That(evidence.meanAbsoluteError, Is.LessThan(0.001f), "URP camera output differs from DLSS output with neutral post processing.");
                Assert.That(evidence.maximumAbsoluteError, Is.LessThan(0.02f));
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(readback);
            }
        }

        private static Vector2 SaveCutImage(Color[] pixels, int width, int height, string path)
        {
            Vector2 centroid = Vector2.zero;
            int count = 0;
            bool finite = true;
            var display = new Texture2D(width, height, TextureFormat.RGBA32, false, false);
            try
            {
                var gamma = new Color[pixels.Length];
                for (int index = 0; index < pixels.Length; index++)
                {
                    Color pixel = pixels[index];
                    finite &= !(float.IsNaN(pixel.r) || float.IsInfinity(pixel.r) ||
                        float.IsNaN(pixel.g) || float.IsInfinity(pixel.g) || float.IsNaN(pixel.b) || float.IsInfinity(pixel.b));
                    if (pixel.r > 0.4f && pixel.g < 0.1f && pixel.b < 0.1f)
                    {
                        centroid += new Vector2((index % width + 0.5f) / width, (index / width + 0.5f) / height);
                        count++;
                    }
                    gamma[index] = pixel.gamma;
                }
                display.SetPixels(gamma);
                display.Apply();
                File.WriteAllBytes(path, display.EncodeToPNG());
                Assert.IsTrue(finite, "Cut snapshot contains non-finite colors.");
                Assert.That(count, Is.GreaterThan(100), "Cut snapshot is missing the moving object.");
                return centroid / count;
            }
            finally { Object.DestroyImmediate(display); }
        }

        private static Vector2 ProjectMotion(Matrix4x4 current, Matrix4x4 previous, Vector3 point, Vector3 previousPoint)
        {
            Vector4 a = current * new Vector4(point.x, point.y, point.z, 1);
            Vector4 b = previous * new Vector4(previousPoint.x, previousPoint.y, previousPoint.z, 1);
            return new Vector2((a.x / a.w - b.x / b.w) * 0.5f, -(a.y / a.w - b.y / b.w) * 0.5f);
        }

        private static void SaveInputEvidence(RenderTexture color, RenderTexture depth, RenderTexture motion, Vector2[] jitter, Vector2 expectedMotion, Vector2 expectedStatic, int[] resetFrames, string folder, string name)
        {
            var evidence = new InputEvidence { depthMinimum = float.MaxValue, depthMaximum = float.MinValue, jitter = jitter, expectedMovingObjectMotion = expectedMotion, expectedStaticGeometryMotion = expectedStatic, resetFrames = resetFrames };
            RenderTexture previous = RenderTexture.active;
            var readback = new Texture2D(depth.width, depth.height, TextureFormat.RGBAFloat, false, true);
            var display = new Texture2D(depth.width, depth.height, TextureFormat.RGBA32, false, false);
            try
            {
                RenderTexture.active = color;
                readback.ReadPixels(new Rect(0, 0, color.width, color.height), 0, 0);
                readback.Apply();
                Color[] sceneColors = readback.GetPixels();
                RenderTexture.active = depth;
                readback.ReadPixels(new Rect(0, 0, depth.width, depth.height), 0, 0);
                readback.Apply();
                Color[] pixels = readback.GetPixels();
                foreach (Color pixel in pixels)
                {
                    evidence.depthMinimum = Mathf.Min(evidence.depthMinimum, pixel.r);
                    evidence.depthMaximum = Mathf.Max(evidence.depthMaximum, pixel.r);
                }
                float range = Mathf.Max(0.000001f, evidence.depthMaximum - evidence.depthMinimum);
                for (int index = 0; index < pixels.Length; index++)
                {
                    float value = (pixels[index].r - evidence.depthMinimum) / range;
                    pixels[index] = new Color(value, value, value, 1);
                }
                display.SetPixels(pixels);
                display.Apply();
                File.WriteAllBytes(Path.Combine(folder, name + "-depth.png"), display.EncodeToPNG());
                RenderTexture.active = motion;
                readback.ReadPixels(new Rect(0, 0, motion.width, motion.height), 0, 0);
                readback.Apply();
                pixels = readback.GetPixels();
                int movingPixels = 0;
                int staticPixels = 0;
                for (int index = 0; index < pixels.Length; index++)
                {
                    Color pixel = pixels[index];
                    Vector2 vector = new Vector2(pixel.r, pixel.g);
                    evidence.maximumMotion = Mathf.Max(evidence.maximumMotion, vector.magnitude);
                    Color sceneColor = sceneColors[index];
                    if (sceneColor.r > 0.6f && sceneColor.g < 0.3f && sceneColor.b < 0.2f)
                    {
                        evidence.movingObjectMeanMotion += vector;
                        movingPixels++;
                    }
                    // HDR 输入是线性颜色；材质绿色 0.65 在这里约为 0.38。
                    if (sceneColor.g > 0.25f && sceneColor.r < 0.05f)
                    {
                        evidence.staticGeometryMaximumMotion = Mathf.Max(evidence.staticGeometryMaximumMotion, vector.magnitude);
                        evidence.staticGeometryMeanMotion += vector;
                        staticPixels++;
                    }
                    pixels[index] = new Color(0.5f + pixel.r * 32, 0.5f + pixel.g * 32, 0.5f, 1);
                }
                evidence.movingObjectMeanMotion /= Mathf.Max(1, movingPixels);
                evidence.staticGeometryMeanMotion /= Mathf.Max(1, staticPixels);
                display.SetPixels(pixels);
                display.Apply();
                File.WriteAllBytes(Path.Combine(folder, name + "-motion.png"), display.EncodeToPNG());
                File.WriteAllText(Path.Combine(folder, name + "-inputs.json"), JsonUtility.ToJson(evidence, true));
                Assert.That(evidence.depthMaximum - evidence.depthMinimum, Is.GreaterThan(0.00001f), "Scene depth was not captured.");
                Assert.That(evidence.maximumMotion, Is.GreaterThan(0.000001f), "Moving geometry did not produce motion vectors.");
                Assert.That(evidence.maximumMotion, Is.LessThan(0.02f), "Motion exceeds the small horizontal movement in this fixture.");
                Assert.That(movingPixels, Is.GreaterThan(100), "Moving-object input mask is missing.");
                Assert.That(Vector2.Distance(evidence.movingObjectMeanMotion, expectedMotion), Is.LessThan(expectedMotion.magnitude * 0.2f), "Motion direction/scale disagrees with projected object movement.");
                Assert.That(staticPixels, Is.GreaterThan(100));
                Assert.That(Vector2.Distance(evidence.staticGeometryMeanMotion, expectedStatic), Is.LessThan(Mathf.Max(0.00005f, expectedStatic.magnitude * 0.2f)), "Camera motion on static geometry disagrees with projection.");
                if (expectedStatic.sqrMagnitude < 0.0000000001f)
                    Assert.That(evidence.staticGeometryMaximumMotion, Is.LessThan(0.00005f), "Static geometry contains spurious motion.");
                bool changed = false;
                for (int index = 1; index < jitter.Length; index++) changed |= (jitter[index] - jitter[0]).sqrMagnitude > 0.000001f;
                Assert.IsTrue(changed, "URP jitter did not advance across frames.");
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(readback);
                Object.DestroyImmediate(display);
            }
        }
    }
}
#endif
