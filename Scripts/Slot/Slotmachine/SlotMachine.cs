using Wooduduk.Data.DataSO;
using static Wooduduk.Slot.RunProcessor;
using static Wooduduk.Slot.SpinJudge;
using System;
using UnityEngine;

namespace Wooduduk.Slot.Machine
{
    // 한 스핀의 패키징된 결과
    // 추첨 + 판정 + 상태 반영을 한 번에 묶음
    // 연출에 필요한 값 포함
    public struct SpinOutcome
    {
        public SpinResult Result;       // 릴별 심볼
        public SpinJudgment Judgment; // 명중/등급/퍼펙트/막걸리/실패사유
        public SpinApplied Applied;     // 이번 스핀 장작/소실/보험/막걸리 발동

        // 반영 후 런 상태 스냅샷 / HUD, 연출
        public int Combo;
        public long UnsettledWood;
        public long BankedWood;

        public SlotEffect RolledMGL;    // 이번 스핀 막걸리로 새로 발동된 효과 / 없으면 Null
    }
    // 슬롯 머신 코어
    // 추첨, 판정, 진행, 체온, 효과를 하나의 진입점으로 묶음
    public sealed class SlotMachine
    {
        private readonly IDeterministicRng _rng;
        private readonly SlotDrawEngine _engine;
        private readonly SlotJudgeConfig _judgeCfg;
        private readonly RunProcessor _processor;
        private readonly EffectRegistry _effects;
        private readonly IMGLSource _mglRoller;
        private readonly RunProgress _run;
        private readonly int _baseReelCount;
        private const int MAX_REELS = 6;   // 릴 상한(기본5 + 4번째릴 카드1). 방어1(TryBuyCard)·방어2(Spin) 공유.

        public RunProgress Run => _run;
        public EffectRegistry Effects => _effects; // 카드 추가용 (상점/로비)
        public int CardCost => _processor.CardCost(_run.CardBuyCount); // 현재 카드 구매가
        public void SetCardCostOverride(int cost) => _processor.SetCardCostOverride(cost);
        public bool IsAlive => _run.IsAlive;
        //public bool IsCritical => _survival.IsCritical(_run);

