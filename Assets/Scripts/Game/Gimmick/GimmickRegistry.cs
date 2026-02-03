using System.Collections.Generic;
using UnityEngine;
using BalloonOut.Data;
using NGFE.Data;

namespace BalloonOut.Game.Gimmick
{
    /// <summary>
    /// 기믹 레지스트리 - 모든 기믹 정의를 관리하는 싱글톤
    /// </summary>
    public class GimmickRegistry : MonoBehaviour
    {
        // ========== 싱글톤 ==========
        private static GimmickRegistry _instance;
        public static GimmickRegistry Instance
        {
            get
            {
                if (_instance == null)
                {
                    // 씬에서 찾기
                    _instance = FindObjectOfType<GimmickRegistry>();

                    // 없으면 생성
                    if (_instance == null)
                    {
                        var go = new GameObject("GimmickRegistry");
                        _instance = go.AddComponent<GimmickRegistry>();
                        DontDestroyOnLoad(go);
                    }
                }
                return _instance;
            }
        }

        // ========== 내부 상태 ==========
        private Dictionary<string, GimmickDefinitionSO> _definitions;
        private Dictionary<string, IGimmickBehavior> _behaviors;
        private bool _isInitialized;

        // ========== Unity 생명주기 ==========
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            Initialize();
        }

        // ========== 초기화 ==========

        /// <summary>
        /// 레지스트리 초기화 - Resources에서 모든 기믹 정의 로드
        /// </summary>
        public void Initialize()
        {
            if (_isInitialized) return;

            _definitions = new Dictionary<string, GimmickDefinitionSO>();
            _behaviors = new Dictionary<string, IGimmickBehavior>();

            // Resources/ScriptableObjects/Gimmicks에서 모든 기믹 정의 로드
            var allDefs = Resources.LoadAll<GimmickDefinitionSO>("ScriptableObjects/Gimmicks");
            foreach (var def in allDefs)
            {
                if (!string.IsNullOrEmpty(def.gimmickId))
                {
                    _definitions[def.gimmickId] = def;
                    Debug.Log($"[GimmickRegistry] Loaded gimmick: {def.gimmickId}");
                }
            }

            // 기본 내장 기믹 Behavior 등록
            RegisterBuiltInBehaviors();

            _isInitialized = true;
            Debug.Log($"[GimmickRegistry] Initialized with {_definitions.Count} gimmick definitions");
        }

        /// <summary>
        /// 내장 기믹 Behavior 등록
        /// </summary>
        private void RegisterBuiltInBehaviors()
        {
            // Surprise 기믹
            RegisterBehavior(new Behaviors.SurpriseGimmickBehavior());

            // Number 기믹
            RegisterBehavior(new Behaviors.NumberGimmickBehavior());

            // Connected 기믹
            RegisterBehavior(new Behaviors.ConnectedGimmickBehavior());
        }

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// Behavior 등록
        /// </summary>
        public void RegisterBehavior(IGimmickBehavior behavior)
        {
            if (behavior != null && !string.IsNullOrEmpty(behavior.GimmickId))
            {
                _behaviors[behavior.GimmickId] = behavior;
                Debug.Log($"[GimmickRegistry] Registered behavior: {behavior.GimmickId}");
            }
        }

        /// <summary>
        /// 기믹 정의 가져오기
        /// </summary>
        public GimmickDefinitionSO GetDefinition(string gimmickId)
        {
            if (!_isInitialized) Initialize();

            if (_definitions != null && _definitions.TryGetValue(gimmickId, out var def))
            {
                return def;
            }
            return null;
        }

        /// <summary>
        /// 기믹 Behavior 가져오기
        /// </summary>
        public IGimmickBehavior GetBehavior(string gimmickId)
        {
            if (!_isInitialized) Initialize();

            if (_behaviors != null && _behaviors.TryGetValue(gimmickId, out var behavior))
            {
                return behavior;
            }
            return null;
        }

        /// <summary>
        /// 모든 기믹 정의 가져오기
        /// </summary>
        public IEnumerable<GimmickDefinitionSO> GetAllDefinitions()
        {
            if (!_isInitialized) Initialize();
            if (_definitions == null) return System.Linq.Enumerable.Empty<GimmickDefinitionSO>();
            return _definitions.Values;
        }

        /// <summary>
        /// 모든 기믹 ID 가져오기
        /// </summary>
        public IEnumerable<string> GetAllGimmickIds()
        {
            if (!_isInitialized) Initialize();
            if (_definitions == null) return System.Linq.Enumerable.Empty<string>();
            return _definitions.Keys;
        }

        /// <summary>
        /// 기믹이 등록되어 있는지 확인
        /// </summary>
        public bool HasGimmick(string gimmickId)
        {
            if (!_isInitialized) Initialize();
            return _definitions != null && _definitions.ContainsKey(gimmickId);
        }

        /// <summary>
        /// 기믹 인스턴스 데이터 생성 (기본 파라미터 포함)
        /// </summary>
        public GimmickInstanceData CreateInstanceData(string gimmickId)
        {
            var def = GetDefinition(gimmickId);
            if (def != null)
            {
                return def.CreateInstanceData();
            }

            // 정의가 없어도 기본 인스턴스 생성
            return new GimmickInstanceData(gimmickId);
        }

        // ========== Enum 매핑 ==========

        /// <summary>
        /// GIMMICK_UNLOCK_INFO_TYPE enum을 gimmickId 문자열로 변환
        /// </summary>
        public static string GetGimmickId(GIMMICK_UNLOCK_INFO_TYPE type)
        {
            switch (type)
            {
                case GIMMICK_UNLOCK_INFO_TYPE.SURPRISEBALLOON:
                    return "surprise";
                case GIMMICK_UNLOCK_INFO_TYPE.NUMBERBALLOON:
                    return "number";
                default:
                    return null;
            }
        }

        /// <summary>
        /// gimmickId 문자열을 GIMMICK_UNLOCK_INFO_TYPE enum으로 변환
        /// </summary>
        public static GIMMICK_UNLOCK_INFO_TYPE GetGimmickType(string gimmickId)
        {
            switch (gimmickId)
            {
                case "surprise":
                    return GIMMICK_UNLOCK_INFO_TYPE.SURPRISEBALLOON;
                case "number":
                    return GIMMICK_UNLOCK_INFO_TYPE.NUMBERBALLOON;
                default:
                    return GIMMICK_UNLOCK_INFO_TYPE.NONE;
            }
        }
    }
}
