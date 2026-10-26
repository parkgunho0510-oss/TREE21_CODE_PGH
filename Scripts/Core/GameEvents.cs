using System;
using UnityEngine;

namespace Wooduduk.Core
{
    // 모듈 간 이벤트 채널 (글로벌)
    public static class GameEvents
    {
        // [PIVOT-DISABLED 2026-06-22 한충희] FruitAttribute 삭제(구 열매 시스템) → 컴파일용 주석. 슬롯 피벗으로 열매 속성 이벤트 폐기.
        // 열매 획득
        // FruitDataSO 추가 / 현재는 속성만 전달
        //public static event Action<FruitAttribute> OnFruitCollected;
        //public static void RaiseFruitCollected(FruitAttribute attribute)
        //{
        //    OnFruitCollected?.Invoke(attribute);
        //}
        // 플레이어 사망
        // PlayerHealth가 raise, 정산/UI 등이 listen
        public static event Action<GameObject> OnPlayerDied;
        public static void RaisePlayerDied(GameObject g)
        {
            OnPlayerDied?.Invoke(g);
        }
        // 점수 변화 (장작 수 -> 재화 정산 등)
        public static event Action<int> OnScoreChanged;
        public static void RaiseScoreChanged(int score)
        {
            OnScoreChanged?.Invoke(score);
        }

        public static event Action<GameObject> OnPlayerExtracted;
        public static void RaisePlayerExtracted(GameObject go)
        {
            OnPlayerExtracted?.Invoke(go);
        }
    }
}
