using System;
using System.Collections.Generic;
using System.Linq;
using Hotfix.BlockPorters;
using NUnit.Framework;
using UnityEditor;

namespace Tests.Demo
{
    public sealed class BlockPortersSessionTests
    {
        [Test]
        public void ReservationIsUniqueAndOnlyPickupOpensCell()
        {
            var session = new BlockPortersSession(Level(2, 1, new[] { 0, 0 }));
            var team = session.Dispatch(0);
            Assert.That(session.TryAssign(team.Id, out var first), Is.True);
            Assert.That(session.TryAssign(team.Id, out var second), Is.True);
            Assert.That(first.CellIndex, Is.Not.EqualTo(second.CellIndex));
            Assert.That(session.GetCell(first.CellIndex), Is.EqualTo(0));
            Assert.That(session.TryAssign(team.Id, out _), Is.False);
            Assert.That(session.Deliver(first.Id), Is.False);
            Assert.That(session.PickUp(first.Id), Is.True);
            Assert.That(session.GetCell(first.CellIndex), Is.EqualTo(-1));
            Assert.That(session.PickUp(first.Id), Is.False);
        }

        [Test]
        public void ClosedInteriorHoleDoesNotExposeCenter()
        {
            var cells = Enumerable.Repeat(-1, 25).ToArray();
            for (int i = 0; i < 25; i++) if (i % 5 == 0 || i % 5 == 4 || i / 5 == 0 || i / 5 == 4) cells[i] = 0;
            cells[12] = 1;
            var session = new BlockPortersSession(Level(5, 5, cells));
            var center = session.Dispatch(1);
            Assert.That(session.TryAssign(center.Id, out _), Is.False);
            var outer = session.Dispatch(0);
            Assert.That(session.TryAssign(outer.Id, out var corner), Is.True);
            session.PickUp(corner.Id);
            Assert.That(session.TryAssign(center.Id, out _), Is.False, "只清角落不能沿对角线穿进内部。");
            session.Deliver(corner.Id);
            Assert.That(session.TryAssign(outer.Id, out var edge), Is.True);
            session.PickUp(edge.Id);
            Assert.That(session.TryAssign(center.Id, out var centerJob), Is.True);
            Assert.That(centerJob.CellIndex, Is.EqualTo(12));
        }

        [Test]
        public void PartlyCompletedTeamWaitsAndResumesAfterAnotherColorOpensRoad()
        {
            var session = new BlockPortersSession(Level(3, 3, new[] { 0, 1, 1, 1, 0, 1, 1, 1, 1 }));
            var first = session.Dispatch(0);
            Assert.That(session.TryAssign(first.Id, out var job), Is.True);
            session.PickUp(job.Id); session.Deliver(job.Id);
            Assert.That(first.Waiting, Is.EqualTo(1));
            Assert.That(session.TryAssign(first.Id, out _), Is.False);
            var second = session.Dispatch(1);
            session.TryAssign(second.Id, out job); session.PickUp(job.Id);
            Assert.That(session.TryAssign(first.Id, out job), Is.True);
            Assert.That(job.CellIndex, Is.EqualTo(4));
        }

        [Test]
        public void LastDeliveryWinsAndDuplicateDeliveryCannotCountTwice()
        {
            var session = new BlockPortersSession(Level(1, 1, new[] { 0 }, 1));
            var team = session.Dispatch(0); session.TryAssign(team.Id, out var job);
            session.EvaluateOutcome();
            Assert.That(session.Status, Is.EqualTo(BlockPortersStatus.Playing));
            session.PickUp(job.Id); session.EvaluateOutcome();
            Assert.That(session.Status, Is.EqualTo(BlockPortersStatus.Playing));
            Assert.That(session.Teams.Count, Is.EqualTo(1));
            session.Deliver(job.Id);
            Assert.That(session.Status, Is.EqualTo(BlockPortersStatus.Won));
            Assert.That(session.Teams, Is.Empty);
            Assert.That(session.Deliver(job.Id), Is.False);
            Assert.That(session.Delivered, Is.EqualTo(1));
        }

        [Test]
        public void FullSlotsWithReachableWorkDoNotFail()
        {
            var session = new BlockPortersSession(Level(1, 1, new[] { 0 }, 1));
            session.Dispatch(0); session.EvaluateOutcome();
            Assert.That(session.Status, Is.EqualTo(BlockPortersStatus.Playing));
            Assert.That(session.Dispatch(0), Is.Null);
        }

