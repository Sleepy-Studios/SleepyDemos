using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hotfix.JinxCasino.Rules
{
    // 网络层只在状态权威执行 Apply；客户端展示回执，不自行抽取或先扣团队资金。
    public sealed class CasinoSession
    {
        public const long MaximumStake = 1000000;

        private readonly CasinoRunState state;
        private readonly Dictionary<string, CasinoBetReceipt> receipts = new Dictionary<string, CasinoBetReceipt>(StringComparer.Ordinal);
        private CasinoRandom random;

        public string RunId => state.RunId;
        public long Balance => state.Coins;
        public int Revision => state.Revision;

        /// <summary>创建 P0 团队钱包与固定种子的独立局，不包含网络、倒计时和场景职责。</summary>
        /// <param name="runId">非空且最多 64 字符的局标识；在房间开始新局时重新生成。</param>
        /// <param name="seed">由状态权威选定的固定种子；0 同样可用。</param>
        /// <param name="startingCoins">初始团队余额，默认 1000；必须为非负整数。</param>
        public CasinoSession(string runId, uint seed, long startingCoins = 1000)
        {
            if (!IsIdentityValid(runId)) throw new ArgumentException("局标识必须为非空且最多 64 字符。", nameof(runId));
            if (startingCoins < 0) throw new ArgumentOutOfRangeException(nameof(startingCoins));
            random = new CasinoRandom(seed);
            state = new CasinoRunState
            {
                RunId = runId,
                Seed = seed,
                StartingCoins = startingCoins,
                Coins = startingCoins,
                RandomState = random.State
            };
        }

        /// <summary>原子处理投入和返还；同一玩家重复提交相同请求时只返回历史回执。</summary>
        /// <param name="request">带已认证玩家标识的请求；同 ID 改变内容会拒绝，失败请求也保留幂等性。</param>
        /// <returns>独立副本回执；所有失败均不改变钱包、随机状态和结算版本。</returns>
        public CasinoBetReceipt Apply(CasinoBetRequest request)
        {
            if (request == null || !IsIdentityValid(request.RunId) || !IsIdentityValid(request.PlayerId) || !IsIdentityValid(request.RequestId))
                return CreateRejected(request, CasinoBetError.InvalidIdentity);
            // 局标识先于幂等键检查，旧局网络包不能覆盖当前局缓存。
            if (!string.Equals(request.RunId, state.RunId, StringComparison.Ordinal))
                return CreateRejected(request, CasinoBetError.WrongRun);

            string key = CreateKey(request);
            if (receipts.TryGetValue(key, out CasinoBetReceipt previous))
            {
                return RequestsEqual(previous.Request, request)
                    ? CopyReceipt(previous)
                    : CreateRejected(request, CasinoBetError.ConflictingRequest);
            }

            CasinoBetError error = ValidateBet(request);
            if (error != CasinoBetError.None)
                return Remember(key, CreateRejected(request, error));

            // 在局部生成器上求结果，确保溢出拒绝也不推进本局的随机序列。
            CasinoRandom candidate = random;
            int outcome;
            int[] symbols;
            int multiplier = Resolve(request, ref candidate, out outcome, out symbols);
            long payout = request.Stake * multiplier;
            long afterStake = state.Coins - request.Stake;
            if (afterStake > long.MaxValue - payout || state.Revision == int.MaxValue)
                return Remember(key, CreateRejected(request, CasinoBetError.BalanceOverflow));

            var receipt = new CasinoBetReceipt
            {
                Request = CopyRequest(request),
                Accepted = true,
                Error = CasinoBetError.None,
                Outcome = outcome,
                Symbols = symbols,
                Payout = payout,
                BalanceBefore = state.Coins,
                BalanceAfter = afterStake + payout,
                Revision = state.Revision + 1
            };
            state.Coins = receipt.BalanceAfter;
            state.Revision = receipt.Revision;
            random = candidate;
            state.RandomState = random.State;
            return Remember(key, receipt);
        }

        /// 导出完整局快照；包含种子、钱包、随机状态与全部幂等回执。
        public string ToSnapshotJson()
        {
            return JsonUtility.ToJson(state);
        }

        /// 返回可供网络适配读取的深拷贝；修改该对象不会改变当前局。
        public CasinoRunState CaptureState()
        {
            return JsonUtility.FromJson<CasinoRunState>(ToSnapshotJson());
        }

        /// <summary>恢复快照并按保存的原始请求重放校验；恢复后重发与未来结果均保持一致。</summary>
        /// <param name="snapshotJson">SchemaVersion 为 1 的完整快照；空值、非法结构和账本不一致抛出 ArgumentException。</param>
        /// <returns>与原局独立的可继续结算实例。</returns>
        public static CasinoSession Restore(string snapshotJson)
        {
            if (string.IsNullOrWhiteSpace(snapshotJson)) throw new ArgumentException("快照不能为空。", nameof(snapshotJson));
            CasinoRunState saved;
            try { saved = JsonUtility.FromJson<CasinoRunState>(snapshotJson); }
            catch (ArgumentException exception) { throw new ArgumentException("快照不是合法 JSON。", nameof(snapshotJson), exception); }
            if (saved == null || saved.SchemaVersion != 1 || !IsIdentityValid(saved.RunId) || saved.StartingCoins < 0 || saved.Ledger == null)
                throw new ArgumentException("快照版本或局结构非法。", nameof(snapshotJson));

            var session = new CasinoSession(saved.RunId, saved.Seed, saved.StartingCoins);
            for (int index = 0; index < saved.Ledger.Count; index++)
            {
                CasinoBetReceipt entry = saved.Ledger[index];
                if (entry == null || entry.Request == null)
                    throw new ArgumentException("快照账本存在空请求。", nameof(snapshotJson));
                CasinoBetReceipt replayed = session.Apply(entry.Request);
                if (session.state.Ledger.Count != index + 1 || !ReceiptsEqual(entry, replayed))
                    throw new ArgumentException("快照账本无法重放校验。", nameof(snapshotJson));
            }
            if (session.Balance != saved.Coins || session.Revision != saved.Revision || session.random.State != saved.RandomState)
                throw new ArgumentException("快照余额、随机状态或结算版本与账本不一致。", nameof(snapshotJson));
            return session;
        }

        private CasinoBetError ValidateBet(CasinoBetRequest request)
        {
            if (request.Game < CasinoGameKind.Slots || request.Game > CasinoGameKind.CoinFlip)
                return CasinoBetError.InvalidGame;
            if (request.Stake <= 0 || request.Stake > MaximumStake)
                return CasinoBetError.InvalidStake;
            int maximumChoice = request.Game == CasinoGameKind.Roulette ? 36 : request.Game == CasinoGameKind.CoinFlip ? 1 : 0;
            if (request.Choice < 0 || request.Choice > maximumChoice)
                return CasinoBetError.InvalidChoice;
            if (request.Stake > state.Coins)
                return CasinoBetError.InsufficientCoins;
            return CasinoBetError.None;
        }

        private static int Resolve(CasinoBetRequest request, ref CasinoRandom random, out int outcome, out int[] symbols)
        {
            symbols = Array.Empty<int>();
            if (request.Game == CasinoGameKind.CoinFlip)
            {
                outcome = random.NextInt(2);
                return outcome == request.Choice ? 2 : 0;
            }
            if (request.Game == CasinoGameKind.Roulette)
            {
                outcome = random.NextInt(37);
                return outcome == request.Choice ? 36 : 0;
            }
            symbols = new[] { random.NextInt(6), random.NextInt(6), random.NextInt(6) };
            outcome = symbols[0] * 36 + symbols[1] * 6 + symbols[2];
            // P0：任意双符号毛返还 2 倍；三同 10 倍，三个星号（5）为 30 倍。
            if (symbols[0] == symbols[1] && symbols[1] == symbols[2]) return symbols[0] == 5 ? 30 : 10;
            return symbols[0] == symbols[1] || symbols[1] == symbols[2] || symbols[0] == symbols[2] ? 2 : 0;
        }

        private CasinoBetReceipt CreateRejected(CasinoBetRequest request, CasinoBetError error)
        {
            return new CasinoBetReceipt
            {
                Request = CopyRequest(request),
                Error = error,
                Outcome = -1,
                Symbols = Array.Empty<int>(),
                BalanceBefore = state.Coins,
                BalanceAfter = state.Coins,
                Revision = state.Revision
            };
        }

        private CasinoBetReceipt Remember(string key, CasinoBetReceipt receipt)
        {
            state.Ledger.Add(receipt);
            receipts.Add(key, receipt);
            return CopyReceipt(receipt);
        }

        private static string CreateKey(CasinoBetRequest request)
        {
            // 长度前缀避免玩家/请求标识中包含分隔符造成碰撞。
            return request.PlayerId.Length + ":" + request.PlayerId + request.RequestId;
        }

        private static bool IsIdentityValid(string value)
        {
            return !string.IsNullOrWhiteSpace(value) && value.Length <= 64;
        }

        private static CasinoBetRequest CopyRequest(CasinoBetRequest request)
        {
            if (request == null) return null;
            return new CasinoBetRequest
            {
                RunId = request.RunId, PlayerId = request.PlayerId, RequestId = request.RequestId,
                Game = request.Game, Stake = request.Stake, Choice = request.Choice
            };
        }

        private static CasinoBetReceipt CopyReceipt(CasinoBetReceipt receipt)
        {
            return new CasinoBetReceipt
            {
                Request = CopyRequest(receipt.Request), Accepted = receipt.Accepted, Error = receipt.Error,
                Outcome = receipt.Outcome, Symbols = receipt.Symbols == null ? null : (int[])receipt.Symbols.Clone(),
                Payout = receipt.Payout, BalanceBefore = receipt.BalanceBefore, BalanceAfter = receipt.BalanceAfter, Revision = receipt.Revision
            };
        }

        private static bool RequestsEqual(CasinoBetRequest first, CasinoBetRequest second)
        {
            return first != null && second != null && first.RunId == second.RunId && first.PlayerId == second.PlayerId &&
                first.RequestId == second.RequestId && first.Game == second.Game && first.Stake == second.Stake && first.Choice == second.Choice;
        }

        private static bool ReceiptsEqual(CasinoBetReceipt first, CasinoBetReceipt second)
        {
            if (!RequestsEqual(first.Request, second.Request) || first.Accepted != second.Accepted || first.Error != second.Error ||
                first.Outcome != second.Outcome || first.Payout != second.Payout || first.BalanceBefore != second.BalanceBefore ||
                first.BalanceAfter != second.BalanceAfter || first.Revision != second.Revision || first.Symbols == null ||
                second.Symbols == null || first.Symbols.Length != second.Symbols.Length) return false;
            for (int index = 0; index < first.Symbols.Length; index++)
                if (first.Symbols[index] != second.Symbols[index]) return false;
            return true;
        }
    }
}
