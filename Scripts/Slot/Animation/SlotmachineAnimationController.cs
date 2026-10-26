using MoreMountains.Feedbacks;
using System.Collections;
using Unity.Services.Authentication;
using UnityEngine;

namespace Wooduduk.Slot.Machine
{
    public class SlotmachineAnimationController : MonoBehaviour
    {

        [Header("Controller Reference")]
        [SerializeField] private SlotmachineController _mainController;
        [SerializeField] private float _RollTimer = 0.5f;

        [Header("Machine Feedbacks")]
        [SerializeField] private MMF_Player _machineShakeFeedback;

        [Header("Reel Settings")]
        [SerializeField] private MMF_Player[] _reelPlayers; // 회전 정지 시 덜컹거림 등의 연출을 위해 유지
        [SerializeField] private float _startDelay = 0.2f;

        [Header("Rotation Settings")]
        [SerializeField] private float _spinSpeed = 3000f; // 회전 속도
        [SerializeField] private float _tensionSpinMultiplier = 0.2f;

        [Header("Result VFX Particles")]
        [SerializeField] private ParticleSystem _perfectParticle; // 퍼펙트
        [SerializeField] private ParticleSystem _acornParticle;   // 도토리
        [SerializeField] private ParticleSystem _wormParticle;    // 좀벌레
        [SerializeField] private ParticleSystem _luckParticle;    // 행운
        [SerializeField] private ParticleSystem _firebatParticle; // 불빠따
        
        [Header("Makgeolli Settings")]
        [SerializeField] private MMF_Player _bottle; // 막걸리 연출용 MMF
        private Vector3 _bottleStartPos; // 원래 위치 기억용
        private Coroutine _makgeolliCoroutine;

        private bool[] _isSpinning; // 각 릴의 회전 상태
        private float[] _currentSpinSpeeds;
        private Vector3[] _startEulers;
        private Vector3 _machineStartEuler;

        private SlotSymbolManager _symbolManager;
        private SlotMachineReel[] _reels;

        private SpinOutcome _cachedOutcome;
        private void Awake()
        {
            if(_mainController == null)
            _mainController = GetComponent<SlotmachineController>();
            _symbolManager = GetComponent<SlotSymbolManager>();
            if (_reelPlayers == null || _reelPlayers.Length == 0)
            {
                Debug.LogError("인스펙터에 _reelPlayers 배열 세팅 안 됨");
                return;
            }

            _isSpinning = new bool[_reelPlayers.Length];
            _currentSpinSpeeds = new float[_reelPlayers.Length];
            _startEulers = new Vector3[_reelPlayers.Length];
            _reels = new SlotMachineReel[_reelPlayers.Length];

            for (int i = 0; i < _reelPlayers.Length; i++)
            {
                if (_reelPlayers[i] == null)
                {
                    Debug.LogError($"[인스펙터 누락] _reelPlayers 배열의 {i}번째 칸이 비어있음");
                    continue;
                }

                _startEulers[i] = _reelPlayers[i].transform.localEulerAngles;

                _reels[i] = _reelPlayers[i].GetComponent<SlotMachineReel>();
                if (_reels[i] == null)
                {
                    Debug.LogError($"{_reelPlayers[i].name} 오브젝트에 SlotMachineReel 컴포넌트가 누락");
                }
            }
           
            _machineStartEuler = _machineShakeFeedback.transform.localEulerAngles;

            if (_bottle != null)
            {
                _bottleStartPos = _bottle.transform.localPosition;
                _bottle.gameObject.SetActive(false);
            }

        }
        private void OnEnable()
        {
            // SlotEvent 구독
            SlotEvent.OnSpinStarted += StartReels;
            SlotEvent.OnReelsDecided += StopReels;
            SlotEvent.OnSpinJudged += HandleSpinJudged; // 판정 결과 캐싱을 위해 추가
            SlotEvent.OnRunReset += HandleRunReset;     // 리셋 시 빛 초기화용
        }

        private void OnDisable()
        {
            SlotEvent.OnSpinStarted -= StartReels;
            SlotEvent.OnReelsDecided -= StopReels;
            SlotEvent.OnSpinJudged -= HandleSpinJudged; // 판정 결과 캐싱을 위해 추가
            SlotEvent.OnRunReset -= HandleRunReset;     // 리셋 시 빛 초기화용
        }

        private void Update()
        {
            // Update 문에서 유니티 내장 Rotate를 사용해 끊김 없는 완벽한 무한 회전 처리
            for (int i = 0; i < _reelPlayers.Length; i++)
            {
                if (_isSpinning[i])
                {
                    // Z축 기준으로 초고속 회전 (원하는 축으로 변경 가능)
                    _reelPlayers[i].transform.Rotate(0, 0, _currentSpinSpeeds[i] * Time.deltaTime);
                }
            }
        }
        private void HandleSpinJudged(SpinOutcome outcome)
        {
            _cachedOutcome = outcome;
        }

