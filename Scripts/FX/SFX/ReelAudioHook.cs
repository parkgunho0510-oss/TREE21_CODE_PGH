// 각 릴 오브젝트에 부착 + _reelIndex만 왼→오로 지정.
// 회전 시작 Animation Event -> StartRoll(), 정지 스냅 프레임 Animation Event -> Stop().
using System.Collections;
using UnityEngine;
using Wooduduk.Data.DataSO;
using Wooduduk.SFX;

namespace Wooduduk.Audio
{
    // 릴별 독립 소스. 데이터/믹서 그룹은 SoundManager 단일 소스에서 가져옴(중복 튜닝 방지)
    public sealed class ReelAudioHook : MonoBehaviour
    {
        [SerializeField] private int _reelIndex;                          // 0부터. 정지음 피치 계단
        [SerializeField, Range(0f, 1f)] private float _spatialBlend = 1f; // 1 = 3D 다이제틱, 0 = 2D
        [SerializeField] private AudioSource _source;                     // 비우면 자동 생성

        private Coroutine _roll;
        private int _tickCursor;
        private int _recentStop = -1;

        private void Awake()
        {
            if (_source == null) _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = _spatialBlend;
        }
        private void OnDisable() => StopRollRoutine();

        private void EnsureRouting()
        {
            var sm = SoundManager.Instance;
            if (sm != null && _source.outputAudioMixerGroup == null) _source.outputAudioMixerGroup = sm.SfxGroup;
        }

        // 회전 시작: 짧은 클릭을 _tickInterval 간격으로 번갈아(순차) 재생.
        public void StartRoll()
        {
            if (_roll != null) return;
            var c = SoundManager.Instance?.Catalog?._reel;
            if (c == null || c._rollTickClips == null || c._rollTickClips.Length == 0) return;
            EnsureRouting();
            StopRollRoutine();
            _tickCursor = 0;
            _roll = StartCoroutine(RollTicks(c));
        }
        private IEnumerator RollTicks(SoundCatalogSO.ReelConfig c)
        {
            var wait = new WaitForSeconds(Mathf.Max(0.01f, c._tickInterval));
            while (true)
            {
                var clip = c._rollTickClips[_tickCursor % c._rollTickClips.Length];
                _tickCursor++;
                _source.pitch = 1f;
                if (clip != null) _source.PlayOneShot(clip, c._volume);
                yield return wait;
            }
        }

        // 정지 : 진행 중 클릭까지 즉시 컷 → End 사운드(릴 인덱스만큼 피치 상승).
        public void Stop()
        {
            StopRollRoutine();
            _source.Stop();                  // 울리던 틱 즉시 끊김
            var c = SoundManager.Instance?.Catalog?._reel;
            if (c == null || c._stopClips == null || c._stopClips.Length == 0) return;
            EnsureRouting();
            _source.pitch = c._stopBasePitch + _reelIndex * c._stopPitchStep;
            _source.PlayOneShot(PickStop(c._stopClips), c._volume);
        }

        private void StopRollRoutine() { if (_roll != null) { StopCoroutine(_roll); _roll = null; } }

        private AudioClip PickStop(AudioClip[] clips)
        {
            if (clips.Length == 1) return clips[0];
            int idx, guard = 0;
            do { idx = Random.Range(0, clips.Length); guard++; } while (idx == _recentStop && guard < 8);
            _recentStop = idx; return clips[idx];
        }
    }
}