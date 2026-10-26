using UnityEngine;

namespace Wooduduk.Slot.Machine
{
    public class SlotMachineFreezenObejct : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SlotmachineController _controller;
        [SerializeField] private Renderer[] _iceAlphaRenderers;

        private MaterialPropertyBlock _propBlock;
        private int _baseColorPropertyID;
        
        private bool _isCriticalPhase = false;
        private float _criticalStartTemp = -1f;

        private void Awake()
        {
            if (_controller == null) 
                _controller = GetComponentInParent<SlotmachineController>();

            _baseColorPropertyID = Shader.PropertyToID("_BaseColor");
            _propBlock = new MaterialPropertyBlock();
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
            ApplyIceAlpha(0f); // 즉시 완전히 투명하게 초기화
        }

        private void Update()
        {
            if (_controller == null || _iceAlphaRenderers == null || _iceAlphaRenderers.Length == 0) return;

            float targetAlpha = 0f;
            if (_isCriticalPhase && _criticalStartTemp > 0f)
            {
                // 위험 진입 시점 체온(0~1 사이)부터 사망(0)까지의 구간을 0.0 ~ 1.0 비율로 정규화
                targetAlpha = Mathf.InverseLerp(_criticalStartTemp, 0f, _controller.BodyTempNormalized);
            }

            ApplyIceAlpha(targetAlpha);
        }

        private void ApplyIceAlpha(float alphaValue)
        {
            if (_propBlock == null) _propBlock = new MaterialPropertyBlock(); // 방어: Awake 미실행/인스턴스화 타이밍 대비
            for (int i = 0; i < _iceAlphaRenderers.Length; i++)
            {
                Renderer target = _iceAlphaRenderers[i];
                if (target == null) continue;

                target.GetPropertyBlock(_propBlock);

                // 원본 머티리얼의 기존 컬러 속성을 안전하게 가져옴
                Color currentColor = target.sharedMaterial.HasProperty(_baseColorPropertyID) 
                    ? target.sharedMaterial.GetColor(_baseColorPropertyID) 
                    : Color.white;

                // 계산된 타겟 알파값만 교체
                currentColor.a = alphaValue;

                _propBlock.SetColor(_baseColorPropertyID, currentColor);
                target.SetPropertyBlock(_propBlock);
            }
        }
    }
}