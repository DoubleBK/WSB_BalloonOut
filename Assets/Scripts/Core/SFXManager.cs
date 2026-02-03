using System.Collections.Generic;
using UnityEngine;

namespace BalloonOut.Core
{
    /// <summary>
    /// 효과음 관리자 - 게임 내 SFX 재생 담당
    /// </summary>
    public class SFXManager : MonoBehaviour
    {
        // ========== 싱글톤 ==========
        public static SFXManager Instance { get; private set; }

        // ========== 설정 ==========
        [Header("Settings")]
        [SerializeField] private float _masterVolume = 1f;
        [SerializeField] private int _poolSize = 5;

        // ========== 오디오 클립 ==========
        [Header("Audio Clips")]
        [SerializeField] private AudioClip _balloonPopClip;

        // ========== 내부 상태 ==========
        private List<AudioSource> _audioSourcePool;
        private int _currentPoolIndex = 0;

        // ========== 유니티 라이프사이클 ==========
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            InitializePool();
            LoadDefaultClips();
        }

        private void InitializePool()
        {
            _audioSourcePool = new List<AudioSource>();

            for (int i = 0; i < _poolSize; i++)
            {
                var audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                _audioSourcePool.Add(audioSource);
            }
        }

        private void LoadDefaultClips()
        {
            // Inspector에서 할당되지 않았으면 Resources에서 로드
            if (_balloonPopClip == null)
            {
                _balloonPopClip = Resources.Load<AudioClip>("Sound/SFX/AudioClip/SND_Balloon_Pop");
                if (_balloonPopClip == null)
                {
                    Debug.LogWarning("[SFXManager] Failed to load SND_Balloon_Pop from Resources");
                }
            }
        }

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// 풍선 팝 효과음 재생
        /// </summary>
        public void PlayBalloonPop()
        {
            PlayClip(_balloonPopClip);
        }

        /// <summary>
        /// 지정된 오디오 클립 재생
        /// </summary>
        public void PlayClip(AudioClip clip, float volumeScale = 1f)
        {
            if (clip == null) return;

            var audioSource = GetNextAudioSource();
            audioSource.clip = clip;
            audioSource.volume = _masterVolume * volumeScale;
            audioSource.Play();
        }

        /// <summary>
        /// 마스터 볼륨 설정
        /// </summary>
        public void SetMasterVolume(float volume)
        {
            _masterVolume = Mathf.Clamp01(volume);
        }

        // ========== 내부 유틸리티 ==========

        private AudioSource GetNextAudioSource()
        {
            var audioSource = _audioSourcePool[_currentPoolIndex];
            _currentPoolIndex = (_currentPoolIndex + 1) % _audioSourcePool.Count;
            return audioSource;
        }
    }
}