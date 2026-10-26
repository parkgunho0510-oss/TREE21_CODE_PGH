using System.Collections;
using UnityEngine;
using Wooduduk.Core;
using Wooduduk.Interactables;
using Wooduduk.Slot;
using Wooduduk.Slot.Machine;

namespace Wooduduk.Slot.Slotmachine
{
    public class SlotWoodSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject _woodPrefab;
        [SerializeField] private Transform _spawnPoint;
        [SerializeField] private float _ejectForce = 3f;
        [SerializeField] private float _spawnDelay = 0.05f; // 장작이 뿜어져 나오는 간격
        [SerializeField] private int _maxVisualSpawn = 30;  // 렉 방지용 물리 생성 최대치


        private void OnEnable()
        {
            SlotEvent.OnSettled += HandleSettled;
        }

        private void OnDisable()
        {
            SlotEvent.OnSettled -= HandleSettled;
        }

        // 정산 완료 시점
        private void HandleSettled(RunProgress run, long movedWood)
        {
            if (movedWood > 0)
            {
                StartCoroutine(EjectWoodRoutine((int)movedWood));
            }
        }

        private IEnumerator EjectWoodRoutine(int amount)
        {
            // 실제 수치가 너무 높아 물리 연산으로 게임이 멈추는 것을 방지
            int actualSpawnCount = Mathf.Min(amount, _maxVisualSpawn);

            for (int i = 0; i < actualSpawnCount; i++)
            {
                GameObject go = GenericObjectPool.Instance.Spawn(_woodPrefab, _spawnPoint.position, UnityEngine.Random.rotation);
                if (go.TryGetComponent<WoodLog>(out var woodLog))
                {
                    // 정면을 기준으로 약간의 부채꼴(원뿔) 형태로 퍼지면서 날아가도록 벡터 계산
                    Vector3 spreadDir = (_spawnPoint.forward + UnityEngine.Random.insideUnitSphere * 0.1f).normalized;
                    woodLog.Throw(spreadDir, _ejectForce);
                }

                yield return new WaitForSeconds(_spawnDelay);
            }
        }
    }
}