        public SlotMachine(
            IDeterministicRng rng,
            SlotDrawEngine engine,
            SlotJudgeConfig judgeCfg,
            RunProcessor processor,
            EffectRegistry effects,
            MGLRoller mglRoller,
            RunProgress run,
            int reelCount = 5)
        {
            _rng = rng;
            _engine = engine;
            _judgeCfg = judgeCfg;
            _processor = processor;
            _effects = effects;
            _mglRoller = mglRoller;
            _run = run;
            _baseReelCount = reelCount < 1 ? 5 : reelCount;
            if (_baseReelCount > MAX_REELS)
                Debug.LogError($"[SlotMachine] baseReelCount({_baseReelCount}) > MAX_REELS({MAX_REELS}) — 릴 상한이 기본 릴 수보다 작음. 4번째릴 카드 영구 비활성·매 스핀 클램프 발생. 데이터시트 reelCount 확인.");

        }
        // 스핀 : 추첨 -> 판정 -> 효과 보정 반영 -> 막걸리 롤/ 첫 스핀에 런 시작
        public SpinOutcome Spin()
        {
            // 싱글: 첫 스윙에 런 시작(체온 드레인 ON). 멀티: AutoStartOnSpin=false라 여기서 안 켬 →
            //   MultiSessionCoordinator가 서버시간 GO에 Started를 켠다(카운트다운/대기 중 조기 드레인 방지).
            if (_run.AutoStartOnSpin) _run.Started = true;
            _run.FireFeedStreak = 0;
            // 효과 반영된 릴 수
            int extra = _effects.TotalExtraReels();
            int reels = _baseReelCount + (extra < 0 ? 0 : extra);
            if (reels > MAX_REELS)
            {
                Debug.LogWarning($"[릴클램프] reels {reels}→{MAX_REELS} 상한 적용");
                reels = MAX_REELS;
            }

            var ctx = new SpinContext(_run.SpinIndex, _run.Combo, _run.DryStreak, reels);
            SpinResult result = _engine.Draw(_rng, in ctx);
            SpinJudgment judgment = SpinJudge.Judge(result, _judgeCfg);

            // ── (1) 무거운도끼: 명중일 때만 강제 약스윙(×1.0). 헛스윙/좀벌레 실패는 손대지 않음. ──
            //     약스윙 강제 = 퍼펙트/불빠따 보너스 무효 + 이번 스핀 불빠따 자체 취소(배율·페널티 둘 다).
            if (judgment.IsHit && _effects.AnyForcesWeakSwing())
            {
                judgment.Tier = SwingTier.Weak;
                judgment.EffectiveSwingCount = 1;
                judgment.BaseMultiplierBp = _judgeCfg._tierMultiplierBp[1]; // ×1.0
                judgment.IsPerfect = false;
                judgment.FireBatActive = false;
            }
            // ── (2) 기름통: 불빠따 배율 추가 증폭(불빠따 살아있을 때만). ──
            if (judgment.FireBatActive)
            {
                long fbBonus = _effects.CombinedFireBatMultBonusBp();
                if (fbBonus != 10000)
                    judgment.BaseMultiplierBp = judgment.BaseMultiplierBp * fbBonus / 10000;
            }

            var mods = new SpinModifiers
            {
                WoodMultiplierBp = _effects.CombinedWoodMultiplierBp(),
                AcornValueMultiplier = _effects.CombinedAcornMultiplier(),
                PreventFailPenalty = !judgment.IsHit && _effects.TryConsumeFailGuard(), // 실패일 때만 보험 소모
                ExtraComboOnHit = _effects.CombinedExtraComboOnHit(),
            };
            SpinApplied applied = _processor.Apply(_run, judgment, mods);

            // 기존 임시 효과를 카운트 다운 -> 새 막걸리 효과는 다음 스핀 부터
            _effects.TickDown(Run);

            // ── (3) 불빠따 페널티: 다음 스핀 명중률↓ 임시효과 추가(기름통이 페널티 더 강화). ──
            if (judgment.FireBatActive)
            {
                int penaltyPct = _judgeCfg._fireBatPenaltyPercent - _effects.CombinedFireBatPenaltyBonusPercent();
                if (penaltyPct < 0) penaltyPct = 0;
                _effects.Add(new FireBatPenaltyEffect(1, penaltyPct), _run);
            }

            SlotEffect rolled = null;
            if (judgment.MGLTriggered)
            {
                rolled = _mglRoller.Roll(_rng);
                if (rolled != null)
                {
                    // ── (4) 막걸리 항아리: 디버프 막걸리면 지속 절반(⌈dur/2⌉). ──
                    if (rolled.IsDebuff && _effects.AnyHalvesDebuffDuration())
                        rolled.RemainingSpins = (rolled.RemainingSpins + 1) / 2;
                    _effects.Add(rolled, _run);
                }
            }
            return new SpinOutcome
            {
                Result = result,
                Judgment = judgment,
                Applied = applied,
                Combo = _run.Combo,
                UnsettledWood = _run.UnsettledWood,
                BankedWood = _run.BankedWood,
                RolledMGL = rolled,
            };
        }
        public long Settle() => _processor.Settle(_run);
        // 모닥불 투입 : 장작 소모 + 횟수 증가 + 체온 회복. 성공 여부 반환
        public bool FeedFire() => _processor.TryFeedFire(_run);

        public bool PaySpin() => _processor.TryPaySpin(_run);
        public void Reseed(ulong seed) => _rng.Restore(RngFactory.StateFromSeed(seed));
        public bool TryBuyCard(SlotEffect card)
        {
            if (card == null) return false;

            // 릴 추가 카드 상한 가드 — 장작 차감 전 검사(거절 시 장작 보존)
            if (card.ExtraReels > 0 &&
                _baseReelCount + _effects.TotalExtraReels() + card.ExtraReels > MAX_REELS)
            {
                Debug.LogWarning($"[릴가드] 릴 상한 도달 — 카드 거절: {card.Id} ({_baseReelCount}+{_effects.TotalExtraReels()}+{card.ExtraReels} > {MAX_REELS})");
                return false;
            }

            if (!_processor.TryBuyCard(_run)) return false;
            _effects.Add(card, _run);
            return true;
        }
  
    }
    // 슬롯 머신 기본 설정
    // 모든 config, 풀은 임시 -> TODO : SO/시트로 교체
    public static class SlotMachineFactory
    {
        public static SlotMachine CreateDefault(RunContext ctx, DataManagerSO data)
        {
            if (data == null)
                throw new InvalidOperationException("DataManagerSO 미연결");
            var gb = data._gameBalance?._data
                ?? throw new InvalidOperationException("GameBalanceDataSO 미연결");

            var rng = RngFactory.FromSeed(ctx.Seed);
            var balance = gb._slotBalance;
            var catalog = data.BuildSymbolCatalog();
            var registry = new EffectRegistry();
            int reel = data._roulette._data._reelData._reelCount;

            var pipeline = new WeightPipeline()
                .Add(new ComboDifficultyStage(balance))
                .Add(new LuckCompensationStage(balance))
                .Add(new EffectsWeightStage(registry));

            var engine = new SlotDrawEngine(catalog, pipeline);
            var roller = MGLPool.CreateRoller(data);
            var judge = gb._slotJudge;

            return new SlotMachine(rng, engine, judge, ctx.Economy, 
                                   registry, roller, ctx.Run, reel);
        }
    }
}
