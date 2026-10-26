using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Wooduduk.Data.DataSO;
using Wooduduk.Data.Service;
using Wooduduk.Player;

namespace Wooduduk.Slot.Machine
{
    // 슬롯 머신 컨트롤러
    // 결과는 Swing 시 즉시 결정
    // 체온은 실시간 감소 -> 추후 분리 예정
    public class SlotmachineController : MonoBehaviour
    {
        [SerializeField] private PlayerSurvival _survival;
        [SerializeField] private CampfireController _campfire;
        [Header("MachineSetting")]
        [SerializeField] private DataManagerSO _data;
        [SerializeField] private string _seed = "Local_Test";
        [SerializeField] private bool _initOnAwake = true;
        [SerializeField] private bool _randomizeSeed = true;    // 싱글/로컬 매 런 새 시드.
        [SerializeField] private RunMode _mode = RunMode.Single;    // 싱글 = 로컬 랜덤 / 멀티 = 첫 스핀 공유 시드
        [SerializeField] private float _autoCompleteSeconds = 0f; // n초 후 회전 종료. CompleteSpin 미연결 시 락 방지용(테스트용)
        [Header("Revive")]
        [SerializeField] private bool _autoReviveOnDeath = true;    // 싱글 : 사망 후 자동 초기화/부활
        [SerializeField] private float _reviveDelaySeconds = 3f;    // 사망 연출 시간

        private Coroutine _reviveRoutine;

        private RunContext _ctx;
        private SlotMachine _machine;
        private bool _spinning;
        private bool _spinLocked; // P1: 멀티 카운트다운 중 스핀 봉인(코디네이터 제어). 기본 false = 단일플레이/기존 씬 무영향.
        private float _spinTimer;
        private bool _branched;
        private int[] _comboMilestones;
        private int _lastCombo;


        // 릴 회전 중 여부
        public SlotMachine Machine => _machine;
        public bool IsSpinning => _spinning;

        // P1: 멀티 세션 코디네이터가 카운트다운 중 lock(true), 시작 시 unlock(false).
        // 단일플레이/기존 씬은 호출 안 하면 항상 false → 무영향.
        public bool IsSpinLocked => _spinLocked;
        public void SetSpinLocked(bool locked) => _spinLocked = locked;
        public RunProgress Run => _ctx?.Run;
        public EffectRegistry Effects => _machine != null ? _machine.Effects : null;
        // 활성 효과 / UI
        public IReadOnlyList<SlotEffect> ActiveEffects => _machine?.Effects.Active;
        // 체온 게이지 정규화 / UI
        public float BodyTempNormalized
            => (_ctx != null && _ctx.Run.MaxBodyTemp > 0f)
            ? Mathf.Clamp01(_ctx.Run.BodyTemp / _ctx.Run.MaxBodyTemp)
            : 1f;

