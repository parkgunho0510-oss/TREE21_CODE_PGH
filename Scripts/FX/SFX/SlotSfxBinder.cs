
using System;
using UnityEngine;
using Wooduduk.SFX;
using Wooduduk.Slot;          
using Wooduduk.Slot.Machine; 

namespace Wooduduk.Audio
{
    // SlotEvent -> SoundManager 라우팅 어댑터
    // 슬롯 사운드 매핑
    public sealed class SlotSfxBinder : MonoBehaviour
    {
        private static SoundManager S => SoundManager.Instance;
        [SerializeField] private Transform _machineAnchor;  // 슬롯머신 본체: 타격/레버/심볼매치/에러
        [SerializeField] private Transform _cardAnchor;     // 카드 진열대: 호버/구매
        [SerializeField] private Transform _campfireAnchor; // 모닥불: FireFed/FireOut 폴백(Lit pos가 오면 그걸 캐시)
        [SerializeField] private Transform _settleAnchor;    // 정산 큐브(비우면 머신 위치)

        private Vector3 _campfirePos;      // OnCampfireLit가 준 실제 위치 캐시(앵커보다 우선)
        private bool _campfirePosValid;
        private SymbolId[] _decidedReels; // 이번 스핀 릴 결과 
        private int[] _symbolSeen;
        private static readonly int SymbolCount = Enum.GetValues(typeof(SymbolId)).Length;
        private bool _pendingMakgeolli;   // 이번 스핀 막걸리 발동 여부(판정 후 발동)
        private SpinOutcome? _pendingOutcome;    // 판정 캐시
        private int _pendingMilestoneIndex = -1; // 콤보 마일스톤 캐시

        private static SfxId LadderSfxFor(SymbolId s)
        {
            switch (s)
            {
                case SymbolId.SWING: return SfxId.SwingHit;
                case SymbolId.PERFECT: return SfxId.Perfect;
                case SymbolId.FRIEBAT: return SfxId.FireBatOn;
                case SymbolId.ACORN: return SfxId.AcornPop;
                case SymbolId.LUCK: return SfxId.LuckWild;
                case SymbolId.MAKGEOLLI: return SfxId.Makgeolli;
                    case SymbolId.KNOT: return SfxId.Knot;
                    case SymbolId.WORM: return SfxId.WormFail;
                default: return SfxId.None;   // KNOT/WORM/MAKGEOLLI
            }
        }

        // 앵커 미연결 시 자기 위치 폴백(소리 누락보다 오배치가 낫다)
        private Vector3 MachinePos => _machineAnchor ? _machineAnchor.position : transform.position;
        private Vector3 CardPos => _cardAnchor ? _cardAnchor.position : MachinePos;
        private Vector3 SettlePos => _settleAnchor ? _settleAnchor.position : MachinePos;
        private Vector3 CampfirePos => _campfirePosValid ? _campfirePos
                                     : _campfireAnchor ? _campfireAnchor.position : MachinePos;

        private void OnEnable()
        {
            SlotEvent.OnAxeSwing += HandleAxeSwing;
            SlotEvent.OnSpinStarted += HandleSpinStarted;
            SlotEvent.OnSpinJudged += HandleSpinJudged;
            //SlotEvent.OnEffectApplied += HandleEffectApplied;
            SlotEvent.OnSettled += HandleSettled;
            SlotEvent.OnFireFed += HandleFireFed;
            SlotEvent.OnBodyTempCritical += HandleCritical;
            SlotEvent.OnDeath += HandleDeath;
            SlotEvent.OnCampfireLit += HandleCampfireLit;
            SlotEvent.OnCampfireOut += HandleCampfireOut;
            SlotEvent.OnReelsDecided += HandleReelsDecided;
            SlotEvent.OnComboMilestone += HandleComboMilestone;
            SlotEvent.OnCostRejected += HandleCostRejected;
            SlotEvent.OnRunReset += HandleRunReset;
            CardEvent.OnCardHover += HandleCardHover;
            CardEvent.OnCardBought += HandleCardBought;
            SlotEvent.OnReelStopped += HandleReelStopped;
            SlotEvent.OnBodyTempRecovered += HandleRecovered;
            SlotEvent.OnSpinCompleted += HandleSpinCompleted;
        }
        private void OnDisable()
        {
            SlotEvent.OnAxeSwing -= HandleAxeSwing;
            SlotEvent.OnSpinStarted -= HandleSpinStarted;
            SlotEvent.OnSpinJudged -= HandleSpinJudged;
            //SlotEvent.OnEffectApplied -= HandleEffectApplied;
            SlotEvent.OnSettled -= HandleSettled;
            SlotEvent.OnFireFed -= HandleFireFed;
            SlotEvent.OnBodyTempCritical -= HandleCritical;
            SlotEvent.OnDeath -= HandleDeath;
            SlotEvent.OnCampfireLit -= HandleCampfireLit;
            SlotEvent.OnCampfireOut -= HandleCampfireOut;
            SlotEvent.OnReelsDecided -= HandleReelsDecided;
            SlotEvent.OnComboMilestone -= HandleComboMilestone;
            SlotEvent.OnCostRejected -= HandleCostRejected;
            SlotEvent.OnRunReset -= HandleRunReset;
            CardEvent.OnCardHover -= HandleCardHover;
            CardEvent.OnCardBought -= HandleCardBought;
            SlotEvent.OnReelStopped -= HandleReelStopped;
            SlotEvent.OnBodyTempRecovered -= HandleRecovered;
            SlotEvent.OnSpinCompleted -= HandleSpinCompleted;
        }

