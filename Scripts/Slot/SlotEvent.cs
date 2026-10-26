using System;
using UnityEngine;
using Wooduduk.Slot.Machine;

namespace Wooduduk.Slot
{
    public enum CostKind
    {
        Spin,
        Fire,
        Card,
    }
    // 슬롯 연출 이벤트 채널
    // 상위 슬롯 컨트롤러가 결과를 바독 여기서 Raise
    // 애니/VFX/SFX가 구독하면 됨
    public static class SlotEvent
    {
        // 도끼 스윙 애니메이션 용
        public static event Action OnAxeSwing;
        public static void RaiseAxeSwing() => OnAxeSwing?.Invoke();

        // 릴 회전 애니 시작, 도끼 타격 SFX
        public static event Action OnSpinStarted;
        public static void RaiseSpinStarted() => OnSpinStarted?.Invoke();

        // 개별 릴 정지 이벤트
        public static event Action<int> OnReelStopped;
        public static void RaiseReelStopped(int reelIndex) => OnReelStopped?.Invoke(reelIndex);

        // 릴 결정. 각 릴을 Reels[i] 심볼에 멈추는 정지 애니. 
        public static event Action<SpinResult> OnReelsDecided;
        public static void RaiseReelsDecided(SpinResult result) => OnReelsDecided?.Invoke(result);

        // 스핀이 완전히 완료될 때
        public static event Action OnSpinCompleted;
        public static void RaiseSpinCompleted() => OnSpinCompleted?.Invoke();

        // 판정 확정. 등급별 셰이크/퍼펙트/막걸리/좀벌레 실패 VFX·SFX, 콤보 UI. 
        public static event Action<SpinOutcome> OnSpinJudged;
        public static void RaiseSpinJudged(SpinOutcome outcome) => OnSpinJudged?.Invoke(outcome);

        // 정산. 정산 연출, 미정산→banked 이동 UI. 
        public static event Action<RunProgress, long> OnSettled;
        public static void RaiseSettled(RunProgress run, long movedWood) => OnSettled?.Invoke(run, movedWood);

        // 모닥불 투입. 불 키우기 연출, 체온 게이지 상승 반응. 
        public static event Action<RunProgress> OnFireFed;
        public static void RaiseFireFed(RunProgress run) => OnFireFed?.Invoke(run);

        // 체온 위험 구간 진입(1회). 심장박동 SFX, 붉은 비네트 VFX. 
        public static event Action<RunProgress> OnBodyTempCritical;
        public static void RaiseBodyTempCritical(RunProgress run) => OnBodyTempCritical?.Invoke(run);
        // 체온 위험 구간 이탈(1회성). 심박 정지·비네트 해제 등 복귀 연출용
        public static event Action<RunProgress> OnBodyTempRecovered;
        public static void RaiseBodyTempRecovered(RunProgress run) => OnBodyTempRecovered?.Invoke(run);
        // 체온 0 → 사망/게임 종료. 결과 화면, 사망 연출. payload: 최종 재화, 런 상태. 
        public static event Action<RunProgress> OnDeath;
        public static void RaiseDeath(RunProgress run) => OnDeath?.Invoke(run);

        // 효과 발동(막걸리 발동/카드 장착). 버프·디버프 아이콘 표시, 발동 연출. 
        public static event Action<SlotEffect> OnEffectApplied;
        public static void RaiseEffectApplied(SlotEffect effect) => OnEffectApplied?.Invoke(effect);
        // 런 리셋/부활. 결빙 연출 정리/HUD 초기화 
        public static event Action<RunProgress> OnRunReset;
        public static void RaiseRunReset(RunProgress run) => OnRunReset?.Invoke(run);
        // 모닥불 점화/소화 / 화염 VFX, SFX
        public static event Action<Vector3> OnCampfireLit;    
        public static void RaiseCampfireLit(Vector3 pos) => OnCampfireLit?.Invoke(pos);
        public static event Action OnCampfireOut;
        public static void RaiseCampfireOut() => OnCampfireOut?.Invoke();
        // 모닥불 연료 변동
        public static event Action<int, int, int> OnFireFuelChanged;
        public static void RaiseFireFuelChanged(int current, int max, int cost) => OnFireFuelChanged?.Invoke(current, max, cost);

        // 콤보 마일스톤 도달(5/10/20/30…)
        public static event Action<int, int> OnComboMilestone;
        public static void RaiseComboMilestone(int milestone, int index) => OnComboMilestone?.Invoke(milestone, index);

        // 비용 부족/사용 불가 거부
        public static event Action<CostKind> OnCostRejected;
        public static void RaiseCostRejected(CostKind kind) => OnCostRejected?.Invoke(kind);


    }
}


