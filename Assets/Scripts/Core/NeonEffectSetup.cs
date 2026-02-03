using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BalloonOut.Core
{
    /// <summary>
    /// NEON 효과 설정 컴포넌트
    /// 씬에 자동으로 Bloom이 적용된 Volume을 생성합니다.
    /// 에디터에서도 실시간 조정 가능합니다.
    /// </summary>
    [ExecuteAlways]  // 에디터에서도 동작
    public class NeonEffectSetup : MonoBehaviour
    {
        // ========== 싱글톤 ==========
        public static NeonEffectSetup Instance { get; private set; }

        [Header("Bloom Settings")]
        [SerializeField, Range(0f, 2f)] private float _bloomThreshold = 0.8f;
        [SerializeField, Range(0f, 5f)] private float _bloomIntensity = 1.5f;
        [SerializeField, Range(0f, 1f)] private float _bloomScatter = 0.7f;

        [Header("Options")]
        [SerializeField] private bool _setupOnAwake = true;
        [SerializeField] private bool _enableNeonEffect = true;

        private Volume _volume;
        private VolumeProfile _profile;
        private Bloom _bloom;

        /// <summary>
        /// 게임 시작 시 자동으로 NEON 효과 설정
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance != null) return;

            var go = new GameObject("[NeonEffectSetup]");
            var setup = go.AddComponent<NeonEffectSetup>();
            DontDestroyOnLoad(go);
        }

        private void Awake()
        {
            // 싱글톤 설정
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (_setupOnAwake)
            {
                SetupBloom();
            }
        }

        /// <summary>
        /// Bloom 효과를 설정합니다.
        /// </summary>
        public void SetupBloom()
        {
            // 기존 Volume이 있으면 사용
            _volume = GetComponent<Volume>();
            if (_volume == null)
            {
                _volume = gameObject.AddComponent<Volume>();
            }

            // Global Volume으로 설정
            _volume.isGlobal = true;
            _volume.priority = 100;

            // Profile 생성
            _profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _volume.profile = _profile;

            // Bloom 추가
            _bloom = _profile.Add<Bloom>(true);
            ApplyBloomSettings();

            Debug.Log("[NeonEffectSetup] Bloom effect initialized");
        }

        /// <summary>
        /// 현재 설정값을 Bloom에 적용
        /// </summary>
        private void ApplyBloomSettings()
        {
            if (_bloom == null) return;

            _bloom.threshold.value = _bloomThreshold;
            _bloom.intensity.value = _bloomIntensity;
            _bloom.scatter.value = _bloomScatter;
            _bloom.threshold.overrideState = true;
            _bloom.intensity.overrideState = true;
            _bloom.scatter.overrideState = true;

            if (_volume != null)
            {
                _volume.enabled = _enableNeonEffect;
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Inspector에서 값 변경 시 실시간 적용
        /// </summary>
        private void OnValidate()
        {
            // 에디터에서 값 변경 시 즉시 적용
            if (_bloom != null)
            {
                ApplyBloomSettings();
            }
            else if (_volume != null && _volume.profile != null)
            {
                // 기존 profile에서 Bloom 찾기
                if (_volume.profile.TryGet<Bloom>(out var bloom))
                {
                    _bloom = bloom;
                    ApplyBloomSettings();
                }
            }
        }

        /// <summary>
        /// 에디터에서 Reset 메뉴 선택 시
        /// </summary>
        private void Reset()
        {
            _bloomThreshold = 0.8f;
            _bloomIntensity = 1.5f;
            _bloomScatter = 0.7f;
            _enableNeonEffect = true;
        }
#endif

        /// <summary>
        /// Bloom 강도 조절
        /// </summary>
        public void SetBloomIntensity(float intensity)
        {
            _bloomIntensity = intensity;
            if (_bloom != null)
            {
                _bloom.intensity.value = intensity;
            }
        }

        /// <summary>
        /// Bloom 임계값 조절
        /// </summary>
        public void SetBloomThreshold(float threshold)
        {
            _bloomThreshold = threshold;
            if (_bloom != null)
            {
                _bloom.threshold.value = threshold;
            }
        }

        /// <summary>
        /// Bloom 산란 조절
        /// </summary>
        public void SetBloomScatter(float scatter)
        {
            _bloomScatter = scatter;
            if (_bloom != null)
            {
                _bloom.scatter.value = scatter;
            }
        }

        /// <summary>
        /// NEON 효과 활성화/비활성화
        /// </summary>
        public void SetEnabled(bool enabled)
        {
            _enableNeonEffect = enabled;
            if (_volume != null)
            {
                _volume.enabled = enabled;
            }
        }

        /// <summary>
        /// 현재 NEON 효과가 활성화되어 있는지
        /// </summary>
        public bool IsEnabled => _volume != null && _volume.enabled;

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (_profile != null)
            {
                Destroy(_profile);
            }
        }
    }
}