        private void HandleAxeSwing() { if (S) S.PlaySfxAt(SfxId.AxeSwing, MachinePos); }
        private void HandleSpinStarted() { if (S) S.PlaySfxAt(SfxId.LeverPull, MachinePos); }
        private void HandleReelStopped(int reelIndex)
        {
            if (S == null || _decidedReels == null || _symbolSeen == null) return;
            if (reelIndex < 0 || reelIndex >= _decidedReels.Length) return;

            var symbol = _decidedReels[reelIndex];
            var sfx = LadderSfxFor(symbol);
            if (sfx == SfxId.None) return;

            int seen = ++_symbolSeen[(int)symbol];        // 같은 심볼 n번째 등장 = n계단
            float pitchScale = 1f;
            var hp = S.Catalog != null ? S.Catalog._hitPitch : null;
            if (hp != null)
            {
                int step = Mathf.Clamp(seen, 1, hp._maxSwingSteps);
                pitchScale = hp._basePitch + (step - 1) * hp._pitchPerSwing;
            }
            S.PlaySfxAt(sfx, MachinePos, pitchScale);
        }
        // 릴에 뜬 특수 심볼 알림음
        private void HandleReelsDecided(SpinResult r)
        {
            if (r == null || r.Reels == null) return;

            _decidedReels = r.Reels;
            if (_symbolSeen == null) _symbolSeen = new int[SymbolCount];
            else System.Array.Clear(_symbolSeen, 0, _symbolSeen.Length);   // 스핀마다 계단 리셋

            // 옹이: 결과 시점 알림음(등장 여부만, 스핀당 1회)
            if (S == null) return;
            for (int i = 0; i < r.Reels.Length; i++)
                if (r.Reels[i] == SymbolId.KNOT) { S.PlaySfxAt(SfxId.Knot, MachinePos); break; }
        }

        // 판정 : 심볼매치별 + 스윙 등급 피치 사다리
        private void HandleSpinJudged(SpinOutcome outcome) => _pendingOutcome = outcome;
        private void HandleComboMilestone(int milestone, int index) => _pendingMilestoneIndex = index;

        // 모닥불/슬롯/카드 공용 에러음(kind별 다른 음 원하면 여기서 switch로 분기)

        private void HandleCostRejected(CostKind kind) 
        {
            if (S == null) return;
            switch (kind)
            {
                case CostKind.Fire: S.PlaySfxAt(SfxId.CostReject, CampfirePos); break;
                case CostKind.Card: S.PlaySfxAt(SfxId.CostReject, CardPos); break;
                default: S.PlaySfxAt(SfxId.CostReject, MachinePos); break; // Spin
            }
        }
        private void HandleRunReset(RunProgress _)
        {
            _campfirePosValid = false;
            if (S == null) return;
            S.StopAmbient(AmbientId.HeartbeatCritical);   // 리셋 잔류 방지
            _pendingMakgeolli = false;
            _pendingMilestoneIndex = -1;
            S.PlaySfx(SfxId.Revive);
        }
        private void HandleCardHover(bool on) { if (S) S.PlaySfxAt(on ? SfxId.CardHoverOn : SfxId.CardHoverOff, CardPos); }
        private void HandleCardBought(string _, bool ok) { if (ok && S) S.PlaySfxAt(SfxId.CardBuy, CardPos); }

