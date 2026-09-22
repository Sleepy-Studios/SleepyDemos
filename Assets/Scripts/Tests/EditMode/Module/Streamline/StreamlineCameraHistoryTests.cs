using Core.Runtime.Rendering.Streamline;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Module
{
    public sealed class StreamlineCameraHistoryTests
    {
        [Test]
        public void CameraTranslationReprojectsToPreviousClipAndResetDiscardsHistory()
        {
            var item = new GameObject("History test", typeof(Camera));
            try
            {
                var camera = item.GetComponent<Camera>();
                var history = new StreamlineCameraHistory();
                Matrix4x4 projection = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);
                var frame = MakeFrame(1);
                history.Apply(ref frame, camera, projection, Vector2.zero);
                Assert.That(frame.Reset, Is.EqualTo(1));
                Vector4 world = new Vector4(0.4f, 0.2f, 5, 1);
                Vector4 previousClip = projection * camera.worldToCameraMatrix * world;
                camera.transform.position = new Vector3(0.3f, 0.1f, 0);
                frame = MakeFrame(2);
                history.Apply(ref frame, camera, projection, new Vector2(0.25f, -0.25f));
                Assert.That(frame.Reset, Is.Zero);
                Vector4 currentClip = projection * camera.worldToCameraMatrix * world;
                Assert.That(Vector4.Distance(frame.ClipToPreviousClip * currentClip, previousClip), Is.LessThan(0.0001f));
                Assert.That(Vector4.Distance(frame.PreviousClipToClip * previousClip, currentClip), Is.LessThan(0.0001f));
                history.Reset();
                frame = MakeFrame(3);
                history.Apply(ref frame, camera, projection, Vector2.zero);
                Assert.That(frame.Reset, Is.EqualTo(1));
                Assert.That(frame.ClipToPreviousClip, Is.EqualTo(Matrix4x4.identity));
            }
            finally { Object.DestroyImmediate(item); }
        }

        [TestCase("Size")]
        [TestCase("Mode")]
        [TestCase("FrameGap")]
        [TestCase("Projection")]
        [TestCase("Viewport")]
        [TestCase("Explicit")]
        public void IncompatibleFrameResetsHistory(string change)
        {
            var item = new GameObject("History reset test", typeof(Camera));
            try
            {
                var camera = item.GetComponent<Camera>();
                var history = new StreamlineCameraHistory();
                Matrix4x4 projection = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);
                var frame = MakeFrame(1);
                history.Apply(ref frame, camera, projection, Vector2.zero);
                frame = MakeFrame(2);
                switch (change)
                {
                    case "Size": frame.OutputWidth = 1920; break;
                    case "Mode": frame.Mode = StreamlineDlssMode.Dlaa; break;
                    case "FrameGap": frame.FrameIndex = 4; break;
                    case "Projection": projection.m00 *= 1.1f; break;
                    case "Viewport": frame.Viewport = 9; break;
                    case "Explicit": frame.Reset = 1; break;
                }
                history.Apply(ref frame, camera, projection, Vector2.zero);
                Assert.That(frame.Reset, Is.EqualTo(1), change);
            }
            finally { Object.DestroyImmediate(item); }
        }

        private static StreamlineDlssFrame MakeFrame(uint index)
        {
            return new StreamlineDlssFrame { FrameIndex = index, Viewport = 1, Mode = StreamlineDlssMode.Quality,
                InputWidth = 512, InputHeight = 288, OutputWidth = 768, OutputHeight = 432 };
        }
    }
}
