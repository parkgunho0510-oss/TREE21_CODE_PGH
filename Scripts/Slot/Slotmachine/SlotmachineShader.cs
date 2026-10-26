using UnityEngine;

namespace Wooduduk.Slot.Machine
{
    // [리팩토링] 오직 슬롯머신 3D 본체 머티리얼의 얼음 쉐이더 수치만 전담하여 제어하는 클래스
    [RequireComponent(typeof(SlotmachineController))]
    public class SlotmachineShader : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SlotmachineController _controller;
        [SerializeField] private Renderer[] _targetRenderer; // 얼어붙을 메쉬의 렌더러들

        [Header("Shader Settings")]
        [SerializeField] private string _freezePropertyName = "_FreezeAmount";

        private int _freezePropertyID;
        private MaterialPropertyBlock _propBlock;

        private void Awake()
        {
            if (_controller == null) _controller = GetComponent<SlotmachineController>();

            _freezePropertyID = Shader.PropertyToID(_freezePropertyName);
            _propBlock = new MaterialPropertyBlock();

            if (_targetRenderer == null || _targetRenderer.Length == 0)
            {
                Debug.LogWarning("Target Renderer가 할당되지 않았습니다.");
            }
        }

        private void Update()
        {
            if (_controller == null || _targetRenderer == null || _targetRenderer.Length == 0) return;
            if (_propBlock == null) _propBlock = new MaterialPropertyBlock(); // 방어: Awake 미실행/인스턴스화 타이밍 대비(GetPropertyBlock dest null 방지)

            // 체온 감소 비율 (1 = 따뜻함 -> 0 = 얼어죽음 상태이므로 반대로 뒤집음)
            float freezeAmount = 1f - _controller.BodyTempNormalized;

            // 본체 머티리얼 쉐이더로 프로퍼티 블록을 이용해 값 전달
            for (int i = 0; i < _targetRenderer.Length; i++)
            {
                Renderer target = _targetRenderer[i];
                if (target == null) continue;

                target.GetPropertyBlock(_propBlock);
                _propBlock.SetFloat(_freezePropertyID, freezeAmount);
                target.SetPropertyBlock(_propBlock);
            }
        }
    }
}