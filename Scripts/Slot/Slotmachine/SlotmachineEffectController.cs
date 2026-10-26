using MoreMountains.Feedbacks;
using UnityEngine;
using Wooduduk.Data.Service;
using Wooduduk.Slot;
using static UnityEngine.Rendering.DebugManager;

namespace Wooduduk.Slot.Machine
{
    public class SlotmachineEffectController : MonoBehaviour
    {
        [Header("Swing Effects (스윙 & 타격)")]
        [Tooltip("도끼 스윙 궤적이나 이펙트")]
        [SerializeField] private ParticleSystem _swingVFX;

        [Header("Freezing Effects (얼어붙는 연기)")]
        [Tooltip("얼음/연기 이펙트")]
        [SerializeField] private ParticleSystem _freezingSmokeVFX;
        [SerializeField] private ParticleSystem _freezingSnowVFX;

        private void OnEnable()
        {
            // 결합도를 낮추기 위해 SlotEvent에 자동 연결
            SlotEvent.OnSpinStarted += PlaySwingAndHitEffects;
            SlotEvent.OnBodyTempCritical += FreezingEffect; // 체온 위험 시 연기 시작
            SlotEvent.OnDeath += FreezingSnowEffect;         // 사망 시 완전 결빙
            SlotEvent.OnFireFed += StopFreezingEffect;           // 불을 쬐면 얼음 녹음
            SlotEvent.OnRunReset += HandleRunReset;
        }

        private void OnDisable()
        {
            SlotEvent.OnSpinStarted -= PlaySwingAndHitEffects;
            SlotEvent.OnBodyTempCritical -= FreezingEffect;
            SlotEvent.OnDeath -= FreezingSnowEffect;
            SlotEvent.OnFireFed -= StopFreezingEffect;
            SlotEvent.OnRunReset -= HandleRunReset;
        }

        /// <summary>
        /// 스윙 시 타격 피드백과 스윙 이펙트 동시 재생
        /// </summary>
        public void PlaySwingAndHitEffects()
        {
            if (_swingVFX != null)
            {
                // Play를 호출하면 파티클이 처음부터 재생됩니다.
                _swingVFX.Stop();
                _swingVFX.Play();
            }
        }

        /// <summary>
        /// 체온이 위험할 때 연기가 피어오르는 이펙트 시작
        /// </summary>
        public void FreezingEffect(RunProgress run = null)
        {
            if (_freezingSmokeVFX != null && !_freezingSmokeVFX.isPlaying)
            {
                _freezingSmokeVFX.Play();
            }
        }

        /// <summary>
        /// 사망(체온 0) 시 연기가 최대치에 달하거나 완전히 얼어붙는 연출
        /// </summary>
        public void FreezingSnowEffect(RunProgress run = null)
        {
            if (_freezingSnowVFX != null && !_freezingSnowVFX.isPlaying)
            {
                _freezingSnowVFX.Play();
            }
        }
        
        /// <summary>
        /// 모닥불에 장작을 넣어 체온이 회복되었을 때 연기 이펙트 정지 및 초기화
        /// </summary>
        public void StopFreezingEffect(RunProgress run = null)
        {
            if (_freezingSmokeVFX != null && _freezingSmokeVFX.isPlaying)
            {
                // 더 이상 파티클을 방출하지 않되, 이미 나온 파티클은 자연스럽게 사라지게 함
                _freezingSmokeVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            if (_freezingSnowVFX != null && _freezingSnowVFX.isPlaying)
            {
                _freezingSnowVFX.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
        }

        private void HandleRunReset(RunProgress run = null)
        {
            if (_freezingSmokeVFX != null && _freezingSmokeVFX.isPlaying)
                _freezingSmokeVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_freezingSnowVFX != null && _freezingSnowVFX.isPlaying)
                _freezingSnowVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }
}