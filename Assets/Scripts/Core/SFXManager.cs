using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BalloonOut.Core
{
    /// <summary>
    /// 오디오 관리자 - 게임 내 SFX 및 BGM 재생 담당
    /// </summary>
    public class SFXManager : MonoBehaviour
    {
        // ========== 싱글톤 ==========
        public static SFXManager Instance { get; private set; }

        // ========== SFX 설정 ==========
        [Header("SFX Settings")]
        [SerializeField] private float _sfxVolume = 1f;
        [SerializeField] private int _poolSize = 5;

        // ========== BGM 설정 ==========
        [Header("BGM Settings")]
        [SerializeField] private float _bgmVolume = 0.5f;
        [SerializeField] private float _fadeDuration = 1f;
        [SerializeField] private bool _autoPlayBGM = true;

        // ========== 오디오 클립 ==========
        [Header("SFX Clips")]
        [SerializeField] private AudioClip _balloonPopClip;

        [Header("BGM Clips")]
        [SerializeField] private AudioClip _ingameBGMClip;

        // ========== 내부 상태 ==========
        private List<AudioSource> _audioSourcePool;
        private int _currentPoolIndex = 0;
        private AudioSource _bgmSource;
        private Coroutine _fadeCoroutine;

        // ========== 프로퍼티 ==========
        public float SFXVolume => _sfxVolume;
        public float BGMVolume => _bgmVolume;
        public bool IsBGMPlaying => _bgmSource != null && _bgmSource.isPlaying;

        // ========== 유니티 라이프사이클 ==========
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // 씬 전환 시에도 유지 (로비 ↔ 인게임)
            DontDestroyOnLoad(gameObject);

            InitializeSFXPool();
            InitializeBGM();
            LoadDefaultClips();
        }

        private void Start()
        {
            if (_autoPlayBGM)
            {
                PlayBGM();
            }
        }

        private void InitializeSFXPool()
        {
            _audioSourcePool = new List<AudioSource>();

            for (int i = 0; i < _poolSize; i++)
            {
                var audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                _audioSourcePool.Add(audioSource);
            }
        }

        private void InitializeBGM()
        {
            _bgmSource = gameObject.AddComponent<AudioSource>();
            _bgmSource.playOnAwake = false;
            _bgmSource.loop = true;
            _bgmSource.volume = _bgmVolume;
        }

        private void LoadDefaultClips()
        {
            // SFX 클립 로드
            if (_balloonPopClip == null)
            {
                _balloonPopClip = Resources.Load<AudioClip>("Sound/SFX/AudioClip/SND_Balloon_Pop");
                if (_balloonPopClip == null)
                {
                    Debug.LogWarning("[SFXManager] Failed to load SND_Balloon_Pop from Resources");
                }
            }

            // BGM 클립 로드
            if (_ingameBGMClip == null)
            {
                _ingameBGMClip = Resources.Load<AudioClip>("Sound/Music/AudioClip/BGM_IngameTemp");
                if (_ingameBGMClip == null)
                {
                    Debug.LogWarning("[SFXManager] Failed to load BGM_IngameTemp from Resources");
                }
            }
        }

        // ========== SFX 공개 인터페이스 ==========

        /// <summary>
        /// 풍선 팝 효과음 재생
        /// </summary>
        public void PlayBalloonPop()
        {
            PlaySFX(_balloonPopClip);
        }

        /// <summary>
        /// 지정된 SFX 클립 재생
        /// </summary>
        public void PlaySFX(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null) return;

            var audioSource = GetNextAudioSource();
            audioSource.clip = clip;
            audioSource.volume = _sfxVolume * volumeScale;
            audioSource.Play();
        }

        /// <summary>
        /// SFX 볼륨 설정
        /// </summary>
        public void SetSFXVolume(float volume)
        {
            _sfxVolume = Mathf.Clamp01(volume);
        }

        // ========== BGM 공개 인터페이스 ==========

        /// <summary>
        /// 인게임 BGM 재생
        /// </summary>
        public void PlayBGM()
        {
            PlayBGM(_ingameBGMClip);
        }

        /// <summary>
        /// 지정된 BGM 클립 재생
        /// </summary>
        public void PlayBGM(AudioClip clip, bool fadeIn = true)
        {
            if (clip == null) return;
            if (_bgmSource == null) return;

            // 이미 같은 BGM이 재생 중이면 무시
            if (_bgmSource.clip == clip && _bgmSource.isPlaying)
                return;

            _bgmSource.clip = clip;

            if (fadeIn)
            {
                StartFade(0f, _bgmVolume);
                _bgmSource.Play();
            }
            else
            {
                _bgmSource.volume = _bgmVolume;
                _bgmSource.Play();
            }

            Debug.Log($"[SFXManager] BGM started: {clip.name}");
        }

        /// <summary>
        /// BGM 정지
        /// </summary>
        public void StopBGM(bool fadeOut = true)
        {
            if (_bgmSource == null || !_bgmSource.isPlaying) return;

            if (fadeOut)
            {
                StartFade(_bgmSource.volume, 0f, () =>
                {
                    _bgmSource.Stop();
                });
            }
            else
            {
                _bgmSource.Stop();
            }

            Debug.Log("[SFXManager] BGM stopped");
        }

        /// <summary>
        /// BGM 일시정지
        /// </summary>
        public void PauseBGM()
        {
            if (_bgmSource != null && _bgmSource.isPlaying)
            {
                _bgmSource.Pause();
            }
        }

        /// <summary>
        /// BGM 재개
        /// </summary>
        public void ResumeBGM()
        {
            if (_bgmSource != null && !_bgmSource.isPlaying && _bgmSource.clip != null)
            {
                _bgmSource.UnPause();
            }
        }

        /// <summary>
        /// BGM 볼륨 설정
        /// </summary>
        public void SetBGMVolume(float volume)
        {
            _bgmVolume = Mathf.Clamp01(volume);
            if (_bgmSource != null)
            {
                _bgmSource.volume = _bgmVolume;
            }
        }

        // ========== 내부 유틸리티 ==========

        private AudioSource GetNextAudioSource()
        {
            var audioSource = _audioSourcePool[_currentPoolIndex];
            _currentPoolIndex = (_currentPoolIndex + 1) % _audioSourcePool.Count;
            return audioSource;
        }

        private void StartFade(float from, float to, System.Action onComplete = null)
        {
            if (_fadeCoroutine != null)
            {
                StopCoroutine(_fadeCoroutine);
            }
            _fadeCoroutine = StartCoroutine(FadeCoroutine(from, to, onComplete));
        }

        private IEnumerator FadeCoroutine(float from, float to, System.Action onComplete)
        {
            float elapsed = 0f;
            _bgmSource.volume = from;

            while (elapsed < _fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / _fadeDuration;
                _bgmSource.volume = Mathf.Lerp(from, to, t);
                yield return null;
            }

            _bgmSource.volume = to;
            _fadeCoroutine = null;
            onComplete?.Invoke();
        }

        // ========== 레거시 호환 ==========

        /// <summary>
        /// 마스터 볼륨 설정 (SFX 볼륨으로 매핑)
        /// </summary>
        public void SetMasterVolume(float volume)
        {
            SetSFXVolume(volume);
        }

        /// <summary>
        /// 클립 재생 (SFX로 매핑)
        /// </summary>
        public void PlayClip(AudioClip clip, float volumeScale = 1f)
        {
            PlaySFX(clip, volumeScale);
        }
    }
}