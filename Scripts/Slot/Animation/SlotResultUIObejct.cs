using MoreMountains.Feedbacks;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Wooduduk.Slot;
using Wooduduk.Slot.Machine;

namespace Wooduduk.Slot.Slotmachine
{
    public class SlotResultUIObejct : MonoBehaviour
    {
        [Header("3D Objects References")]
        [SerializeField] private GameObject _symbolPopupGroup;     
        [SerializeField] private MeshRenderer _symbolRenderer;     
        [SerializeField] private TextMeshPro _symbolText;          
        [SerializeField] private TextMeshPro _finalWoodText;

        [SerializeField] private MMF_Player _hoverFeedback;

        [Header("Symbol Materials (0~7 Enum 순서 맞춤)")]
        [SerializeField] private Material[] _symbolMaterials;      

        // 데이터 캐싱용
        private SpinOutcome _cachedOutcome;
        private Dictionary<SymbolId, int> _currentReelCounts = new Dictionary<SymbolId, int>();
        private Coroutine _finalResultCoroutine;

        private void OnEnable()
        {
            SlotEvent.OnSpinStarted += HandleSpinStarted;
            SlotEvent.OnSpinJudged += HandleSpinJudged;
            SlotEvent.OnReelStopped += HandleReelStopped;
            SlotEvent.OnSpinCompleted += HandleSpinCompleted;
            SlotEvent.OnRunReset += HandleReset;
        }

        private void OnDisable()
        {
            SlotEvent.OnSpinStarted -= HandleSpinStarted;
            SlotEvent.OnSpinJudged -= HandleSpinJudged;
            SlotEvent.OnReelStopped -= HandleReelStopped;
            SlotEvent.OnSpinCompleted -= HandleSpinCompleted;
            SlotEvent.OnRunReset -= HandleReset;

        }

        private void Start()
        {
            _symbolPopupGroup.SetActive(false);
            _finalWoodText.gameObject.SetActive(false);
        }

        private void HandleReset(RunProgress run)
        {
            ResetFeedback();
            StopFinalResultCoroutine();
            _symbolPopupGroup.SetActive(false);
            _finalWoodText.gameObject.SetActive(false);
        }
        private void HandleSpinStarted()
        {
            ResetFeedback();
            StopFinalResultCoroutine();
            _currentReelCounts.Clear();
            _symbolPopupGroup.SetActive(false);
            _finalWoodText.gameObject.SetActive(false);
        }

        private void HandleSpinJudged(SpinOutcome outcome)
        {
            _cachedOutcome = outcome;
        }

        private void HandleReelStopped(int reelIndex)
        {
            if (_cachedOutcome.Result == null) return;

            SymbolId stoppedSymbol = _cachedOutcome.Result.Reels[reelIndex];

            if (!_currentReelCounts.ContainsKey(stoppedSymbol))
            {
                _currentReelCounts[stoppedSymbol] = 0;
            }
            _currentReelCounts[stoppedSymbol]++;

            // 각 릴이 멈출 때마다 누적 카운트 표기
            ShowSymbolPopup(stoppedSymbol, _currentReelCounts[stoppedSymbol]);

        }

        private void HandleSpinCompleted()
        {
            long gainedWood = _cachedOutcome.Applied.WoodStack;

            if (gainedWood > 0)
            {
                _finalWoodText.text = $"미정산 장작 \n획득! + {gainedWood}";
                _finalWoodText.gameObject.SetActive(true);
            }

            // 스핀 종료 후 1초 뒤에 최종 결과 표기 로직 실행
            _finalResultCoroutine = StartCoroutine(ShowFinalResultRoutine());

        }

        IEnumerator ShowFinalResultRoutine()
        {
            // 1f 대기
            yield return new WaitForSeconds(1f);

            SymbolId finalSymbol = SymbolId.SWING; // 기본값
            int maxCount = 0;
            bool isWormPenalty = false;

            // 좀벌레 특수 실패 조건 최우선
            if (_currentReelCounts.TryGetValue(SymbolId.WORM, out int wormCount) && wormCount >= 2)
            {
                finalSymbol = SymbolId.WORM;
                isWormPenalty = true;
            }

            // 가장 많이 나온 심볼 찾기
            if (!isWormPenalty)
            {
                foreach (var kvp in _currentReelCounts)
                {
                    if (kvp.Value > maxCount)
                    {
                        maxCount = kvp.Value;
                        finalSymbol = kvp.Key;
                    }
                }
            }

            // 최종 선정된 심볼과 그 카운트를 팝업에 표시
            if (_currentReelCounts.TryGetValue(finalSymbol, out int finalCount))
            {
                ShowSymbolPopup(finalSymbol, finalCount);
            }


            if (_hoverFeedback != null && !_hoverFeedback.IsPlaying)
            {
                _hoverFeedback.PlayFeedbacks();
            }
        }

        private void StopFinalResultCoroutine()
        {
            if (_finalResultCoroutine != null)
            {
                StopCoroutine(_finalResultCoroutine);
                _finalResultCoroutine = null;
            }
        }

        private void ResetFeedback()
        {
            if (_hoverFeedback != null)
            {
                _hoverFeedback.StopFeedbacks();
                _hoverFeedback.RestoreInitialValues();
            }
        }
        private void ShowSymbolPopup(SymbolId symbol, int count)
        {
            _symbolPopupGroup.SetActive(true);

            int symbolIndex = (int)symbol;
            if (symbolIndex >= 0 && symbolIndex < _symbolMaterials.Length)
            {
                // 인스턴싱으로 인한 메모리 릭 방지를 위해 sharedMaterial 사용
                _symbolRenderer.sharedMaterial = _symbolMaterials[symbolIndex];
            }

            string symbolName = GetSymbolNameKOR(symbol);
            _symbolText.text = $"{symbolName} \nx {count}";


        }

        private string GetSymbolNameKOR(SymbolId id)
        {
            switch (id)
            {
                case SymbolId.SWING: return "스윙";
                case SymbolId.PERFECT: return "퍼펙트";
                case SymbolId.ACORN: return "도토리";
                case SymbolId.LUCK: return "네잎클로버";
                case SymbolId.FRIEBAT: return "불빠따";
                case SymbolId.KNOT: return "옹이";
                case SymbolId.WORM: return "좀벌레";
                case SymbolId.MAKGEOLLI: return "막걸리";
                default: return id.ToString();
            }
        }
    }
}