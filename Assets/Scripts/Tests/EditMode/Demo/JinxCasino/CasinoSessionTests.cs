using System;
using Hotfix.JinxCasino.Rules;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Demo
{
    public sealed class CasinoSessionTests
    {
        [Test]
        public void RandomSequenceHasFixedCrossPlatformGoldenValues()
        {
            var random = new CasinoRandom(1);
            Assert.That(random.NextUInt(), Is.EqualTo(270369u));
            Assert.That(random.NextUInt(), Is.EqualTo(67634689u));
            Assert.That(random.NextUInt(), Is.EqualTo(2647435461u));
            Assert.That(new CasinoRandom(0).NextUInt(), Is.Not.Zero);
            Assert.That(default(CasinoRandom).NextUInt(), Is.EqualTo(new CasinoRandom(0).NextUInt()));
            Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(0));
        }

        [TestCase(CasinoGameKind.Slots, 0)]
        [TestCase(CasinoGameKind.Roulette, 12)]
        [TestCase(CasinoGameKind.CoinFlip, 1)]
        public void SameSeedAndRequestsProduceSameResults(CasinoGameKind game, int choice)
        {
            var first = new CasinoSession("run", 123);
            var second = new CasinoSession("run", 123);
            for (int index = 0; index < 8; index++)
            {
                var request = Request(index.ToString(), game, 5, choice);
                var receipt = first.Apply(request);
                Assert.That(receipt.Accepted, Is.True);
                Assert.That(receipt.BalanceAfter, Is.EqualTo(receipt.BalanceBefore - request.Stake + receipt.Payout));
                Assert.That(JsonUtility.ToJson(second.Apply(request)), Is.EqualTo(JsonUtility.ToJson(receipt)));
            }
            Assert.That(first.ToSnapshotJson(), Is.EqualTo(second.ToSnapshotJson()));
        }

        [Test]
        public void CoinAndRouletteUseExactWinningPayouts()
        {
            // 种子 1 的第一个值为 270369；无偏区间映射后硬币为 0，轮盘为 9。
            var coin = new CasinoSession("run", 1).Apply(Request("a", CasinoGameKind.CoinFlip, 10, 0));
            Assert.That(coin.Outcome, Is.Zero);
            Assert.That(coin.Payout, Is.EqualTo(20));
            var roulette = new CasinoSession("run", 1).Apply(Request("a", CasinoGameKind.Roulette, 10, 9));
            Assert.That(roulette.Outcome, Is.EqualTo(9));
            Assert.That(roulette.Payout, Is.EqualTo(360));
            var loss = new CasinoSession("run", 1).Apply(Request("a", CasinoGameKind.CoinFlip, 10, 1));
            Assert.That(loss.Payout, Is.Zero);
            Assert.That(loss.BalanceAfter, Is.EqualTo(990));
        }

        [Test]
        public void SlotsHaveFixedSymbolsAndPairPayout()
        {
            var receipt = new CasinoSession("run", 1).Apply(Request("a", CasinoGameKind.Slots, 10));
            Assert.That(receipt.Symbols, Is.EqualTo(new[] { 2, 0, 2 }));
            Assert.That(receipt.Outcome, Is.EqualTo(74));
            Assert.That(receipt.Payout, Is.EqualTo(20));
            Assert.That(receipt.BalanceAfter, Is.EqualTo(1010));
        }

        [Test]
        public void AllInLossCannotMakeWalletNegative()
        {
            var session = new CasinoSession("run", 1, 10);
            Assert.That(session.Apply(Request("a", CasinoGameKind.CoinFlip, 10, 1)).BalanceAfter, Is.Zero);
            Assert.That(session.Apply(Request("b", CasinoGameKind.CoinFlip, 1)).Error, Is.EqualTo(CasinoBetError.InsufficientCoins));
            Assert.That(session.Balance, Is.Zero);
        }

        [Test]
        public void RejectedRequestRemainsRejectedAfterTeamBalanceIncreases()
        {
            var session = new CasinoSession("run", 1, 10);
            var pending = Request("rejected", CasinoGameKind.CoinFlip, 15);
            string rejected = JsonUtility.ToJson(session.Apply(pending));
            Assert.That(session.Apply(Request("winner", CasinoGameKind.CoinFlip, 10, 0)).Payout, Is.EqualTo(20));
            string snapshot = session.ToSnapshotJson();
            Assert.That(JsonUtility.ToJson(session.Apply(pending)), Is.EqualTo(rejected));
            Assert.That(session.ToSnapshotJson(), Is.EqualTo(snapshot));
        }

        [Test]
        public void DuplicateDoesNotDeductOrConsumeRandomAndReceiptIsDefensiveCopy()
        {
            var session = new CasinoSession("run", 19);
            var request = Request("a", CasinoGameKind.Slots, 20);
            string result = JsonUtility.ToJson(session.Apply(request));
            string snapshot = session.ToSnapshotJson();
            var duplicate = session.Apply(request);
            Assert.That(JsonUtility.ToJson(duplicate), Is.EqualTo(result));
            duplicate.Request.Stake = 1;
            duplicate.Symbols[0] = 99;
            request.Stake = 1;
            Assert.That(session.ToSnapshotJson(), Is.EqualTo(snapshot));
            Assert.That(session.Revision, Is.EqualTo(1));
        }

        [Test]
        public void ChangingSameRequestIdIsRejectedWithoutStateChange()
        {
            var session = new CasinoSession("run", 19);
            session.Apply(Request("a", CasinoGameKind.Slots, 20));
            string snapshot = session.ToSnapshotJson();
            Assert.That(session.Apply(Request("a", CasinoGameKind.Slots, 21)).Error, Is.EqualTo(CasinoBetError.ConflictingRequest));
            Assert.That(session.ToSnapshotJson(), Is.EqualTo(snapshot));
        }

        [Test]
        public void PlayerIdsScopeIdempotenceIndependently()
        {
            var session = new CasinoSession("run", 1);
            var first = Request("a", CasinoGameKind.CoinFlip, 10);
            var second = Request("a", CasinoGameKind.CoinFlip, 10);
            second.PlayerId = "player-b";
            session.Apply(first);
            Assert.That(session.Apply(second).Accepted, Is.True);
            Assert.That(session.Revision, Is.EqualTo(2));
        }

        [TestCase(CasinoGameKind.Slots, 1, CasinoBetError.InvalidChoice)]
        [TestCase(CasinoGameKind.Roulette, 37, CasinoBetError.InvalidChoice)]
        [TestCase(CasinoGameKind.CoinFlip, -1, CasinoBetError.InvalidChoice)]
        [TestCase((CasinoGameKind)99, 0, CasinoBetError.InvalidGame)]
        public void InvalidGamesAndChoicesLeaveBalanceAndRandomUntouched(CasinoGameKind game, int choice, CasinoBetError error)
        {
            var session = new CasinoSession("run", 1);
            var before = session.CaptureState();
            var receipt = session.Apply(Request("a", game, 10, choice));
            Assert.That(receipt.Error, Is.EqualTo(error));
            Assert.That(session.Balance, Is.EqualTo(before.Coins));
            Assert.That(session.CaptureState().RandomState, Is.EqualTo(before.RandomState));
            Assert.That(session.Revision, Is.Zero);
            Assert.That(JsonUtility.ToJson(session.Apply(Request("a", game, 10, choice))), Is.EqualTo(JsonUtility.ToJson(receipt)));
        }

        [TestCase(0)] [TestCase(-1)] [TestCase(1000001)]
        public void InvalidStakesAreRejected(long stake)
        {
            var session = new CasinoSession("run", 1);
            Assert.That(session.Apply(Request("a", CasinoGameKind.Slots, stake)).Error, Is.EqualTo(CasinoBetError.InvalidStake));
            Assert.That(session.Balance, Is.EqualTo(1000));
        }

        [Test]
        public void InsufficientBalanceAndStaleRunAreRejected()
        {
            var session = new CasinoSession("run", 1, 5);
            Assert.That(session.Apply(Request("a", CasinoGameKind.CoinFlip, 10)).Error, Is.EqualTo(CasinoBetError.InsufficientCoins));
            var stale = Request("b", CasinoGameKind.CoinFlip, 1);
            stale.RunId = "other-run";
            Assert.That(session.Apply(stale).Error, Is.EqualTo(CasinoBetError.WrongRun));
            Assert.That(session.Balance, Is.EqualTo(5));
            Assert.That(session.Revision, Is.Zero);
        }

        [Test]
        public void SnapshotRestoresLedgerAndExactFutureRandomSequence()
        {
            var original = new CasinoSession("run", 481);
            original.Apply(Request("a", CasinoGameKind.Slots, 5));
            original.Apply(Request("rejected", CasinoGameKind.CoinFlip, 1000000));
            var resumed = CasinoSession.Restore(original.ToSnapshotJson());
            Assert.That(resumed.ToSnapshotJson(), Is.EqualTo(original.ToSnapshotJson()));
            string beforeReplay = resumed.ToSnapshotJson();
            resumed.Apply(Request("a", CasinoGameKind.Slots, 5));
            resumed.Apply(Request("rejected", CasinoGameKind.CoinFlip, 1000000));
            Assert.That(resumed.ToSnapshotJson(), Is.EqualTo(beforeReplay));
            foreach (var game in new[] { CasinoGameKind.CoinFlip, CasinoGameKind.Roulette, CasinoGameKind.Slots })
            {
                var request = Request("future-" + game, game, 3);
                Assert.That(JsonUtility.ToJson(resumed.Apply(request)), Is.EqualTo(JsonUtility.ToJson(original.Apply(request))));
            }
        }

        [Test]
        public void CorruptedSnapshotIsRejectedAndCapturedStateDoesNotMutateSession()
        {
            var session = new CasinoSession("run", 1);
            session.Apply(Request("a", CasinoGameKind.CoinFlip, 10));
            var state = session.CaptureState();
            state.Coins++;
            Assert.Throws<ArgumentException>(() => CasinoSession.Restore(JsonUtility.ToJson(state)));
            Assert.That(session.Balance, Is.EqualTo(1010));
            state = session.CaptureState();
            state.Ledger[0].Payout++;
            Assert.Throws<ArgumentException>(() => CasinoSession.Restore(JsonUtility.ToJson(state)));
            Assert.Throws<ArgumentException>(() => CasinoSession.Restore("{}"));
        }

        [Test]
        public void OverflowIsRejectedWithoutConsumingRandom()
        {
            var session = new CasinoSession("run", 1, long.MaxValue - 1);
            uint before = session.CaptureState().RandomState;
            Assert.That(session.Apply(Request("a", CasinoGameKind.CoinFlip, 10)).Error, Is.EqualTo(CasinoBetError.BalanceOverflow));
            Assert.That(session.CaptureState().RandomState, Is.EqualTo(before));
            Assert.That(session.Balance, Is.EqualTo(long.MaxValue - 1));
        }

        private static CasinoBetRequest Request(string requestId, CasinoGameKind game, long stake, int choice = 0)
        {
            return new CasinoBetRequest { RunId = "run", PlayerId = "player-a", RequestId = requestId, Game = game, Stake = stake, Choice = choice };
        }
    }
}
