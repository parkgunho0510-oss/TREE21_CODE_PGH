using UnityEngine;

namespace Wooduduk.Slot.Machine
{
    public class SlotSymbolManager : MonoBehaviour
    {
        [Header("Symbol Materials")]
        [Tooltip("SymbolId enum 순서(0~7)에 맞춰 8개의 머티리얼을 할당하세요.")]
        [SerializeField] private Material[] _symbolMaterials = new Material[8];

        public int MaterialCount => _symbolMaterials.Length;

        // 인덱스에 맞는 머티리얼 반환
        public Material GetMaterial(int index)
        {
            if (index < 0 || index >= _symbolMaterials.Length) return null;
            return _symbolMaterials[index];
        }
    }
}