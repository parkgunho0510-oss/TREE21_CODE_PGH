using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using Wooduduk.Data.DataSO;

namespace Wooduduk.SFX
{
    // 단일 오디오 권한자. BGM/환경음/SFX/UI를 한 곳에서 관리
    // 순수 로컬 연출
    public sealed class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        [Header("Data")]
        [SerializeField] private SoundCatalogSO _catalog;

        [Header("Mixer Group")]
        [SerializeField] private AudioMixerGroup _bgmGroup;
        [SerializeField] private AudioMixerGroup _sfxGroup;

        [Header("SFX Pool")]
        [SerializeField, Min(1)] private int _sfxVoices = 8;       // 동시 재생 보이스
        [SerializeField, Min(0)] private int _noRepeatWindow = 2;  // 직전 n개 클립 회피

        [Header("BGM")]
        [SerializeField] private float _bgmFadeSeconds = 1f;
        private Coroutine _bgmRotate;
        private int _bgmCursor;              // 순차 재생 커서
        private int _recentBgmTrack = -1;    // 셔플 직전 곡 회피

        public SoundCatalogSO Catalog => _catalog;
        public AudioMixerGroup SfxGroup => _sfxGroup;

        private AudioSource _bgmSource;
        private AudioSource[] _sfxPool;
        private int _sfxCursor;
        private Coroutine _bgmFade;
        private BgmId _currentBgm = BgmId.None;

