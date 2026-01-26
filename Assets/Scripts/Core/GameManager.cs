using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using BalloonOut.Data;
using BalloonOut.Game.Grid;
using BalloonOut.Game.Arrow;
using BalloonOut.UI;
using BalloonOut.Effects;

namespace BalloonOut.Core
{
    /// <summary>
    /// 게임 매니저
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        // ========== 싱글톤 ==========
        public static GameManager Instance { get; private set; }

        // ========== 이벤트 ==========
        public System.Action<GameState> OnGameStateChanged;
        public System.Action<GameColor, bool> OnArrowEscaped;  // color, wasMatch
        public System.Action OnLevelCleared;
        public System.Action OnLevelFailed;

        // ========== 인스펙터 노출 변수 ==========
        [Header("References")]
        [SerializeField] private GridSystem _gridSystem;
        [SerializeField] private QueueUI _queueUI;
        [SerializeField] private Transform _arrowContainer;
        [SerializeField] private HomingArrowSpawner _homingArrowSpawner;

        [Header("Prefabs")]
        [SerializeField] private GameObject _arrowPrefab;

        [Header("Homing Arrow Settings")]
        [SerializeField] private bool _useHomingArrow = true;

        [Header("Level Settings")]
        [SerializeField] private int _startLevelIdx = 1;

        [Header("Clear Sequence")]
        [SerializeField] private ConfettiEffect _confettiEffect;
        [SerializeField] private string _lobbySceneName = "LobbyScene";

        // ========== 내부 상태 변수 ==========
        private GameState _state = GameState.Ready;
        private LevelData _currentLevel;
        private List<ArrowController> _arrows = new List<ArrowController>();
        private bool _isProcessing = false;

        // ========== 레벨 진행 ==========
        private int _currentLevelIdx = 1;
        private StageTableEntry _currentStageEntry;

        // ========== 에디터 테스트용 ==========
        private static LevelData _editorTestLevel;

        /// <summary>
        /// 에디터에서 테스트할 레벨 설정 (Play Mode 진입 전 호출)
        /// </summary>
        public static void SetEditorTestLevel(LevelData levelData)
        {
            _editorTestLevel = levelData;
        }

        // ========== 프로퍼티 ==========
        public GameState State => _state;
        public LevelData CurrentLevel => _currentLevel;
        public int CurrentLevelIdx => _currentLevelIdx;
        public StageTableEntry CurrentStageEntry => _currentStageEntry;
        public int TotalLevelCount => StageLoader.GetTotalLevelCount();

        // ========== 유니티 라이프사이클 ==========
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            // 에디터에서 설정한 테스트 레벨이 있으면 우선 사용
            if (_editorTestLevel != null)
            {
                InitializeLevel(_editorTestLevel);
                _editorTestLevel = null; // 사용 후 초기화
                return;
            }

