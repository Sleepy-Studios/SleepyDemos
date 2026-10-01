using System;
using System.Linq;
using System.Threading;
using Hotfix.BlockPorters;
using Hotfix.Editor.BlockPorters;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Demo
{
    public sealed class BlockPortersWorkbenchTests
    {
        [Test]
        public void TransparentAndOpaqueWhiteAreDistinct()
        {
            var settings = SmallSettings(2, 1);
            var image = BlockPortersImagePipeline.Convert(new Color32[] { new(255,255,255,255), new(0,0,0,0) }, 2, 1, settings);
            Assert.That(image.Cells[0], Is.GreaterThanOrEqualTo(0)); Assert.That(image.Cells[1], Is.EqualTo(-1));
            settings.RemoveBackground = true;
            Assert.Throws<ArgumentException>(() => BlockPortersImagePipeline.Convert(new Color32[] { new(255,255,255,255), new(0,0,0,0) }, 2, 1, settings));
        }

        [Test]
        public void NonSquareImageKeepsAspectAndCropIsRespected()
        {
            var settings = SmallSettings(4, 4); settings.KeepAspect = true;
            var image = BlockPortersImagePipeline.Convert(Enumerable.Repeat(new Color32(200, 0, 0, 255), 8).ToArray(), 4, 2, settings);
            Assert.That(image.Cells.Count(c => c >= 0), Is.EqualTo(8));
            settings.KeepAspect = false; settings.Crop = new Rect(.5f, 0, .5f, 1);
            var pixels = new Color32[] { new(255,0,0,255), new(255,0,0,255), new(0,0,255,255), new(0,0,255,255) };
            image = BlockPortersImagePipeline.Convert(pixels, 4, 1, settings);
            Assert.That(image.Palette[0].b, Is.GreaterThan(.9f));
        }

        [Test]
        public void NearColorsMergeInLabAndRemainDeterministic()
        {
            var settings = SmallSettings(3, 1); settings.ColorBudget = 3;
            var pixels = new Color32[] { new(200, 20, 20, 255), new(202, 22, 22, 255), new(20, 20, 220, 255) };
            var first = BlockPortersImagePipeline.Convert(pixels, 3, 1, settings);
            var second = BlockPortersImagePipeline.Convert(pixels, 3, 1, settings);
            Assert.That(first.Palette.Length, Is.EqualTo(2)); Assert.That(first.Cells, Is.EqualTo(second.Cells));
            Assert.That(first.Palette, Is.EqualTo(second.Palette));
        }

        [Test]
        public void DifficultColorSplitPreservesSilhouetteAndTenPercentBudget()
        {
            var settings = SmallSettings(32, 32); settings.Difficulty = PorterDifficulty.Hard; settings.ColorBudget = 12; settings.NearGroups = 2;
            var pixels = Enumerable.Range(0, 1024).Select(i => i % 33 == 0 ? new Color32(0,0,0,0) : new Color32(160,160,160,255)).ToArray();
            var image = BlockPortersImagePipeline.Convert(pixels, 32, 32, settings);
            Assert.That(image.Cells.Select(c => c >= 0), Is.EqualTo(image.PixelCells.Select(c => c >= 0)));
            int changed = image.Cells.Where((c, i) => c != image.PixelCells[i]).Count();
            Assert.That(changed, Is.LessThanOrEqualTo(Mathf.FloorToInt(image.Cells.Count(c => c >= 0) * .1f)));
            Assert.That(changed, Is.GreaterThan(0));
            for (int a = 0; a < image.Palette.Length; a++) for (int b = a + 1; b < image.Palette.Length; b++)
                Assert.That(BlockPortersImagePipeline.Delta(image.Palette[a], image.Palette[b]), Is.GreaterThanOrEqualTo(10));
        }

        [Test]
        public void PhotoLikeInputRespectsTwelveColorBudget()
        {
            var settings = SmallSettings(32, 32); settings.Difficulty = PorterDifficulty.Hard; settings.ColorBudget = 12;
            var random = new System.Random(40);
            var pixels = Enumerable.Range(0, 1024).Select(_ => new Color32((byte)random.Next(256), (byte)random.Next(256), (byte)random.Next(256), 255)).ToArray();
            var image = BlockPortersImagePipeline.Convert(pixels, 32, 32, settings);
            Assert.That(image.Palette.Length, Is.InRange(1, 12)); Assert.That(image.Cells.Count(c => c >= 0), Is.EqualTo(1024));
        }

        [Test]
        public void CancellationNeverReturnsVerifiedGeneration()
        {
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            Assert.Throws<OperationCanceledException>(() => BlockPortersImagePipeline.Convert(new Color32[] { new(255,0,0,255) }, 1, 1, SmallSettings(1, 1), cancel.Token));
            Assert.That(BlockPortersAnalysis.Solve(OneColor(), token: cancel.Token).State, Is.EqualTo(PorterSolvability.Unknown));
        }

        [Test]
        public void SearchSeparatesUnsolvableAndBudgetUnknown()
        {
            var cells = Enumerable.Repeat(0, 9).ToArray(); cells[4] = 1;
            var data = new BlockPortersLevelData(3, 3, cells, new[] {
                new[] { new PorterTeamDefinition(1, 1), new PorterTeamDefinition(0, 8) },
                Array.Empty<PorterTeamDefinition>(), Array.Empty<PorterTeamDefinition>(), Array.Empty<PorterTeamDefinition>() }, 1, 2);
            Assert.That(BlockPortersAnalysis.Solve(data).State, Is.EqualTo(PorterSolvability.Unsolvable));
            Assert.That(BlockPortersAnalysis.Solve(data, maxStates: 0).State, Is.EqualTo(PorterSolvability.Unknown));
        }

        [Test]
        public void TwelfthColorUsesExactIdAndThirteenthIsRejected()
        {
            var queues = new[] { new[] { new PorterTeamDefinition(11, 1) }, Array.Empty<PorterTeamDefinition>(), Array.Empty<PorterTeamDefinition>(), Array.Empty<PorterTeamDefinition>() };
            var session = new BlockPortersSession(new BlockPortersLevelData(1, 1, new[] { 11 }, queues, 5, 12));
            var team = session.Dispatch(0); Assert.That(session.TryAssign(team.Id, out var job), Is.True); Assert.That(job.Color, Is.EqualTo(11));
            Assert.Throws<ArgumentException>(() => new BlockPortersLevelData(1, 1, new[] { 11 }, queues, 5, 13));
        }

        [Test]
        public void RuntimeClockAndVirtualClockHaveIdenticalEventOrder()
        {
            var data = OneColor();
            var a = new BlockPortersScheduler(new BlockPortersSession(data)); var b = new BlockPortersScheduler(new BlockPortersSession(data));
            var logA = new System.Collections.Generic.List<string>(); var logB = new System.Collections.Generic.List<string>();
            a.PickedUp += job => logA.Add("P" + job.Job.Id); a.Delivered += job => logA.Add("D" + job.Job.Id);
            b.PickedUp += job => logB.Add("P" + job.Job.Id); b.Delivered += job => logB.Add("D" + job.Job.Id);
            a.Dispatch(0); b.Dispatch(0);
            while (!a.IsStable) a.AdvanceTo(a.Time + .017);
            b.Settle();
            Assert.That(logA, Is.EqualTo(logB)); Assert.That(a.Session.Status, Is.EqualTo(BlockPortersStatus.Won));
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void OldLevelWitnessWorksWithSharedScheduler(int number)
        {
            var level = AssetDatabase.LoadAssetAtPath<BlockPortersLevel>($"Assets/LoadResources/Demos/block_porters/Data/Level{number}.asset");
            var scheduler = new BlockPortersScheduler(new BlockPortersSession(level.CreateData()));
            foreach (int col in level.Solution) { Assert.That(scheduler.Dispatch(col), Is.True); scheduler.Settle(); }
            Assert.That(scheduler.Session.Status, Is.EqualTo(BlockPortersStatus.Won));
        }

        [Test]
        public void QueueGenerationConservesPeopleAndSeedAndDoesNotChangeArt()
        {
            var cells = new[] { 0, 0, 0, 1, 1, 1, 0, 0, 0 }; var before = (int[])cells.Clone();
            var settings = SmallSettings(3, 3); settings.Candidates = 1; settings.PolicyRuns = 0;
            var a = BlockPortersLevelGenerator.Generate(3, 3, cells, 2, settings);
            var b = BlockPortersLevelGenerator.Generate(3, 3, cells, 2, settings);
            Assert.That(a.Columns.Length, Is.EqualTo(5));
            Assert.That(a.Analysis.State, Is.EqualTo(PorterSolvability.Solvable));
            Assert.That(a.Columns.SelectMany(c => c), Is.EqualTo(b.Columns.SelectMany(c => c))); Assert.That(cells, Is.EqualTo(before));
            Assert.DoesNotThrow(() => new BlockPortersLevelData(3, 3, cells, a.Columns, 5, 2));
        }

        [Test]
        public void HardQueuesCreateRiskWithoutRecoloringOrDowngrading()
        {
            var cells = new int[256];
            for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) cells[y * 16 + x] = Math.Min(Math.Min(x, 15 - x), Math.Min(y, 15 - y));
            var settings = SmallSettings(16, 16); settings.Difficulty = PorterDifficulty.Hard; settings.Candidates = 1; settings.PolicyRuns = 20;
            var candidate = BlockPortersLevelGenerator.Generate(16, 16, cells, 8, settings);
            Assert.That(candidate.MeetsDifficulty, Is.True);
            Assert.That(candidate.Analysis.GreedyWins, Is.EqualTo(20));
            Assert.That(candidate.Analysis.RandomWins, Is.LessThan(16));
            var trivial = SmallSettings(1, 1); trivial.Difficulty = PorterDifficulty.Normal; trivial.Candidates = 1; trivial.PolicyRuns = 5;
            candidate = BlockPortersLevelGenerator.Generate(1, 1, new[] { 0 }, 1, trivial);
            Assert.That(candidate.Analysis.State, Is.EqualTo(PorterSolvability.Solvable));
            Assert.That(candidate.MeetsDifficulty, Is.False);
        }

        [Test]
        public void PerturbedNormalCandidatesKeepExactQuotasAndSeed()
        {
            var cells = new int[100];
            for (int y = 0; y < 10; y++) for (int x = 0; x < 10; x++) cells[y * 10 + x] = Math.Min(Math.Min(x, 9 - x), Math.Min(y, 9 - y));
            var settings = SmallSettings(10, 10); settings.Difficulty = PorterDifficulty.Normal; settings.Candidates = 3; settings.PolicyRuns = 10;
            var a = BlockPortersLevelGenerator.Generate(10, 10, cells, 5, settings);
            var b = BlockPortersLevelGenerator.Generate(10, 10, cells, 5, settings);
            Assert.That(a.Columns.SelectMany(c => c), Is.EqualTo(b.Columns.SelectMany(c => c)));
            Assert.That(a.Columns.SelectMany(c => c).All(t => t.Count >= 1 && t.Count <= 8), Is.True);
            for (int color = 0; color < 5; color++) Assert.That(a.Columns.SelectMany(c => c).Where(t => t.Color == color).Sum(t => t.Count), Is.EqualTo(cells.Count(c => c == color)));
            Assert.That(a.Analysis.State, Is.EqualTo(PorterSolvability.Solvable));
        }

        [Test]
        public void LockedAreasAndUndoProtectManualEdits()
        {
            var recipe = CreateRecipe();
            try
            {
                recipe.EditCell(0, 0, PorterBrush.Lock);
                Assert.That(recipe.EditCell(0, 1, PorterBrush.Paint), Is.False);
                recipe.EditCell(1, 1, PorterBrush.Fill); Assert.That(recipe.Cells[0], Is.EqualTo(0));
                Assert.Throws<InvalidOperationException>(() => recipe.Merge(0, 1));
                Undo.RecordObject(recipe, "测试画笔"); recipe.EditCell(1, 0, PorterBrush.Paint); Undo.FlushUndoRecordObjects();
                Undo.PerformUndo(); Assert.That(recipe.Cells[1], Is.EqualTo(1));
            }
            finally { UnityEngine.Object.DestroyImmediate(recipe); }
        }

        [Test]
        public void ExportRejectsUnknownStaleAndPreservesGuidOnUpdate()
        {
            const string path = BlockPortersWorkbenchIO.DataRoot + "/WorkbenchTestLevel.asset";
            var recipe = CreateRecipe(); var catalog = ScriptableObject.CreateInstance<BlockPortersLevelCatalog>();
            try
            {
                recipe.Settings.Difficulty = PorterDifficulty.Easy;
                recipe.SetQueues(new[] { new[] { new PorterTeamDefinition(0, 2) }, Array.Empty<PorterTeamDefinition>(), Array.Empty<PorterTeamDefinition>(), Array.Empty<PorterTeamDefinition>() });
                var report = BlockPortersAnalysis.Solve(recipe.CreateData());
                Assert.Throws<InvalidOperationException>(() => BlockPortersWorkbenchIO.Export(recipe, new PorterAnalysis { State = PorterSolvability.Unknown }, recipe.Revision, path, catalog));
                Assert.Throws<InvalidOperationException>(() => BlockPortersWorkbenchIO.Export(recipe, report, recipe.Revision - 1, path, catalog));
                var level = BlockPortersWorkbenchIO.Export(recipe, report, recipe.Revision, path, catalog);
                string guid = AssetDatabase.AssetPathToGUID(path);
                BlockPortersWorkbenchIO.Export(recipe, report, recipe.Revision, path, catalog);
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid)); Assert.That(catalog.Levels.Single(), Is.SameAs(level));
                recipe.Invalidate(); Assert.Throws<InvalidOperationException>(() => BlockPortersWorkbenchIO.Export(recipe, report, recipe.Revision - 1, path, catalog));
            }
            finally { AssetDatabase.DeleteAsset(path); UnityEngine.Object.DestroyImmediate(recipe); UnityEngine.Object.DestroyImmediate(catalog); }
        }
        private static PorterRecipeSettings SmallSettings(int width, int height) => new() { Width = width, Height = height, KeepAspect = false, Difficulty = PorterDifficulty.Easy, ColorBudget = 5, NearGroups = 0 };
        private static BlockPortersLevelData OneColor() => new(2, 1, new[] { 0, 0 }, new[] { new[] { new PorterTeamDefinition(0, 2) }, Array.Empty<PorterTeamDefinition>(), Array.Empty<PorterTeamDefinition>(), Array.Empty<PorterTeamDefinition>() }, 5, 1);
        private static BlockPortersRecipe CreateRecipe()
        {
            var recipe = ScriptableObject.CreateInstance<BlockPortersRecipe>(); recipe.Settings.Width = 2; recipe.Settings.Height = 1;
            recipe.SetImage(new PorterImageResult { Cells = new[] { 0, 0 }, PixelCells = new[] { 0, 0 }, Palette = new[] { Color.red, Color.blue }, PixelPalette = new[] { Color.red, Color.blue } });
            return recipe;
        }
    }
}
