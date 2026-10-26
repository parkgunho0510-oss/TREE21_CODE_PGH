using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Wooduduk.Data.DataSO;
using Wooduduk.Slot;
using Wooduduk.Slot.Machine;

namespace Wooduduk.SFX
{
    // 단일 슬롯 오디오 디렉터
    public sealed class ReelAudioDirector : MonoBehaviour
    {
        private SoundManager S => SoundManager.Instance;

        // 릴 개수가 인게임 중에 변할 수 있으므로 List 사용
        private List<AudioSource> _reelSources = new List<AudioSource>();
        private List<Coroutine> _reelRoutines = new List<Coroutine>();
        private int _recentStopClipIndex = -1;

        private void OnEnable()
        {
            SlotEvent.OnReelsDecided += HandleReelsDecided; 
            SlotEvent.OnReelStopped += HandleReelStopped;
        }

        private void OnDisable()
        {
            SlotEvent.OnReelsDecided -= HandleReelsDecided;
            SlotEvent.OnReelStopped -= HandleReelStopped;

            StopAllReelCoroutines();
        }

        // 실제 필요한 릴 개수(targetCount)만큼만 AudioSource를 동적으로 생성 및 풀링
        private void EnsureReelCapacity(int targetCount)
        {
            while (_reelSources.Count < targetCount)
            {
                int i = _reelSources.Count;
                var go = new GameObject($"ReelAudioSource_{i}");
                go.transform.SetParent(transform);

                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.loop = false;
                src.spatialBlend = 1f;                      
                go.transform.position = transform.position;
                // 생성 즉시 믹서 그룹 할당
                if (S != null && S.SfxGroup != null)
                {
                    src.outputAudioMixerGroup = S.SfxGroup;
                }

                _reelSources.Add(src);
                _reelRoutines.Add(null);
            }
        }

        //// 슬롯 애니메이션 연동 사운드

        //private void HandleAxeSwing()
        //{
        //    if (S) S.PlaySfx(SfxId.AxeSwing);
        //}

        //private void HandleSpinStarted()
        //{
        //    if (S == null || S.Catalog == null) return;
        //    S.PlaySfx(SfxId.LeverPull);
        //}

        // 스윙 즉시 결과가 확정되며 브로드캐스트됨. 여기서 이번 턴의 정확한 릴 개수를 알 수 있음.
        private void HandleReelsDecided(SpinResult result)
        {
            if (S == null || S.Catalog == null || result == null) return;

            int actualReelCount = result.Reels.Length;
            EnsureReelCapacity(actualReelCount); // 부족한 오디오 소스가 있다면 동적 생성

            var reelConfig = S.Catalog._reel;
            if (reelConfig?._rollTickClips == null || reelConfig._rollTickClips.Length == 0) return;

            // 확정된 릴 개수만큼 회전음 코루틴 시작
            for (int i = 0; i < actualReelCount; i++)
            {
                if (_reelRoutines[i] != null) StopCoroutine(_reelRoutines[i]);
                _reelRoutines[i] = StartCoroutine(ReelTickingRoutine(i, reelConfig));
            }
        }

        private void HandleReelStopped(int reelIndex)
        {
            if (S == null || S.Catalog == null) return;

            // 방어 코드: 생성된 AudioSource 범위를 넘어서면 무시
            if (reelIndex < 0 || reelIndex >= _reelSources.Count) return;

            var reelConfig = S.Catalog._reel;

            // 돌고 있던 틱 사운드 코루틴 종료
            if (_reelRoutines[reelIndex] != null)
            {
                StopCoroutine(_reelRoutines[reelIndex]);
                _reelRoutines[reelIndex] = null;
            }
            _reelSources[reelIndex].Stop();

            // 정지 사운드 재생
            if (reelConfig._stopClips != null && reelConfig._stopClips.Length > 0)
            {
                var src = _reelSources[reelIndex];
                // 왼쪽에서 오른쪽으로 갈수록 피치가 상승하는 연출 (찰진 타격감)
                src.pitch = reelConfig._stopBasePitch + (reelIndex * reelConfig._stopPitchStep);
                src.PlayOneShot(PickRandomStopClip(reelConfig._stopClips), reelConfig._volume);
            }
        }

        private IEnumerator ReelTickingRoutine(int index, SoundCatalogSO.ReelConfig config)
        {
            var src = _reelSources[index];
            var wait = new WaitForSeconds(Mathf.Max(0.01f, config._tickInterval));

            // 각 릴마다 약간의 시작 오프셋을 주어 기계적인 동시 재생 방지
            yield return new WaitForSeconds(Random.Range(0f, 0.05f));

            while (true)
            {
                var clip = config._rollTickClips[Random.Range(0, config._rollTickClips.Length)];
                src.pitch = Random.Range(0.95f, 1.05f); // 틱마다 미세한 피치 변화
                src.PlayOneShot(clip, config._volume);
                yield return wait;
            }
        }

        private AudioClip PickRandomStopClip(AudioClip[] clips)
        {
            if (clips.Length == 1) return clips[0];
            int idx;
            int guard = 0;
            // 소리가 연달아 나지 않도록 방지
            do { idx = Random.Range(0, clips.Length); guard++; }
            while (idx == _recentStopClipIndex && guard < 10);

            _recentStopClipIndex = idx;
            return clips[idx];
        }

        private void StopAllReelCoroutines()
        {
            for (int i = 0; i < _reelRoutines.Count; i++)
            {
                if (_reelRoutines[i] != null)
                {
                    StopCoroutine(_reelRoutines[i]);
                    _reelRoutines[i] = null;
                }
            }
        }


        //// 판정 및 시스템 이벤트 연동
        //private void HandleSpinJudged(SpinOutcome outcome)
        //{
        //    var j = outcome.Judgment;
        //    if (S == null || j == null) return;

        //    if (!j.IsHit)
        //    {
        //        S.PlaySfx(j.FailReason == FailReason.WORM ? SfxId.WormFail : SfxId.WhiffFail);
        //        return;
        //    }

        //    S.PlaySfx((int)j.Tier >= (int)SwingTier.Full ? SfxId.HitFull : SfxId.HitWeak);
        //    if (j.IsPerfect) S.PlaySfx(SfxId.Perfect);
        //}

        //private void HandleEffectApplied(SlotEffect _) { if (S) S.PlaySfx(SfxId.Makgeolli); }
        //private void HandleSettled(RunProgress _, long movedWood) { if (S) S.PlaySfx(SfxId.Settle); }
        //private void HandleFireFed(RunProgress _) 
        //{
        //    if (S) S.PlaySfx(SfxId.FireFeed);
        //    S.StopAmbient(AmbientId.HeartbeatCritical);
        //}
        //private void HandleCritical(RunProgress _) { if (S) S.PlayAmbient(AmbientId.HeartbeatCritical); }

        //private void HandleDeath(RunProgress _)
        //{
        //    if (S == null) return;
        //    S.PlaySfx(SfxId.Death);
        //    S.StopAllAmbient();
        //}
    }
}