            // 저장된 레벨 진행 상황 로드 (PlayerPrefs)
            int savedLevel = GameProgressManager.LoadCurrentLevel(_startLevelIdx);
            LoadLevelByIdx(savedLevel);
        }

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// LevelIdx로 레벨 로드 (StageTable 기반)
        /// </summary>
        public void LoadLevelByIdx(int levelIdx)
        {
            _currentStageEntry = StageLoader.GetEntryByLevelIdx(levelIdx);
            if (_currentStageEntry == null)
            {
                Debug.LogError($"[GameManager] LevelIdx {levelIdx} not found in StageTable");
                return;
            }

            var levelData = StageLoader.LoadByLevelIdx(levelIdx);
            if (levelData != null)
            {
                _currentLevelIdx = levelIdx;
                InitializeLevel(levelData);
                Debug.Log($"[GameManager] Loaded Level {levelIdx} (Stage: {_currentStageEntry.StageIdx}, Difficulty: {_currentStageEntry.Difficulty})");
            }
            else
            {
                Debug.LogError($"[GameManager] Failed to load level by idx: {levelIdx}");
            }
        }

        /// <summary>
        /// 다음 레벨로 진행
        /// </summary>
        public bool NextLevel()
        {
            int nextLevelIdx = _currentLevelIdx + 1;
            var nextEntry = StageLoader.GetEntryByLevelIdx(nextLevelIdx);

            if (nextEntry == null)
            {
                Debug.Log($"[GameManager] No more levels! Current: {_currentLevelIdx}");
                return false;
            }

            LoadLevelByIdx(nextLevelIdx);
            return true;
        }

        /// <summary>
        /// 특정 레벨로 이동
        /// </summary>
        public void GoToLevel(int levelIdx)
        {
            LoadLevelByIdx(levelIdx);
        }

        /// <summary>
        /// 레벨 초기화
        /// </summary>
        public void InitializeLevel(LevelData levelData)
        {
            _currentLevel = levelData;

            // 기존 화살표 정리
            ClearArrows();

            // 그리드 초기화
            if (_gridSystem != null)
            {
                _gridSystem.Initialize(levelData.gridSize);
            }

            // Queue UI 초기화
            if (_queueUI != null && levelData.lanes != null)
            {
                _queueUI.Initialize(levelData.lanes);
            }

            // HomingArrowSpawner 초기화
            if (_homingArrowSpawner != null && _queueUI != null)
            {
                _homingArrowSpawner.Initialize(_queueUI);
                _homingArrowSpawner.OnHomingHitTarget -= OnHomingHitTargetHandler;
                _homingArrowSpawner.OnHomingHitTarget += OnHomingHitTargetHandler;
            }

            // 화살표 스폰
            SpawnArrows(levelData.arrows);

            // 게임 상태 변경
            SetState(GameState.Playing);

            Debug.Log($"Level initialized: {levelData.name}");
        }

        /// <summary>
        /// 레벨 재시작
        /// </summary>
        public void RestartLevel()
        {
            // StageTable 기반 재로드 (데이터 무결성 보장)
            if (_currentLevelIdx > 0)
            {
                LoadLevelByIdx(_currentLevelIdx);
            }
            else if (_currentLevel != null)
            {
                InitializeLevel(_currentLevel);
            }
        }

        // ========== 내부 유틸리티 ==========

        /// <summary>
        /// 화살표 스폰
        /// </summary>
        private void SpawnArrows(List<ArrowData> arrowsData)
        {
            if (arrowsData == null) return;

            for (int i = 0; i < arrowsData.Count; i++)
            {
                var data = arrowsData[i];
                SpawnArrow(i, data);
            }
        }

        /// <summary>
        /// 단일 화살표 스폰
        /// </summary>
        private void SpawnArrow(int id, ArrowData data)
        {
            GameObject arrowObj;

            if (_arrowPrefab != null)
            {
                arrowObj = Instantiate(_arrowPrefab, _arrowContainer);
            }
            else
            {
                arrowObj = new GameObject($"Arrow_{id}");
                arrowObj.transform.SetParent(_arrowContainer);
                arrowObj.AddComponent<ArrowController>();
            }

            var controller = arrowObj.GetComponent<ArrowController>();
            if (controller == null)
            {
                controller = arrowObj.AddComponent<ArrowController>();
            }

            controller.Initialize(id, data);
            controller.OnTapped += OnArrowTapped;

            // HomingArrow 사용 시 탈출 시작 이벤트 구독
            if (_useHomingArrow && _homingArrowSpawner != null)
            {
                controller.OnExtractionStarted += OnArrowExtractionStartedHandler;
            }

            _arrows.Add(controller);
        }

        /// <summary>
        /// 화살표 탈출 시작 이벤트 핸들러 (HomingArrow 전환용)
        /// </summary>
        private void OnArrowExtractionStartedHandler(ArrowController arrow, Vector2 headPos, ArrowDirection exitDir)
        {
            if (_homingArrowSpawner != null)
            {
                _homingArrowSpawner.HandleArrowExtractionStarted(arrow, headPos, exitDir);
            }
        }

        /// <summary>
        /// 화살표 정리
        /// </summary>
        private void ClearArrows()
        {
            foreach (var arrow in _arrows)
            {
                if (arrow != null)
                {
                    arrow.Cleanup();
                    Destroy(arrow.gameObject);
                }
            }
            _arrows.Clear();

            if (_gridSystem != null)
            {
                _gridSystem.ClearAllOccupied();
            }
        }

        /// <summary>
        /// 화살표 탭 이벤트
        /// </summary>
        private void OnArrowTapped(ArrowController arrow)
        {
            if (_state != GameState.Playing) return;
            if (_isProcessing) return;
            if (!arrow.CanLaunch) return;

            _isProcessing = true;

            // 이벤트 구독
            arrow.OnExtracted += OnArrowExtractedHandler;
            arrow.OnStopped += OnArrowStoppedHandler;

            // 발사
            arrow.Launch();
        }

        /// <summary>
        /// 화살표 탈출 완료 이벤트 핸들러
        /// </summary>
        private void OnArrowExtractedHandler(ArrowController arrow)
        {
            // 이벤트 구독 해제
            arrow.OnExtracted -= OnArrowExtractedHandler;
            arrow.OnStopped -= OnArrowStoppedHandler;

            OnArrowEscapedHandler(arrow);

            // 화살표 탈출 즉시 다음 입력 허용
            // HomingArrow가 풍선에 도달하는 것은 비동기로 처리되므로
            // 연속 화살표 발사가 가능하도록 여기서 바로 false 설정
            _isProcessing = false;
        }

        /// <summary>
        /// 화살표 정지 이벤트 핸들러 (충돌/막힘)
        /// </summary>
        private void OnArrowStoppedHandler(ArrowController arrow)
        {
            // 이벤트 구독 해제
            arrow.OnExtracted -= OnArrowExtractedHandler;
            arrow.OnStopped -= OnArrowStoppedHandler;

            Debug.Log("BLOCKED!");
            _isProcessing = false;
        }

        /// <summary>
        /// 화살표 탈출 처리
        /// </summary>
        private void OnArrowEscapedHandler(ArrowController arrow)
        {
            // HomingArrow 사용 시 풍선 팝은 HomingArrow가 처리
            if (_useHomingArrow && _homingArrowSpawner != null)
            {
                // 화살표 목록에서 제거만 수행
                _arrows.Remove(arrow);
                Debug.Log($"Arrow extracted, waiting for HomingArrow to hit balloon. Color: {arrow.Color}");
                return;
            }

            // HomingArrow 미사용 시 기존 로직
            bool wasMatch = false;
            if (_queueUI != null)
            {
                wasMatch = _queueUI.TryPopBalloon(arrow.Color);
            }

            OnArrowEscaped?.Invoke(arrow.Color, wasMatch);
            _arrows.Remove(arrow);
            CheckWinCondition();

            Debug.Log(wasMatch ? "POP!" : "FLY AWAY");
        }

        /// <summary>
        /// HomingArrow 타겟 도달 이벤트 핸들러
        /// </summary>
        private void OnHomingHitTargetHandler(HomingArrow homingArrow, GameColor color)
        {
            // 풍선 팝 시도
            bool wasMatch = false;
            if (_queueUI != null)
            {
                wasMatch = _queueUI.TryPopBalloon(color);
            }

            OnArrowEscaped?.Invoke(color, wasMatch);
            CheckWinCondition();

            Debug.Log(wasMatch ? $"HomingArrow POP! Color: {color}" : $"HomingArrow missed! Color: {color}");
            // _isProcessing은 OnArrowExtractedHandler에서 이미 false로 설정됨
            // 화살표 탈출 즉시 다음 입력 허용
        }

        /// <summary>
        /// 승리 조건 확인
        /// </summary>
        private void CheckWinCondition()
        {
            if (_queueUI != null && _queueUI.IsAllCleared())
            {
                SetState(GameState.Clear);
                OnLevelCleared?.Invoke();
                Debug.Log("LEVEL CLEARED!");

                // 클리어 시퀀스 시작 (Confetti → 로비 복귀)
                PlayClearSequence();
                return;
            }

            // 승리가 아니면 패배 조건 확인
            CheckFailCondition();
        }

        /// <summary>
        /// 클리어 시퀀스 재생 (Confetti 연출 → 로비 복귀)
        /// </summary>
        private void PlayClearSequence()
        {
            // 레벨 클리어 저장 (다음 레벨로 진행)
            GameProgressManager.OnLevelCleared(_currentLevelIdx);

            if (_confettiEffect != null)
            {
                _confettiEffect.Play();

                // Confetti 연출 종료 후 로비로 이동
                float confettiDuration = _confettiEffect.Duration;
                Invoke(nameof(GoToLobby), confettiDuration);

                Debug.Log($"[GameManager] Clear sequence started. Going to lobby in {confettiDuration}s");
            }
            else
            {
                // ConfettiEffect가 없으면 바로 로비로 이동
                Debug.LogWarning("[GameManager] ConfettiEffect not assigned. Going to lobby immediately.");
                GoToLobby();
            }
        }

        /// <summary>
        /// 로비 씬으로 이동
        /// </summary>
        public void GoToLobby()
        {
            Debug.Log($"[GameManager] Loading lobby scene: {_lobbySceneName}");
            SceneManager.LoadScene(_lobbySceneName);
        }

        /// <summary>
        /// 패배 조건 확인
        /// </summary>
        private void CheckFailCondition()
        {
            // 이미 게임이 끝났으면 체크하지 않음
            if (_state != GameState.Playing) return;

            // 풍선이 남아있는데 화살표가 없으면 패배
            bool hasRemainingBalloons = _queueUI != null && !_queueUI.IsAllCleared();
            bool hasNoArrows = _arrows.Count == 0;

            if (hasRemainingBalloons && hasNoArrows)
            {
                SetState(GameState.Failed);
                OnLevelFailed?.Invoke();
                Debug.Log("LEVEL FAILED! No more arrows.");
            }
        }

        /// <summary>
        /// 게임 상태 변경
        /// </summary>
        private void SetState(GameState newState)
        {
            if (_state == newState) return;

            _state = newState;
            OnGameStateChanged?.Invoke(_state);

            Debug.Log($"Game State: {_state}");
        }
    }
}