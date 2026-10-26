using System.Collections;
using UnityEngine;
using Wooduduk.Core;
using Wooduduk.Interactables;
using Wooduduk.Slot;

namespace Wooduduk.Player
{
    public class PlayerWoodThrower : MonoBehaviour
    {
        [SerializeField] private GameObject _woodPrefab;
        [SerializeField] private Transform _throwPoint; // 던질 위치
        [SerializeField] private float _throwForce = 3f;


        private void Start()
        {
            GenericObjectPool.Instance.Prewarm(_woodPrefab, 50);
        }

        private void OnEnable()
        {
            SlotEvent.OnFireFed += HandleFireFed;
        }

        private void OnDisable()
        {
            SlotEvent.OnFireFed -= HandleFireFed;
        }
        private void HandleFireFed(RunProgress run)
        {
            StartCoroutine(ThrowRoutine());
        }
        IEnumerator ThrowRoutine()
        {
            yield return null;

            GameObject go = GenericObjectPool.Instance.Spawn(_woodPrefab, _throwPoint.position, _throwPoint.rotation);
            if (go.TryGetComponent<WoodLog>(out var woodLog))
            {
                woodLog.Throw(_throwPoint.forward, _throwForce);
            }
        }
    }
}