        private void HandleRunReset(RunProgress run)
        {
            // 리셋 시 모든 릴의 빛 끄기
            for (int i = 0; i < _reels.Length; i++)
            {
                if (_reels[i] != null) _reels[i].SetSymbolGlow(false, Color.black);
            }

            StopAndClearParticle(_perfectParticle);
            StopAndClearParticle(_acornParticle);
            StopAndClearParticle(_wormParticle);
            StopAndClearParticle(_luckParticle);
            StopAndClearParticle(_firebatParticle);

            ResetMakgeolliBottle();
        }

        public void StartReels()
        {
            SlotEvent.RaiseAxeSwing();
            for (int i = 0; i < _reelPlayers.Length; i++)
            {
                _isSpinning[i] = true;
                _currentSpinSpeeds[i] = _spinSpeed;

                if (_reels[i] != null) _reels[i].SetSymbolGlow(false, Color.black);
            }

            StopAndClearParticle(_perfectParticle);
            StopAndClearParticle(_acornParticle);
            StopAndClearParticle(_wormParticle);
            StopAndClearParticle(_luckParticle);
            StopAndClearParticle(_firebatParticle);

            ResetMakgeolliBottle();

            _machineShakeFeedback?.PlayFeedbacks();
        }

        public void StopReels(SpinResult result)
        {
            for (int i = result.Reels.Length; i < _reelPlayers.Length; i++)
            {
                _isSpinning[i] = false;
                _reelPlayers[i].transform.localRotation = Quaternion.Euler(_startEulers[i].x, _startEulers[i].y, 0f);
            }
            //Debug.Log($"[5.Anim] StartReels 수신 / reelPlayers={(_reelPlayers == null ? -1 : _reelPlayers.Length)}");
            StartCoroutine(StopReelsRoutine(result));
        }

        private IEnumerator StopReelsRoutine(SpinResult result)
        {
            yield return new WaitForSeconds(_RollTimer);

            // 방어3(뷰 최후 방어): 논리 릴 수를 물리 릴 오브젝트(_reelPlayers) 수로 클램프.
            // 방어1(카드가드)·방어2(엔진클램프)가 뚫려 논리릴 > 물리릴이 돼도, 여기서 잘라
            // 아래 정지 루프의 _reelPlayers[i]/_reels[i] 접근이 범위를 넘지 않게 함.
            // → IndexOutOfRangeException 원천 차단 → CompleteSpin 항상 도달 → _spinning 영구락 해소.
            int totalReelCount = Mathf.Min(result.Reels.Length, _reelPlayers.Length);
            if (result.Reels.Length > _reelPlayers.Length)
                Debug.LogWarning($"[릴뷰가드] 논리릴 {result.Reels.Length} > 물리릴 {_reelPlayers.Length} — 물리 수로 클램프(초과 릴 미표시).");

            int normalStopCount = Mathf.Max(0, totalReelCount - 2);

            for (int i = 0; i < normalStopCount; i++)
            {
                _isSpinning[i] = false; // 회전 정지
                _reelPlayers[i].transform.localRotation = Quaternion.Euler(_startEulers[i].x, _startEulers[i].y, 0f);

                SlotEvent.RaiseReelStopped(i);
                if (_reels[i] != null)
                {
                    _reels[i].ChangeSymbol(result.Reels[i], _symbolManager);
                }
                _reelPlayers[i].PlayFeedbacks();

                // 멈출 때의 타격감/효과음 피드백만 여기서 단발성으로 재생
                yield return new WaitForSeconds(_startDelay);
            }


            for (int i = normalStopCount; i < totalReelCount; i++)
            {

                if (i >= _reelPlayers.Length) break;
                _currentSpinSpeeds[i] = _spinSpeed * _tensionSpinMultiplier;

                yield return new WaitForSeconds(1f);

                _isSpinning[i] = false; // 회전 정지
                _reelPlayers[i].transform.localRotation = Quaternion.Euler(_startEulers[i].x, _startEulers[i].y, 0f);

                SlotEvent.RaiseReelStopped(i);

                if (i < result.Reels.Length && _reels[i] != null)
                {
                    _reels[i].ChangeSymbol(result.Reels[i], _symbolManager);
                }

                _reelPlayers[i].PlayFeedbacks();
            }


            _machineShakeFeedback?.StopFeedbacks();

            _machineShakeFeedback.transform.localRotation = Quaternion.Euler(_machineStartEuler);

            string[] logParts = new string[result.Reels.Length];
            for (int i = 0; i < result.Reels.Length; i++)
            {
                logParts[i] = $"[{i}] {result.Reels[i]}";
            }
            Debug.Log($"[슬롯 최종 결과] {string.Join(" | ", logParts)}");

            _mainController.CompleteSpin();
            SlotEvent.RaiseSpinCompleted();

            for (int i = 0; i < result.Reels.Length; i++)
            {
                if (i < _reels.Length && _reels[i] != null)
                {
                    ApplyGlowIfMatched(_reels[i], result.Reels[i]);
                }
            }

            PlayResultVFX(_cachedOutcome.Judgment, result);
        }

