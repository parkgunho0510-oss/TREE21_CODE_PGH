using UnityEngine;

namespace Wooduduk.Core
{
    public interface IPoolable
    {
        // 풀에서 꺼내져 활성화될 때 실행할 로직 (예: 체력 가득 채우기, 위치 설정 등)
        void OnSpawn();
        
        // 풀로 반환되어 비활성화될 때 실행할 로직 (예: 코루틴 정지, 물리 연산 초기화 등)
        void OnReturn();
        
        // 풀로 자신을 돌려보내기 위해 호출할 델리게이트를 등록하는 메서드
        void SetupPoolReturn(System.Action<GameObject> returnAction);
    }
}