        [Test]
        public void BlockedFullSlotsFailAndReviveOnlyOnceWithoutLosingProgress()
        {
            var cells = Enumerable.Repeat(0, 25).ToArray();
            for (int y = 1; y < 4; y++) for (int x = 1; x < 4; x++) cells[y * 5 + x] = 1;
            cells[12] = 2;
            var session = new BlockPortersSession(Level(5, 5, cells, 1));
            session.Dispatch(1); session.EvaluateOutcome();
            Assert.That(session.Status, Is.EqualTo(BlockPortersStatus.Failed));
            Assert.That(session.Dispatch(0), Is.Null);
            Assert.That(session.Revive(), Is.True);
            Assert.That(session.Capacity, Is.EqualTo(3));
            Assert.That(session.Teams.Count, Is.EqualTo(1));
            Assert.That(session.Revive(), Is.False);
            session.Dispatch(2); session.Dispatch(0); Drain(session);
            while (session.Peek(0).HasValue) { session.Dispatch(0); Drain(session); }
            Assert.That(session.Delivered, Is.EqualTo(25));
            Assert.That(session.Status, Is.EqualTo(BlockPortersStatus.Won));
        }

        [Test]
        public void QueueOnlyDispatchesHeadAndSessionDoesNotMutateDefinition()
        {
            var level = Level(2, 1, new[] { 0, 0 }, 1,
                new[] { new[] { new PorterTeamDefinition(0, 1), new PorterTeamDefinition(0, 1) }, Array.Empty<PorterTeamDefinition>(), Array.Empty<PorterTeamDefinition>(), Array.Empty<PorterTeamDefinition>() });
            var session = new BlockPortersSession(level);
            Assert.That(session.Dispatch(-1), Is.Null);
            Assert.That(session.Dispatch(4), Is.Null);
            var team = session.Dispatch(0); Drain(session);
            Assert.That(session.Peek(0).HasValue, Is.True);
            Assert.That(level.Cells[0], Is.EqualTo(0));
            Assert.That(level.Columns[0].Length, Is.EqualTo(2));
            Assert.That(new BlockPortersSession(level).Total, Is.EqualTo(2));
            Assert.That(team.Delivered, Is.EqualTo(1));
        }

        [Test]
        public void InvalidCountsAndOversizedTeamAreRejected()
        {
            Assert.Throws<ArgumentException>(() => Level(1, 1, new[] { 0 }, 5,
                new[] { new[] { new PorterTeamDefinition(0, 2) }, Array.Empty<PorterTeamDefinition>(), Array.Empty<PorterTeamDefinition>(), Array.Empty<PorterTeamDefinition>() }));
            Assert.Throws<ArgumentException>(() => Level(3, 3, new int[9], 5,
                new[] { new[] { new PorterTeamDefinition(0, 9) }, Array.Empty<PorterTeamDefinition>(), Array.Empty<PorterTeamDefinition>(), Array.Empty<PorterTeamDefinition>() }));
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)] [TestCase(5)]
        public void AuthoredLevelReferenceSolutionWinsWithoutRevive(int number)
        {
            var asset = AssetDatabase.LoadAssetAtPath<BlockPortersLevel>($"Assets/LoadResources/Demos/block_porters/Data/Level{number}.asset");
            Assert.That(asset, Is.Not.Null);
            var session = new BlockPortersSession(asset.CreateData());
            foreach (int column in asset.Solution)
            {
                Assert.That(session.Dispatch(column), Is.Not.Null, $"第 {number} 关队列 {column}");
                Drain(session);
                Assert.That(session.Status, Is.Not.EqualTo(BlockPortersStatus.Failed));
            }
            Assert.That(session.Status, Is.EqualTo(BlockPortersStatus.Won));
            Assert.That(session.Delivered, Is.EqualTo(session.Total));
            Assert.That(session.HasRevived, Is.False);
            for (int i = 0; i < 4; i++) Assert.That(session.Peek(i), Is.Null);
        }

        private static BlockPortersLevelData Level(int width, int height, int[] cells, int capacity = 5, PorterTeamDefinition[][] columns = null)
        {
            int colors = cells.Max() + 1;
            if (columns == null)
            {
                var lists = Enumerable.Range(0, 4).Select(_ => new List<PorterTeamDefinition>()).ToArray();
                for (int color = 0; color < colors; color++)
                {
                    int count = cells.Count(value => value == color);
                    while (count > 0) { int size = Math.Min(8, count); lists[color % 4].Add(new PorterTeamDefinition(color, size)); count -= size; }
                }
                columns = lists.Select(list => list.ToArray()).ToArray();
            }
            return new BlockPortersLevelData(width, height, cells, columns, capacity, colors);
        }

        private static void Drain(BlockPortersSession session)
        {
            bool progress;
            int guard = 0;
            do
            {
                progress = false;
                foreach (var team in session.Teams.ToArray())
                while (session.TryAssign(team.Id, out var job)) { session.PickUp(job.Id); session.Deliver(job.Id); progress = true; }
                session.EvaluateOutcome();
                Assert.That(++guard, Is.LessThan(2048), "调度未收敛。");
            } while (progress && session.Status == BlockPortersStatus.Playing);
        }
    }
}