        private void ApplyGlowIfMatched(SlotMachineReel reel, SymbolId symbol)
        {
            if (reel == null || _cachedOutcome.Result == null) return;

            var j = _cachedOutcome.Judgment;
            bool highlight = false;
            Color glowColor = Color.black;

            // 좀벌레로 인한 실패 상태일 경우, 다른 모든 조건 무시
            if (j.FailReason == FailReason.WORM)
            {
                if (symbol == SymbolId.WORM)
                {
                    highlight = true;
                    glowColor = new Color(0.6f, 0f, 1f); // 보라색
                }
            }
            else
            {
                // 좀벌레 실패가 아닐 때의 일반 정상 판정
                switch (symbol)
                {
                    case SymbolId.SWING:
                        if (j.EffectiveSwingCount >= 1) { highlight = true; glowColor = Color.white; }
                        break;
                    case SymbolId.ACORN:
                        highlight = true; glowColor = Color.sandyBrown;
                        break;
                    case SymbolId.LUCK:
                        highlight = true; glowColor = Color.green;
                        break;
                    case SymbolId.PERFECT:
                        if (j.IsPerfect) { highlight = true; glowColor = Color.yellow; }
                        break;
                    case SymbolId.FRIEBAT:
                        if (j.FireBatActive) { highlight = true; glowColor = Color.red; }
                        break;
                    case SymbolId.MAKGEOLLI:
                        if (j.MGLTriggered) { highlight = true; glowColor = Color.blue; }
                        break;
                }
            }

            reel.SetSymbolGlow(highlight, glowColor);
        }


        private void PlayResultVFX(SpinJudgment j, SpinResult result)
        {
            if (j == null) return;

            // 1. 좀벌레 실패 기믹 발동 시 다른 모든 파티클을 무시하고 좀벌레만 재생
            if (j.FailReason == FailReason.WORM)
            {
                _wormParticle?.Play(true);
                return;
            }

            // 2. 일반 정상 판정 시 해당 기믹 파티클 재생 (중복 재생 허용 구조)
            if (j.IsPerfect) _perfectParticle?.Play(true);
            if (j.AcornCount > 0) _acornParticle?.Play(true);
            if (j.FireBatActive) _firebatParticle?.Play(true);

            // 행운(LUCK) 심볼 노출 여부 직접 체크
            bool hasLuck = false;
            if (result != null && result.Reels != null)
            {
                foreach (var symbol in result.Reels)
                {
                    if (symbol == SymbolId.LUCK)
                    {
                        hasLuck = true;
                        break;
                    }
                }
            }
            if (hasLuck) _luckParticle?.Play(true);

            if (j.MGLTriggered)
            {
                if (_makgeolliCoroutine != null) StopCoroutine(_makgeolliCoroutine);
                _makgeolliCoroutine = StartCoroutine(PlayMakgeolliRoutine());
            }

        }

        private void StopAndClearParticle(ParticleSystem ps)
        {
            if (ps == null) return;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private IEnumerator PlayMakgeolliRoutine()
        {
            if (_bottle == null) yield break;

            // 만약을 대비해 위치를 한번 더 잡아주고 활성화 후 Play
            _bottle.transform.localPosition = _bottleStartPos;
            _bottle.gameObject.SetActive(true);
            _bottle.PlayFeedbacks();

            // 0.5초 대기
            yield return new WaitForSeconds(0.5f);

            // MMF 정지, 비활성화, 위치 원복
            ResetMakgeolliBottle();
        }

        // 막걸리 초기화
        private void ResetMakgeolliBottle()
        {
            if (_makgeolliCoroutine != null)
            {
                StopCoroutine(_makgeolliCoroutine);
                _makgeolliCoroutine = null;
            }

            if (_bottle != null)
            {
                _bottle.StopFeedbacks(); // 피드백 즉시 중단
                _bottle.gameObject.SetActive(false); // 비활성화
                _bottle.transform.localPosition = _bottleStartPos; // 원래 위치로 복귀
            }
        }
    }
}