using System.Collections;
using UnityEngine;
using Wooduduk.Slot;          
using Wooduduk.Slot.Machine; 

namespace Wooduduk.Audio
{
    public sealed class SlotReelAudioController : MonoBehaviour
    {
        [Header("릴 훅 (왼→오 순서로 등록)")]
        [SerializeField] private ReelAudioHook[] _reels;

        [Header("정지 타이밍")]
        [Tooltip("ON: OnReelsDecided 후 코드가 릴을 순차 정지(애니 이벤트 미연동 시 폴백) " +
                 "OFF: Animation Event/릴 컨트롤러가 hook.Stop() 호출로 담당")]
        [SerializeField] private bool _useAutoStagger = true;
        [SerializeField, Min(0f)] private float _firstStopDelay = 0.5f;  // 회전 시작~첫 릴 정지
        [SerializeField, Min(0f)] private float _perReelStagger = 0.18f; // 릴 간 간격

        private Coroutine _stagger;

        private void OnEnable()
        {
            SlotEvent.OnSpinStarted += HandleSpinStarted;
            SlotEvent.OnReelsDecided += HandleReelsDecided;
        }
        private void OnDisable()
        {
            SlotEvent.OnSpinStarted -= HandleSpinStarted;
            SlotEvent.OnReelsDecided -= HandleReelsDecided;
            if (_stagger != null) StopCoroutine(_stagger);
        }

        private void HandleSpinStarted()
        {
            if (_stagger != null) StopCoroutine(_stagger);
            for (int i = 0; i < _reels.Length; i++) if (_reels[i] != null) _reels[i].StartRoll();
        }

        private void HandleReelsDecided(SpinResult result)
        {
            if (!_useAutoStagger) return; // 정지는 애니 이벤트가 담당
            int count = Mathf.Min(_reels.Length, result.Reels != null ? result.Reels.Length : _reels.Length);
            _stagger = StartCoroutine(StaggerStop(count));
        }

        private IEnumerator StaggerStop(int count)
        {
            yield return new WaitForSeconds(_firstStopDelay);
            for (int i = 0; i < count; i++)
            {
                if (_reels[i] != null) _reels[i].Stop();
                if (i < count - 1) yield return new WaitForSeconds(_perReelStagger);
            }
            _stagger = null;
        }
    }
}