        private void Awake()
        {
            if (_initOnAwake) Init(ResolveInitSeed(_mode), _mode);
        }
        private void OnEnable() { SlotEvent.OnDeath += HandleDeath; }
        private void OnDisable() { SlotEvent.OnDeath -= HandleDeath; }
        private void Update()
        {
            if (_machine == null) return;
            // 자동 완료 테스트
            if (_spinning && _autoCompleteSeconds > 0f)
            {
                _spinTimer += Time.deltaTime;
                if (_spinTimer >= _autoCompleteSeconds) CompleteSpin();
            }
        }
        public void Init(string seed) => Init(seed, _mode);
        // 초기화
        public void Init(string seed, RunMode mode)
        {
            CardEvent.ResetCardPriceCache();
            _mode = mode;
            _branched = false;
            var gb = _data != null ? _data._gameBalance?._data : null;
            if (gb == null)
            {
                Debug.LogError("DataManagerSO/_gameBalance 미연결. 슬롯 초기화 불가");
                return;
            }
            _comboMilestones = gb._slotEconomy._comboMilestones;
            _lastCombo = 0;   // 부활/리셋 콤보 동기화 겸함
            _ctx = RunContextFactory.CreateDefault(seed, gb._slotEconomy, gb._survival); // 폴백 제거
            // 멀티: Started를 코디네이터(서버시간 GO)만 켜도록 스핀 자동시작 차단. 싱글: 첫 스윙에 시작(기존).
            _ctx.Run.AutoStartOnSpin = (mode != RunMode.Multi);
            Debug.Log($"[SlotmachineController] Init mode={mode}, AutoStartOnSpin={_ctx.Run.AutoStartOnSpin} (멀티면 코디네이터가 Started 소유 → GO 전 체온 드레인 없음).");
            _machine = SlotMachineFactory.CreateDefault(_ctx, _data);                     // reelCount 인자 제거
            if (_survival != null) _survival.Bind(_ctx);
            if (_campfire != null) _campfire.Bind(_ctx);
            CardEvent.RaiseCardPriceChanged(_machine.CardCost);
            _spinning = false;
            _spinTimer = 0f;
        }
        // 모드별 초기시드 해석.
        // 멀티 시드는 여기서 만들지 않고 Init(serverSeed, Multi)로 주입
        private string ResolveInitSeed(RunMode mode)
            => (mode == RunMode.Single && _randomizeSeed)
            ? System.Guid.NewGuid().ToString("N")
            : _seed;
        // 멀티 분기용 클라 로컬 엔트로피
        private static ulong NewBranchSeed()
        {
            ulong hi = (uint)System.Guid.NewGuid().GetHashCode();
            ulong lo = (uint)System.Guid.NewGuid().GetHashCode();
            return (hi << 32) | lo;
        }
        // 스윙 버튼이지만 레버 돌리는 효과. 근데 도끼로 치는 건 맞으니까 
        public void Swing()
        {
            //Debug.Log("[4.Swing] 진입");
            if (_machine == null) Init(ResolveInitSeed(_mode), _mode);
            if (_spinLocked) { Debug.Log("[4.Swing] 스핀 잠금 — 무시(멀티 카운트다운/세션 시작 대기)"); return; }
            if (_spinning || !_machine.IsAlive) return;
            //{ Debug.Log($"[4.Swing] 차단: _spinning={_spinning}, IsAlive={_machine.IsAlive}"); return; }
            if (!_machine.PaySpin()) { SlotEvent.RaiseCostRejected(CostKind.Spin); return; }

            _spinning = true;
            _spinTimer = 0f;
            SpinOutcome outcome = _machine.Spin();  // 클릭 즉시 결과 결정
            // 멀티 : 첫 스핀 공유시드 전원 동일 -> 직후 분기 -> 이후 싱글과 동일
            if (_mode == RunMode.Multi && !_branched && _machine.Run.SpinIndex == 1)
            {
                _machine.Reseed(NewBranchSeed());
                _branched = true;
            }
            //Debug.Log("[4.Swing] RaiseSpinStarted 직전 (여기까지 오면 로직 정상)");
            SlotEvent.RaiseSpinStarted();
            SlotEvent.RaiseReelsDecided(outcome.Result);        // 릴 정지 타겟 심볼
            SlotEvent.RaiseSpinJudged(outcome);                 // 등급/퍼펙트/막걸리/실패 VFX, SFX
            if (outcome.RolledMGL != null)
            {
                SlotEvent.RaiseEffectApplied(outcome.RolledMGL);// 막걸리 발동 효과 아이콘/연출
            }
            if (_comboMilestones != null)
            {
                for (int i = 0; i < _comboMilestones.Length; i++)
                {
                    int m = _comboMilestones[i];
                    if (_lastCombo < m && outcome.Combo >= m) SlotEvent.RaiseComboMilestone(m, i);
                }
            }
            _lastCombo = outcome.Combo;
        }
        // 릴 정지 애니메이션 완료 시 다음 스윙 허용
        public void CompleteSpin()
        {
            _spinning = false;
            _spinTimer = 0f;
        }
        // 정산 : 미정산 -> 사용 가능 장작
        public void Settle()
        {
            if (_machine == null) return;
            long moved = _machine.Settle();
            SlotEvent.RaiseSettled(_machine.Run, moved);
            _lastCombo = 0;
        }
        // 모닥불에 장작 투입 : 장작 소모 + 투입 횟수 증가 + 체온 회복
        public void FeedFire()
        {
            if (_machine == null) return;
            if (_machine.FeedFire()) SlotEvent.RaiseFireFed(_machine.Run);
            else SlotEvent.RaiseCostRejected(CostKind.Fire);   // 장작 부족 에러음
        }
        // 카드 장착
        public bool BuyCard(SlotEffect card)
        {
            if (_machine == null || card == null) return false;
            if (!_machine.TryBuyCard(card)) { SlotEvent.RaiseCostRejected(CostKind.Card); return false; }
            SlotEvent.RaiseEffectApplied(card);
            CardEvent.RaiseCardPriceChanged(_machine.CardCost);
            return true;
        }
        // id로 카드 생성(SO 수치) 후 구매. 인터랙터블 진입점. 알 수 없는 id/머신 없음 → false.
        public bool BuyCardById(string cardId)
        {
            if (_machine == null) return false;
            var card = CardCatalog.Create(cardId, _data);   // CardCatalog: Wooduduk.Slot (using 추가)
            if (card == null) { Debug.LogWarning($"[SlotmachineController] BuyCardById: 알 수 없는 cardId={cardId}"); return false; }
            return BuyCard(card);                           // 기존 BuyCard → TryBuyCard(장작 차감) + Effects.Add
        }
        private void HandleDeath(RunProgress run)
        {
            // 멀티는 서버 권위. 점수 제출/티어/고스트는 네트워크가 검증 후 처리.
            // 클라 로컬 컨트롤러는 싱글에서만 직접 종료(이중 제출·미검증 반영 방지).
            if (_mode != RunMode.Single) return;
            //var gb = _data._gameBalance._data;
            //var score = new SlotScoreCalculator(gb._slotScore);
            //RunEndService.EndRun(run, score, RunMode.Single);   // RunMode: 신규 enum(Single/Multi)

            if (_autoReviveOnDeath && _reviveRoutine == null)
                _reviveRoutine = StartCoroutine(ReviveRoutine());
        }
        private IEnumerator ReviveRoutine()
        {
            yield return new WaitForSecondsRealtime(_reviveDelaySeconds);
            _reviveRoutine = null;
            Revive();
        }
        public void Revive()
        {
            Init(ResolveInitSeed(_mode), _mode);
            SlotEvent.RaiseRunReset(_ctx.Run);
        }

        // 멀티용 자동 부활 멈춤
        public void SetAutoRevive(bool value)
        {
            _autoReviveOnDeath = value;
        }
        public void SetCardCostOverride(int cost)
        {
            if (_machine == null) return;
            _machine.SetCardCostOverride(cost);
            CardEvent.RaiseCardPriceChanged(_machine.CardCost);
        }
    }
}