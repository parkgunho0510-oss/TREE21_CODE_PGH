using System;
using UnityEngine;

namespace Wooduduk.Slot
{
    // 장작 물리 충돌 채널. WoodLog가 Raise → 오디오/VFX 바인더가 구독
    public static class WoodEvent
    {
        // 충돌 위치, 정규화 강도(0~1, 속도 기반)
        public static event Action<Vector3, float> OnWoodImpact;
        public static void RaiseWoodImpact(Vector3 pos, float strength01) => OnWoodImpact?.Invoke(pos, strength01);
    }
}