        private readonly Dictionary<AmbientId, AudioSource> _ambientSources = new(); // 채널별 독립 루프
        private readonly Dictionary<AudioClip[], List<int>> _recent = new(); // 무반복 랜덤 상태
        private readonly Dictionary<SfxId, SoundCatalogSO.SfxEntry> _sfxCache = new();   // 핫패스 캐싱

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            BuildCache();
            BuildSources();
        }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void BuildCache()
        {
            _sfxCache.Clear();
            if (_catalog?._sfx == null) return;
            foreach (var e in _catalog._sfx) if (e != null && !_sfxCache.ContainsKey(e._id)) _sfxCache[e._id] = e;
        }
        private void BuildSources()
        {
            _bgmSource = CreateSource("BGM", _bgmGroup, loop: true, priority: 0);          // 음악은 0 고정(유니티 공식 권장)
            _bgmSource.bypassReverbZones = true;
            _sfxPool = new AudioSource[_sfxVoices];
            for (int i = 0; i < _sfxVoices; i++)
                _sfxPool[i] = CreateSource("SFX_" + i, _sfxGroup, loop: false, priority: 170);

        }
        private AudioSource CreateSource(string label, AudioMixerGroup group, bool loop, int priority = 170)
        {
            var go = new GameObject(label); go.transform.SetParent(transform);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false; src.loop = loop; src.spatialBlend = 0f;
            src.priority = priority;
            src.outputAudioMixerGroup = group;
            return src;
        }

        // SFX / UI
        public void PlaySfx(SfxId id) => PlaySfxCore(id, 1f, null);
        public void PlaySfx(SfxId id, float pitchScale) => PlaySfxCore(id, pitchScale, null);
        public void PlaySfxAt(SfxId id, Vector3 pos, float pitchScale = 1f) => PlaySfxCore(id, pitchScale, pos);
        private void PlaySfxCore(SfxId id, float pitchScale, Vector3? pos)
        {
            if (_catalog == null) return;
            if (!_sfxCache.TryGetValue(id, out var e))
            { BuildCache(); if (!_sfxCache.TryGetValue(id, out e)) return; }

            PlayOneLayer(e, e._clips, e._volume, e._pitchRange, pitchScale, 0f, pos);
            if (e._layers == null) return;
            for (int i = 0; i < e._layers.Length; i++)
            {
                var l = e._layers[i];
                if (l != null) PlayOneLayer(e, l._clips, l._volume, l._pitchRange, pitchScale, l._delay, pos);
            }
        }
        private void PlayOneLayer(SoundCatalogSO.SfxEntry e, AudioClip[] clips, float volume,
                          Vector2 pitchRange, float pitchScale, float delay, Vector3? pos)
        {
            if (clips == null || clips.Length == 0) return;
            var src = _sfxPool[_sfxCursor];
            _sfxCursor = (_sfxCursor + 1) % _sfxPool.Length;

            // 위치·공간감은 샷 단위로 설정. pos 없으면 2D(리스너 부착) + 리버브 존 제외
            if (pos.HasValue)
            {
                src.transform.position = pos.Value;
                src.spatialBlend = e._spatialBlend;        // 엔트리가 0이면 pos 줘도 2D(비다이제틱 예외 허용)
                src.minDistance = e._minDistance;
                src.maxDistance = e._maxDistance;
                src.bypassReverbZones = false;
            }
            else
            {
                src.spatialBlend = 0f;
                src.bypassReverbZones = true;              // UI가 동굴 울림 먹는 것 방지
            }

            float pitch = Random.Range(pitchRange.x, pitchRange.y) * pitchScale;
            var clip = PickNoRepeat(clips);
            if (delay <= 0f) { src.pitch = pitch; src.PlayOneShot(clip, volume); }
            else StartCoroutine(DelayedOneShot(src, clip, volume, pitch, delay));
        }
        private IEnumerator DelayedOneShot(AudioSource src, AudioClip clip, float volume, float pitch, float delay)
        {
            yield return new WaitForSecondsRealtime(delay);     // timeScale 무관(슬로모 연출 중 타이밍 유지)
            if (src != null) { src.pitch = pitch; src.PlayOneShot(clip, volume); }
        }
        private AudioClip PickNoRepeat(AudioClip[] clips)
        {
            if (clips.Length == 1) return clips[0];
            if (!_recent.TryGetValue(clips, out var recent)) { recent = new List<int>(); _recent[clips] = recent; }
            int window = Mathf.Clamp(_noRepeatWindow, 0, clips.Length - 1);
            int idx, guard = 0;
            do { idx = Random.Range(0, clips.Length); guard++; } while (recent.Contains(idx) && guard < 32);
            recent.Add(idx); while (recent.Count > window) recent.RemoveAt(0);
            return clips[idx];
        }

        // BGM
        public void PlayBgm(BgmId id, bool fade = true)
        {
            if (_catalog == null || id == _currentBgm) return;
            var e = _catalog.GetBgm(id);
            if (e == null || e._clips == null || e._clips.Length == 0) return;
            _currentBgm = id;
            _bgmCursor = 0;
            StopBgmRotation();
            if (_bgmFade != null) StopCoroutine(_bgmFade);
            if (fade && _bgmFadeSeconds > 0f && isActiveAndEnabled) _bgmFade = StartCoroutine(FadeToBgm(e));
            else StartBgmTrack(e, NextBgmClip(e), e._volume);
        }
        public void StopBgm()
        {
            _currentBgm = BgmId.None;
            StopBgmRotation();
            if (_bgmSource != null) _bgmSource.Stop();
        }
        private void StartBgmTrack(SoundCatalogSO.BgmEntry e, AudioClip clip, float volume)
        {
            _bgmSource.clip = clip;
            _bgmSource.volume = volume;
            _bgmSource.loop = e._clips.Length == 1;               // 단일 곡만 하드 루프
            _bgmSource.Play();
            if (e._clips.Length > 1) _bgmRotate = StartCoroutine(RotateBgm(e)); // 곡 끝 감시 → 다음 곡
        }

        private IEnumerator RotateBgm(SoundCatalogSO.BgmEntry e)
        {
            while (true)
            {
                while (_bgmSource.isPlaying) yield return null;   // 곡 종료 대기(앱 일시정지 중엔 게임째 멈추므로 안전)
                _bgmSource.clip = NextBgmClip(e);
                _bgmSource.Play();
            }
        }

        private AudioClip NextBgmClip(SoundCatalogSO.BgmEntry e)
        {
            if (e._clips.Length == 1) return e._clips[0];
            if (!e._shuffle) return e._clips[_bgmCursor++ % e._clips.Length];  // 순차
            int idx, guard = 0;                                                // 셔플: 직전 곡만 회피
            do { idx = Random.Range(0, e._clips.Length); guard++; } while (idx == _recentBgmTrack && guard < 8);
            _recentBgmTrack = idx;
            return e._clips[idx];
        }
        private void StopBgmRotation()
        {
            if (_bgmRotate != null)
            {
                StopCoroutine(_bgmRotate);
                _bgmRotate = null;
            }
        }
        private IEnumerator FadeToBgm(SoundCatalogSO.BgmEntry e)
        {
            float half = _bgmFadeSeconds * 0.5f, t = 0f, start = _bgmSource.volume;
            while (t < half) { t += Time.unscaledDeltaTime; _bgmSource.volume = Mathf.Lerp(start, 0f, t / half); yield return null; }
            StartBgmTrack(e, NextBgmClip(e), 0f);                 // 볼륨 0으로 시작 → 페이드 인
            t = 0f;
            while (t < half) { t += Time.unscaledDeltaTime; _bgmSource.volume = Mathf.Lerp(0f, e._volume, t / half); yield return null; }
            _bgmSource.volume = e._volume; _bgmFade = null;
        }

        // 환경음
        public void PlayAmbient(AmbientId id) => PlayAmbientInternal(id, null);

        // 위치기반(3D) 환경음
        public void PlayAmbientAt(AmbientId id, Vector3 worldPos) => PlayAmbientInternal(id, worldPos);

        private void PlayAmbientInternal(AmbientId id, Vector3? worldPos)
        {
            if (_catalog == null) return;
            var e = _catalog.GetAmbient(id);
            if (e == null || e._clip == null) return;

            if (!_ambientSources.TryGetValue(id, out var src) || src == null)
            {
                src = CreateSource("Ambient_" + id, _sfxGroup, loop: true, priority: 16);
                _ambientSources[id] = src;
            }
            src.spatialBlend = e._spatialBlend;
            src.bypassReverbZones = e._spatialBlend <= 0f; // 2D 환경음은 리버브 존 제외
            src.minDistance = e._minDistance;
            src.maxDistance = e._maxDistance;
            if (worldPos.HasValue) src.transform.position = worldPos.Value;

            if (src.isPlaying && src.clip == e._clip) return;  // 재시작 팝 방지(기존 유지)
            src.clip = e._clip; src.volume = e._volume; src.Play();
        }
        public void StopAmbient(AmbientId id)
        {
            if (_ambientSources.TryGetValue(id, out var src) && src != null) src.Stop();
        }
        // 씬 전환 정리
        public void StopAll()
        {
            StopBgm();
            if (_sfxPool != null) foreach (var s in _sfxPool) if (s != null) s.Stop();
        }
        public void StopAllAmbient()
        {
            foreach (var kv in _ambientSources) if (kv.Value != null) kv.Value.Stop();
        }
    }
}