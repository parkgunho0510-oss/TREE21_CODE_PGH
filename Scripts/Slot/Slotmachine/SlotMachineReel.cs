using UnityEngine;

namespace Wooduduk.Slot.Machine
{
    public class SlotMachineReel : MonoBehaviour
    {
        [Header("Reel Renderers (This Reel Only)")]
        [SerializeField] private MeshRenderer _resultRenderer;
        [SerializeField] private MeshRenderer _dummyRenderer1;
        [SerializeField] private MeshRenderer _dummyRenderer2;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock _propBlock;

        /// <summary>
        /// 애니메이션 컨트롤러가 릴을 정지시킬 때 호출하는 함수
        /// </summary>
        public void ChangeSymbol(SymbolId resultSymbol, SlotSymbolManager manager)
        {
            if (manager == null || manager.MaterialCount == 0) return;

            int resultIndex = (int)resultSymbol;
            int count = manager.MaterialCount;

            // 순환 구조로 더미 인덱스 계산
            int dummy1Index = (resultIndex + 1) % count;
            int dummy2Index = (resultIndex + 2) % count;

            // 머티리얼 적용 (.sharedMaterial로 메모리 릭 방지)
            if (_resultRenderer != null)
                _resultRenderer.sharedMaterial = manager.GetMaterial(resultIndex);
                
            if (_dummyRenderer1 != null)
                _dummyRenderer1.sharedMaterial = manager.GetMaterial(dummy1Index);
                
            if (_dummyRenderer2 != null)
                _dummyRenderer2.sharedMaterial = manager.GetMaterial(dummy2Index);
        }

        /// <summary>
        /// 심볼의 Emission을 제어합니다. 연출용 더미는 항상 발광을 차단합니다.
        /// </summary>
        public void SetSymbolGlow(bool isHighlighted, Color glowColor, float intensity = 2.0f)
        {
            if (_propBlock == null) _propBlock = new MaterialPropertyBlock();

            // 연출용 더미는 무조건 발광 Off (검은색)
            if (_dummyRenderer1 != null)
            {
                _dummyRenderer1.GetPropertyBlock(_propBlock);
                _propBlock.SetColor(EmissionColorId, Color.black);
                _dummyRenderer1.SetPropertyBlock(_propBlock);
            }
            if (_dummyRenderer2 != null)
            {
                _dummyRenderer2.GetPropertyBlock(_propBlock);
                _propBlock.SetColor(EmissionColorId, Color.black);
                _dummyRenderer2.SetPropertyBlock(_propBlock);
            }

            // 결과 렌더러에만 판정 발광 적용
            if (_resultRenderer != null)
            {
                _resultRenderer.GetPropertyBlock(_propBlock);
                _propBlock.SetColor(EmissionColorId, isHighlighted ? glowColor * intensity : Color.black);
                _resultRenderer.SetPropertyBlock(_propBlock);
            }
        }
    }
}