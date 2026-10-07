using System.Collections.Generic;
using Hotfix.WallSqueeze;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Tests.Demo
{
    public sealed class WallSqueezeSimulationTests
    {
        private readonly List<Object> assets = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in assets)
            {
                Object.DestroyImmediate(asset);
            }
            assets.Clear();
        }

        [Test]
        public void SingleContactPushesWithoutCompression()
        {
            var simulation = Create(new[] { new Vector2(12, 4) });
            simulation.Advance(.95f, 0, 15);
            Assert.That(simulation.Bodies[0].Center.x, Is.GreaterThan(12));
            Assert.That(simulation.Bodies[0].Size.x, Is.EqualTo(.8f).Within(.001f));
            Assert.That(simulation.Remaining, Is.EqualTo(1));
        }

        [Test]
        public void ClampRequiresHoldAndRetreatClearsItSafely()
        {
            var simulation = Create(new[] { new Vector2(15.6f, 4) }, wallX: 15.08f);
            simulation.Advance(.08f, 0, 15.65f);
            Assert.That(simulation.Remaining, Is.EqualTo(1));
            simulation.Advance(.15f, 0, 14);
            Assert.That(simulation.Bodies[0].Hold, Is.Zero);
            Assert.That(simulation.Bodies[0].Size.x, Is.GreaterThan(.24f));
            AssertNoOverlap(simulation);
            simulation.Advance(.8f, 0, 15.65f);
            Assert.That(simulation.Result, Is.EqualTo(WallSqueezeResult.Won));
        }

        [Test]
        public void TouchingChainDiesTogetherAndNeverCountsTwice()
        {
            var simulation = Create(new[] { new Vector2(14.8f, 4), new Vector2(15.6f, 4) }, wallX: 14.2f);
            simulation.Advance(.5f, 0, 15.65f);
            Assert.That(simulation.Remaining, Is.Zero);
            Assert.That(simulation.KilledThisAdvance, Is.EqualTo(2));
            simulation.Advance(1, 0, 15.65f);
            Assert.That(simulation.KilledThisAdvance, Is.Zero);
        }

        [Test]
        public void RetreatReleasesEntireTwoBodyChainBeforeRecoveryRefillsSpace()
        {
            var simulation = Create(new[] { new Vector2(14.8f, 4), new Vector2(15.6f, 4) }, wallX: 14.2f);
            simulation.Advance(.32f, 0, 15.65f);
            Assert.That(simulation.Remaining, Is.EqualTo(2));
            simulation.Advance(.04f, 0, 14);
            simulation.Advance(.3f);
            Assert.That(simulation.Remaining, Is.EqualTo(2));
            foreach (var body in simulation.Bodies)
            {
                Assert.That(body.Hold, Is.Zero);
            }
            AssertNoOverlap(simulation);
        }

        [Test]
        public void ConnectedChainCompressesUniformlyAtEveryStepWithoutOverlap()
        {
            var simulation = Create(new[] { new Vector2(14.8f, 4), new Vector2(15.6f, 4) }, wallX: 14.2f);
            for (int i = 0; i < 50 && simulation.Result == WallSqueezeResult.Playing; i++)
            {
                simulation.Advance(.01f, 0, 15.65f);
                Assert.That(simulation.Bodies[0].Size.x, Is.EqualTo(simulation.Bodies[1].Size.x).Within(.001f));
                AssertNoOverlap(simulation);
            }
            Assert.That(simulation.Result, Is.EqualTo(WallSqueezeResult.Won));
        }

        [Test]
        public void IndependentFreeLaneDoesNotCompress()
        {
            var simulation = Create(new[] { new Vector2(15.6f, 2), new Vector2(15.6f, 6) }, wallX: 14.2f);
            // 短挡墙仅阻挡下方推链，上方仍可前进。
            var walls = new[]
            {
                simulation.Walls[0],
                new WallSqueezeWallLayout { Axis = 1, Center = new Vector2(15.2f, 2), Size = new Vector2(.24f, 1), Track = new Vector2(2, 2) }
            };
            var level = New<WallSqueezeLevel>();
            level.Walls = walls;
            level.Walls[0] = Wall(13.8f);
            level.Monsters = new[] { new Vector2(14.4f, 2), new Vector2(14.4f, 6) };
            level.Residents = System.Array.Empty<Vector2>();
            var settings = New<WallSqueezeSettings>();
            settings.MonsterSpeed = 0;
            simulation = new WallSqueezeSimulation(settings, level);
            simulation.Advance(.15f, 0, 15);
            Assert.That(simulation.Bodies[0].Size.x, Is.LessThan(.8f));
            Assert.That(simulation.Bodies[1].Size.x, Is.EqualTo(.8f).Within(.001f));
            AssertNoOverlap(simulation);
        }

        [Test]
        public void ResidentDeathWinsOverLastMonsterDeathRegardlessOfOrder()
        {
            var simulation = Create(new[] { new Vector2(14.8f, 4) }, new[] { new Vector2(15.6f, 4) }, 14.2f);
            simulation.Advance(.5f, 0, 15.65f);
            Assert.That(simulation.Result, Is.EqualTo(WallSqueezeResult.Lost));
            Assert.That(simulation.Remaining, Is.Zero);
            Assert.That(simulation.ResidentsAlive, Is.Zero);
        }

        [TestCase(.2f)]
        [TestCase(1f / 60)]
        [TestCase(1f / 144)]
        public void FastCommandsNeverTunnelAndWallsBlockEachOther(float step)
        {
            var simulation = Create(new[] { new Vector2(15.6f, 4) });
            for (float elapsed = 0; elapsed < 2.3f; elapsed += step)
            {
                simulation.Advance(step, 0, 10000);
                AssertNoOverlap(simulation);
            }
            Assert.That(simulation.Result, Is.EqualTo(WallSqueezeResult.Won));
            var level = New<WallSqueezeLevel>();
            level.Walls = new[]
            {
                Wall(8),
                new WallSqueezeWallLayout { Axis = 1, Center = new Vector2(12, 5), Size = new Vector2(2, .24f), Track = new Vector2(.35f, 8.65f) }
            };
            level.Monsters = new[] { new Vector2(2, 2) };
            level.Residents = System.Array.Empty<Vector2>();
            var settings = New<WallSqueezeSettings>();
            settings.MonsterSpeed = 0;
            simulation = new WallSqueezeSimulation(settings, level);
            simulation.Advance(2, 0, 15);
            Assert.That(simulation.Walls[0].Center.x, Is.LessThanOrEqualTo(10.881f));
            simulation.Advance(.3f, 0, 8);
            Assert.That(simulation.Walls[0].Center.x, Is.LessThan(10));
        }

        [Test]
        public void RetryRestoresSeedDimensionsCountersAndTime()
        {
            var simulation = Create(new[] { new Vector2(12, 4) });
            simulation.Advance(.5f, 0, 15);
            simulation.Reset();
            Assert.That(simulation.Walls[0].Center.x, Is.EqualTo(8));
            Assert.That(simulation.Bodies[0].Center, Is.EqualTo(new Vector2(12, 4)));
            Assert.That(simulation.Bodies[0].Size, Is.EqualTo(Vector2.one * .8f));
            Assert.That(simulation.Bodies[0].Hold, Is.Zero);
            Assert.That(simulation.Remaining, Is.EqualTo(1));
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        public void SavedLevelsHaveWinningReferencePaths(int number)
        {
            const string root = "Assets/LoadResources/Demos/wall_squeeze/Data/";
            var settings = AssetDatabase.LoadAssetAtPath<WallSqueezeSettings>(root + "Settings.asset");
            var level = AssetDatabase.LoadAssetAtPath<WallSqueezeLevel>(root + "Level" + number + ".asset");
            Assert.That(settings, Is.Not.Null);
            Assert.That(level, Is.Not.Null);
            var simulation = new WallSqueezeSimulation(settings, level);
            if (number == 6)
            {
                simulation.Advance(1.675f, 0, 7.65f);
                simulation.Advance(1.7f, 1, .35f);
                Assert.That(simulation.ResidentsAlive, Is.EqualTo(1));
            }
            else if (number == 5)
            {
                simulation.Advance(4, 0, 15.65f);
            }
            else if (number == 4)
            {
                simulation.Advance(2, 0, 15.65f);
                Assert.That(simulation.Remaining, Is.EqualTo(2), "横向护甲不能靠持续等待夹死");
                simulation.Advance(1.95f, 0, 7.5f);
                simulation.Advance(1.7f, 1, .35f);
            }
            else if (number == 3)
            {
                simulation.Advance(1.55f, 0, 7.2f);
                Assert.That(simulation.Walls[0].Center.x, Is.EqualTo(7.2f).Within(.001f));
                Assert.That(simulation.ResidentsAlive, Is.EqualTo(1));
                simulation.Advance(1.65f, 1, .35f);
            }
            else if (number == 2)
            {
                simulation.Advance(1.65f, 0, 15.65f);
                simulation.Advance(1.7f, 1, .35f);
            }
            else
            {
                simulation.Advance(2.2f, 0, 15.65f);
                Assert.That(simulation.KilledThisAdvance, Is.GreaterThanOrEqualTo(3));
            }
            Assert.That(simulation.Result, Is.EqualTo(WallSqueezeResult.Won));
            AssertNoOverlap(simulation);
        }

        [Test]
        public void ArmoredBlocksHorizontalCompressionButCanBeCrushedVertically()
        {
            var settings = New<WallSqueezeSettings>();
            settings.MonsterSpeed = 0;
            var level = New<WallSqueezeLevel>();
            level.Walls = new[] { Wall(15.08f) };
            level.Monsters = System.Array.Empty<Vector2>();
            level.Residents = System.Array.Empty<Vector2>();
            level.SpecialMonsters = new[] { Monster(WallSqueezeMonsterType.Armored, 15.6f, 4) };
            var simulation = new WallSqueezeSimulation(settings, level);
            simulation.Advance(1, 0, 15.65f);
            Assert.That(simulation.Bodies[0].Size.x, Is.EqualTo(.8f).Within(.001f));
            Assert.That(simulation.Bodies[0].Hold, Is.Zero);
            Assert.That(simulation.Walls[0].Center.x, Is.EqualTo(15.08f).Within(.001f));
            Assert.That(simulation.Result, Is.EqualTo(WallSqueezeResult.Playing));
            AssertNoOverlap(simulation);

            level.Walls = new[] { Horizontal(1.2f, 16) };
            level.SpecialMonsters = new[] { Monster(WallSqueezeMonsterType.Armored, 8, .4f) };
            simulation = new WallSqueezeSimulation(settings, level);
            simulation.Advance(.6f, 0, .35f);
            Assert.That(simulation.Result, Is.EqualTo(WallSqueezeResult.Won));
            Assert.That(simulation.Bodies[0].Size.x, Is.EqualTo(.8f).Within(.001f));
            Assert.That(simulation.Bodies[0].Size.y, Is.EqualTo(.24f).Within(.001f));
        }

        [Test]
        public void SlipperUsesThinCollisionAndSpeedWithoutDyingAtItsRestHeight()
        {
            var settings = New<WallSqueezeSettings>();
            settings.TurnSeconds = 100;
            var level = New<WallSqueezeLevel>();
            level.Walls = new[] { Wall(2) };
            level.Monsters = new[] { new Vector2(5, 3) };
            level.Residents = System.Array.Empty<Vector2>();
            level.SpecialMonsters = new[] { Monster(WallSqueezeMonsterType.Slipper, 5, 6) };
            var simulation = new WallSqueezeSimulation(settings, level);
            simulation.Bodies[0].Direction = simulation.Bodies[1].Direction = Vector2.right;
            simulation.Advance(.5f);
            Assert.That(simulation.Bodies[1].Center.x - 5,
                Is.EqualTo((simulation.Bodies[0].Center.x - 5) * 1.8f).Within(.002f));
            Assert.That(simulation.Bodies[1].Size, Is.EqualTo(new Vector2(.8f, .4f)));
            Assert.That(simulation.Bodies[1].Hold, Is.Zero);
            Assert.That(simulation.Remaining, Is.EqualTo(2));
            simulation.Reset();
            Assert.That(simulation.Bodies[1].Size, Is.EqualTo(simulation.Bodies[1].RestSize));

            // 同一宽 .5 的水平通道：薄怪通过，普通怪在入口被阻挡。
            level.Monsters = System.Array.Empty<Vector2>();
            level.SpecialMonsters = new[] { Monster(WallSqueezeMonsterType.Slipper, 5, 4.5f) };
            level.FixedWalls = CorridorWalls();
            settings.MonsterSpeed = 1;
            simulation = new WallSqueezeSimulation(settings, level);
            simulation.Bodies[0].Direction = Vector2.right;
            simulation.Advance(2);
            Assert.That(simulation.Bodies[0].Center.x, Is.GreaterThan(8));
            Assert.That(simulation.Bodies[0].Size.y, Is.EqualTo(.4f).Within(.001f));
            Assert.That(simulation.Bodies[0].Hold, Is.Zero);
            AssertNoOverlap(simulation);
            level.SpecialMonsters = System.Array.Empty<WallSqueezeMonsterLayout>();
            level.Monsters = new[] { new Vector2(5, 4.5f) };
            simulation = new WallSqueezeSimulation(settings, level);
            simulation.Bodies[0].Direction = Vector2.right;
            simulation.Advance(2);
            Assert.That(simulation.Bodies[0].Center.x, Is.LessThanOrEqualTo(5.601f));
            Assert.That(simulation.Bodies[0].Size.y, Is.EqualTo(.8f).Within(.001f));
            AssertNoOverlap(simulation);
        }

        [Test]
        public void FixedWallCommandsCannotMoveAndProvideRealCrushSupport()
        {
            var settings = New<WallSqueezeSettings>();
            settings.MonsterSpeed = 0;
            var level = New<WallSqueezeLevel>();
            level.Walls = new[] { Wall(8) };
            level.FixedWalls = new[] { Wall(12) };
            level.Monsters = new[] { new Vector2(11, 4) };
            level.Residents = System.Array.Empty<Vector2>();
            var simulation = new WallSqueezeSimulation(settings, level);
            Assert.That(simulation.Walls[1].IsFixed, Is.True);
            Assert.That(simulation.Walls[1].Track, Is.EqualTo(new Vector2(12, 12)));
            simulation.Advance(.5f, 1, 2);
            Assert.That(simulation.Walls[1].Center.x, Is.EqualTo(12));
            simulation.Advance(1.2f, 0, 15.65f);
            Assert.That(simulation.Result, Is.EqualTo(WallSqueezeResult.Won));
            Assert.That(simulation.Walls[0].Center.x, Is.LessThanOrEqualTo(11.761f));
            AssertNoOverlap(simulation);
        }

        [Test]
        public void TimeLimitUsesRuleTimeAndRetryResetsFailureAndClock()
        {
            var settings = New<WallSqueezeSettings>();
            settings.MonsterSpeed = 0;
            var level = New<WallSqueezeLevel>();
            level.Walls = new[] { Wall(8) };
            level.Monsters = new[] { new Vector2(12, 4) };
            level.Residents = System.Array.Empty<Vector2>();
            level.TimeLimit = .5f;
            var simulation = new WallSqueezeSimulation(settings, level);
            simulation.Advance(.2f);
            Assert.That(simulation.RemainingSeconds, Is.EqualTo(.3f).Within(.001f));
            // 宿主暂停时不推进；零时长调用也不能消耗倒计时或夹持时钟。
            simulation.Advance(0, 0, 15);
            Assert.That(simulation.ElapsedSeconds, Is.EqualTo(.2f).Within(.001f));
            simulation.Advance(1);
            Assert.That(simulation.Result, Is.EqualTo(WallSqueezeResult.Lost));
            Assert.That(simulation.Reason, Is.EqualTo(WallSqueezeLossReason.Timeout));
            Assert.That(simulation.RemainingSeconds, Is.Zero);
            simulation.Reset();
            Assert.That(simulation.Result, Is.EqualTo(WallSqueezeResult.Playing));
            Assert.That(simulation.Reason, Is.EqualTo(WallSqueezeLossReason.None));
            Assert.That(simulation.ElapsedSeconds, Is.Zero);
            Assert.That(simulation.RemainingSeconds, Is.EqualTo(.5f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DeathResultsTakePriorityOverTimeoutInTheSameRuleStep(bool resident)
        {
            var settings = New<WallSqueezeSettings>();
            settings.MonsterSpeed = 0;
            var level = New<WallSqueezeLevel>();
            level.Walls = new[] { Wall(14.2f) };
            level.Monsters = new[] { new Vector2(14.8f, 4) };
            level.Residents = resident ? new[] { new Vector2(15.6f, 4) } : System.Array.Empty<Vector2>();
            var reference = new WallSqueezeSimulation(settings, level);
            reference.Advance(1, 0, 15.65f);
            level.TimeLimit = reference.ElapsedSeconds;
            var simulation = new WallSqueezeSimulation(settings, level);
            simulation.Advance(1, 0, 15.65f);
            Assert.That(simulation.Result, Is.EqualTo(resident ? WallSqueezeResult.Lost : WallSqueezeResult.Won));
            Assert.That(simulation.Reason, Is.EqualTo(resident ? WallSqueezeLossReason.ResidentDeath : WallSqueezeLossReason.None));
            Assert.That(simulation.Remaining, Is.Zero);
        }

        [Test]
        public void InvalidSpecialTypesFixedPositionsAndInitialOverlapsAreRejected()
        {
            var settings = New<WallSqueezeSettings>();
            var level = New<WallSqueezeLevel>();
            level.Walls = new[] { Wall(8) };
            level.Monsters = System.Array.Empty<Vector2>();
            level.Residents = System.Array.Empty<Vector2>();
            level.SpecialMonsters = new[] { Monster((WallSqueezeMonsterType)99, 12, 4) };
            Assert.Throws<System.ArgumentException>(() => new WallSqueezeSimulation(settings, level));
            level.SpecialMonsters = new[] { Monster(WallSqueezeMonsterType.Slipper, 8, 4) };
            Assert.Throws<System.ArgumentException>(() => new WallSqueezeSimulation(settings, level));
            level.SpecialMonsters = System.Array.Empty<WallSqueezeMonsterLayout>();
            level.FixedWalls = new[] { Wall(-1) };
            Assert.Throws<System.ArgumentException>(() => new WallSqueezeSimulation(settings, level));
            level.FixedWalls = System.Array.Empty<WallSqueezeWallLayout>();
            level.Walls[0].IsFixed = true;
            Assert.Throws<System.ArgumentException>(() => new WallSqueezeSimulation(settings, level));
        }

        private static WallSqueezeMonsterLayout Monster(WallSqueezeMonsterType type, float x, float y)
        {
            return new WallSqueezeMonsterLayout { Type = type, Center = new Vector2(x, y) };
        }

        private static WallSqueezeWallLayout Horizontal(float y, float width)
        {
            return new WallSqueezeWallLayout { Axis = 1, Center = new Vector2(width / 2, y), Size = new Vector2(width, .24f), Track = new Vector2(.35f, 8.65f) };
        }

        private static WallSqueezeWallLayout[] CorridorWalls()
        {
            return new[]
            {
                new WallSqueezeWallLayout { Axis = 1, Center = new Vector2(11, 4.13f), Size = new Vector2(10, .24f) },
                new WallSqueezeWallLayout { Axis = 1, Center = new Vector2(11, 4.87f), Size = new Vector2(10, .24f) },
                new WallSqueezeWallLayout { Axis = 1, Center = new Vector2(5, 3.925f), Size = new Vector2(2, .15f) },
                new WallSqueezeWallLayout { Axis = 1, Center = new Vector2(5, 5.075f), Size = new Vector2(2, .15f) }
            };
        }

        private WallSqueezeSimulation Create(Vector2[] monsters, Vector2[] residents = null, float wallX = 8)
        {
            var settings = New<WallSqueezeSettings>();
            settings.MonsterSpeed = 0;
            var level = New<WallSqueezeLevel>();
            level.Walls = new[] { Wall(wallX) };
            level.Monsters = monsters;
            level.Residents = residents ?? System.Array.Empty<Vector2>();
            return new WallSqueezeSimulation(settings, level);
        }

        private static WallSqueezeWallLayout Wall(float x)
        {
            return new WallSqueezeWallLayout { Axis = 0, Center = new Vector2(x, 4.5f), Size = new Vector2(.24f, 9), Track = new Vector2(2, 15.65f) };
        }

        private T New<T>() where T : ScriptableObject
        {
            var result = ScriptableObject.CreateInstance<T>();
            assets.Add(result);
            return result;
        }

        private static void AssertNoOverlap(WallSqueezeSimulation simulation)
        {
            foreach (var body in simulation.Bodies)
            {
                if (body.Dead)
                {
                    continue;
                }
                Assert.That(body.Center.x - body.Size.x / 2, Is.GreaterThanOrEqualTo(-.001f));
                Assert.That(body.Center.x + body.Size.x / 2, Is.LessThanOrEqualTo(16.001f));
                Assert.That(body.Center.y - body.Size.y / 2, Is.GreaterThanOrEqualTo(-.001f));
                Assert.That(body.Center.y + body.Size.y / 2, Is.LessThanOrEqualTo(9.001f));
                foreach (var wall in simulation.Walls)
                {
                    bool overlap = Mathf.Abs(body.Center.x - wall.Center.x) < (body.Size.x + wall.Size.x) / 2 - .001f
                        && Mathf.Abs(body.Center.y - wall.Center.y) < (body.Size.y + wall.Size.y) / 2 - .001f;
                    Assert.That(overlap, Is.False, "方块不得穿墙");
                }
                foreach (var other in simulation.Bodies)
                {
                    if (other == body || other.Dead)
                    {
                        continue;
                    }
                    bool overlap = Mathf.Abs(body.Center.x - other.Center.x) < (body.Size.x + other.Size.x) / 2 - .001f
                        && Mathf.Abs(body.Center.y - other.Center.y) < (body.Size.y + other.Size.y) / 2 - .001f;
                    Assert.That(overlap, Is.False, "恢复和推动不能互穿方块");
                }
            }
        }
    }
}