        //private void HandleEffectApplied(SlotEffect e)
        //{
        //    if (e == null || e.IsPermanent) return;   // 카드(영구)는 OnCardBought 담당
        //    _pendingMakgeolli = true;
        //}
        private void HandleSpinCompleted()
        {
            if (S == null || _pendingOutcome == null) return;
            var o = _pendingOutcome.Value;     // 값 꺼내기
            _pendingOutcome = null;            // 소비 처리(중복 재생 방지)

            var j = o.Judgment;                // SpinJudgment는 class라 null 체크 가능
            if (j == null) return;

            // 결과음 우선순위
            bool luck = false;
            int knotCount = 0;
            if (_decidedReels != null)
                for (int i = 0; i < _decidedReels.Length; i++)
                {
                    if (_decidedReels[i] == SymbolId.LUCK) luck = true;
                    else if (_decidedReels[i] == SymbolId.KNOT) knotCount++;
                }
            bool knotMajority = _decidedReels != null && knotCount > _decidedReels.Length;
            // 결과음 줄 순서 = 우선순위
            if (j.FailReason == FailReason.WORM) S.PlaySfxAt(SfxId.ResultWorm, MachinePos);        // 1. 좀벌레 실패
            else if (j.MGLTriggered) S.PlaySfxAt(SfxId.MakgeolliDrink, MachinePos);  // 2. 막걸리 발동
            else if (!j.IsHit) S.PlaySfxAt(knotMajority ? SfxId.ResultKnot : SfxId.WhiffFail, MachinePos);             // 3. 꽝(옹이 탓이면 전용음)
                                                               
            else if (j.IsPerfect) S.PlaySfxAt(SfxId.ResultPerfect, MachinePos);   // 4. 퍼펙트
            else if (j.FireBatActive) S.PlaySfxAt(SfxId.ResultFireBat, MachinePos);   // 5. 불빠따
            else if (luck) S.PlaySfxAt(SfxId.ResultLuck, MachinePos);      // 6. 행운 복제 명중
            else S.PlaySfxAt(SfxId.ResultHit, MachinePos);       // 7. 일반 명중

            // 독립 레이어(결과음과 겹쳐도 의미 충돌 없음)
            if (j.AcornCount > 0) S.PlaySfx(SfxId.ResultAcorn);   // 도토리 즉발(실패해도 획득)
            if (j.ComboBreak) S.PlaySfx(SfxId.ComboLost);
            else if (j.ComboDelta > 0) S.PlaySfx(SfxId.ComboTick);

            // 콤보 마일스톤
            if (_pendingMilestoneIndex >= 0)
            {
                int idx = _pendingMilestoneIndex;
                _pendingMilestoneIndex = -1;
                float pitchScale = 1f;
                var cp = S.Catalog != null ? S.Catalog._comboPitch : null;
                if (cp != null)
                {
                    int step = Mathf.Clamp(idx + 1, 1, cp._maxSwingSteps);
                    pitchScale = cp._basePitch + (step - 1) * cp._pitchPerSwing;
                }
                S.PlaySfxAt(SfxId.ComboMilestone, MachinePos, pitchScale); 
            }
        }
        private void HandleSettled(RunProgress _, long movedWood)
        {
            if (S == null) return;
            S.PlaySfxAt(SfxId.Settle, SettlePos);
            S.PlaySfx(SfxId.ComboLost);                 // 정산 = 콤보 리셋
        }
        private void HandleFireFed(RunProgress _) { if (S) S.PlaySfxAt(SfxId.FireFeed, CampfirePos); }
        private void HandleCampfireLit(Vector3 pos)
        {
            _campfirePos = pos; _campfirePosValid = true;
            if (S) S.PlayAmbientAt(AmbientId.Campfire, pos);
        }
        private void HandleCampfireOut()
        {
            if (S == null) return;
            S.PlaySfxAt(SfxId.FireOut, CampfirePos);                    // 꺼지는 소리(단발)
            S.StopAmbient(AmbientId.Campfire);           // 루프 정지
        }
        private void HandleCritical(RunProgress _)
        {
            if (S == null) return;
            S.PlayAmbient(AmbientId.HeartbeatCritical);
            S.PlaySfx(SfxId.TempWarn);
        }
        private void HandleRecovered(RunProgress _)
        { 
            if (S) S.StopAmbient(AmbientId.HeartbeatCritical); 
        }
        private void HandleDeath(RunProgress _)
        {
            if (S == null) return;
            S.PlaySfx(SfxId.Death);
            S.StopAmbient(AmbientId.HeartbeatCritical);
        }

    }
}