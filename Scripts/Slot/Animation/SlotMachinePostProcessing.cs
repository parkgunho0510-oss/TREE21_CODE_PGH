using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Wooduduk.Slot.Machine
{    public class SlotMachinePostProcessing : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SlotmachineController _controller;

        [Header("Post Processing (Screen Frost)")]
        [SerializeField] private Volume _globalVolume; // 씬의 Global Volume
        [SerializeField, Range(0f, 1f)] private float _maxVignetteIntensity = 0.3f;

        [Header("UI Frost Effect")]
        [SerializeField] private Image _frostUI; // 화면을 덮는 얼음 캔버스 이미지
        [SerializeField, Range(0f, 1f)] private float _maxFrostAlpha = 1f;

        private Vignette _vignette;
        private bool _isCriticalPhase = false;
        private float _criticalStartTemp = -1f;

        private void Awake()
        {
            if (_globalVolume != null && !_globalVolume.profile.TryGet(out _vignette))
            {
                Debug.LogWarning("Global Volume에 Vignette 오버라이드가 추가되지 않았습니다.");
            }

            if (_frostUI != null)
            {
                _frostUI.raycastTarget = false;
            }
        }

        private void OnEnable()
        {
            SlotEvent.OnBodyTempCritical += HandleBodyTempCritical;
            SlotEvent.OnRunReset += HandleRunReset;
        }

        private void OnDisable()
        {
            SlotEvent.OnBodyTempCritical -= HandleBodyTempCritical;
            SlotEvent.OnRunReset -= HandleRunReset;
        }

        private void HandleBodyTempCritical(RunProgress run)
        {
            _isCriticalPhase = true;
            if (_controller != null)
            {
                _criticalStartTemp = _controller.BodyTempNormalized;
            }
        }

        private void HandleRunReset(RunProgress run)
        {
            _isCriticalPhase = false;
            _criticalStartTemp = -1f;
            
            // 연출 즉시 초기화
            if (_vignette != null) _vignette.intensity.value = 0f;
            if (_frostUI != null)
            {
                Color uiColor = _frostUI.color;
                uiColor.a = 0f;
                _frostUI.color = uiColor;
            }
        }

        private void Update()
        {
            if (_controller == null) return;

            // 위험 구간 진입 시점부터 사망(0)까지의 정규화된 비율 계산 (0.0 ~ 1.0)
            float criticalProgress = 0f;
            if (_isCriticalPhase && _criticalStartTemp > 0f)
            {
                criticalProgress = Mathf.InverseLerp(_criticalStartTemp, 0f, _controller.BodyTempNormalized);
            }

            // 포스트 프로세싱 비네트 제어
            if (_vignette != null)
            {
                // 전체 체온 저하(1f - Normalized)와 위험 구간 진행률 중 더 극적인 연출을 위해 위험 구간 비율을 반영
                float finalFreezeAmount = Mathf.Max(1f - _controller.BodyTempNormalized, criticalProgress);
                _vignette.intensity.value = finalFreezeAmount * _maxVignetteIntensity;
            }

            // UI 캔버스 이미지 알파 제어 (요청하신 위험 구간 기준 완벽 비례 반영)
            if (_frostUI != null)
            {
                Color uiColor = _frostUI.color;
                uiColor.a = criticalProgress * _maxFrostAlpha;
                _frostUI.color = uiColor;
            }
        }
    }
}