using System;
using System.Collections;
using UnityEngine;
using Wooduduk.Core;
using Wooduduk.Slot;

namespace Wooduduk.Interactables
{
    [RequireComponent(typeof(Rigidbody))]
    public class WoodLog : MonoBehaviour, IPoolable
    {
        [Header("충돌 사운드")]
        [SerializeField, Min(0f)] private float _minImpactSpeed = 1.5f;  // 이하 무시(구르는 미세 접촉 컷)
        [SerializeField, Min(0f)] private float _maxImpactSpeed = 8f;    // 이 속도 이상 = 강도 1
        [SerializeField, Min(0f)] private float _impactCooldown = 1f;  // 로그당 재발화 최소 간격(초)
        private float _lastImpactTime;
        private Action<GameObject> _returnToPool;
        private Rigidbody _rb;
        private WaitForSeconds _despawnDelay = new WaitForSeconds(5f);

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
        }

        public void SetupPoolReturn(Action<GameObject> returnAction)
        {
            _returnToPool = returnAction;
        }

        public void OnSpawn()
        {
            // 풀에서 나올 때 물리 상태 초기화
            _lastImpactTime = -999f;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            StartCoroutine(AutoDespawnRoutine());
        }

        public void OnReturn()
        {
            StopAllCoroutines();
        }

        // 외부에서 힘을 가해 던지는 메서드
        public void Throw(Vector3 direction, float force)
        {
            _rb.AddForce(direction * force, ForceMode.Impulse);
            // 약간의 회전을 주어 자연스럽게 날아가도록 처리
            _rb.AddTorque(UnityEngine.Random.insideUnitSphere * force, ForceMode.Impulse);
        }

        private IEnumerator AutoDespawnRoutine()
        {
            yield return _despawnDelay;
            _returnToPool?.Invoke(gameObject);
        }
        private void OnCollisionEnter(Collision c)
        {
            float speed = c.relativeVelocity.magnitude;
            if (speed < _minImpactSpeed) return;                          // 1차 컷: 프리미티브 필터
            if (Time.time - _lastImpactTime < _impactCooldown) return;    // 2차 컷: 로그당 쿨다운
            _lastImpactTime = Time.time;
            float s = Mathf.InverseLerp(_minImpactSpeed, _maxImpactSpeed, speed);
            WoodEvent.RaiseWoodImpact(c.GetContact(0).point, s);          // 실제 재생 여부는 바인더가 최종 결정
        }
    }
}