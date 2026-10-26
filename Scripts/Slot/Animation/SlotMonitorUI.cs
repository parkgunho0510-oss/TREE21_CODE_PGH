using MoreMountains.Feedbacks;
using TMPro; // Unity 6.3 LTS 표준 Text UI
using UnityEngine;
using Wooduduk.Data.DataSO;
using Wooduduk.Slot;
using Wooduduk.Slot.Machine;

namespace Wooduduk.Slot.Slotmachine
{
    public class SlotMonitorUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshPro _monitorText;

        [Header("Data References")]
        [SerializeField] private SlotmachineController _controller;
        [SerializeField] private DataManagerSO _data;

        // 캐싱 데이터
        private int _currentCost = 0;
        private int _currentCombo = 0;
        private float _currentMultiplier = 0f;
        private long _unsettledWood = 0; 
        private long _lastSettled = 0;

        private void OnEnable()
        {
            SlotEvent.OnSpinJudged += HandleSpinJudged;
            SlotEvent.OnSpinCompleted += HandleSpinCompleted;
            SlotEvent.OnSettled += HandleSettled;
            SlotEvent.OnRunReset += HandleRunReset;
        }

        private void OnDisable()
        {
            SlotEvent.OnSpinJudged -= HandleSpinJudged;
            SlotEvent.OnSpinCompleted -= HandleSpinCompleted;
            SlotEvent.OnSettled -= HandleSettled;
            SlotEvent.OnRunReset -= HandleRunReset;
        }

        private void Start()
        {
            if (_controller != null && _controller.Run != null)
            {
                UpdateCost(_controller.Run);
                UpdateRunState(_controller.Run);
            }
            RefreshUI();
        }

        // 판정 즉시 배율은 캐싱하되, UI 갱신은 보류 (스포일러 방지)
        private void HandleSpinJudged(SpinOutcome outcome)
        {
            if (_data != null && _data._gameBalance != null)
                _currentMultiplier = _data._gameBalance._data._slotEconomy.ComboMultBp(outcome.Combo) / 10000f;
        }

        // 릴 회전이 완전히 끝난 후 RunProgress의 최신 상태를 불러와서 UI 갱신
        private void HandleSpinCompleted()
        {
            if (_controller != null && _controller.Run != null)
            {
                UpdateRunState(_controller.Run);
                UpdateCost(_controller.Run);

            }
            RefreshUI();
        }

        // 정산 시
        private void HandleSettled(RunProgress run, long movedWood)
        {
            _lastSettled = movedWood;
            _currentMultiplier = 0f; // 정산 시 배율 표기 초기화

            UpdateCost(run);
            UpdateRunState(run); // 정산 후 콤보(0)와 미정산 장작(0) 갱신
            RefreshUI();
        }

        // 런 리셋 시
        private void HandleRunReset(RunProgress run)
        {
            _currentMultiplier = 0f;
            _lastSettled = 0;

            UpdateCost(run);
            UpdateRunState(run);
            RefreshUI();
        }

        // 콤보와 미정산 장작 상태 갱신
        private void UpdateRunState(RunProgress run)
        {
            if (run == null) return;
            _currentCombo = run.Combo;
            _unsettledWood = run.UnsettledWood;
        }

        // 다음 스핀 비용 갱신
        private void UpdateCost(RunProgress run)
        {
            if (run == null || _data == null || _data._gameBalance == null) return;

            var ecoConfig = _data._gameBalance._data._slotEconomy;
            _currentCost = ecoConfig.SpinCost(run.SpinCostTier);
        }

        // 실제 텍스트 갱신
        private void RefreshUI()
        {
            if (_monitorText == null) return;

            _monitorText.text =
                $"▶ 회전 비용 : {_currentCost} 장작\n" +
                $"▶ 현재 콤보 : {_currentCombo}\n" +
                $"▶ 콤보 배율 : {_currentMultiplier:F1}x\n" +
                $"▶ 미정산 장작 : {_unsettledWood}";
        }
    }
}