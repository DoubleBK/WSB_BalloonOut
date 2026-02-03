using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using BalloonOut.Data;
using BalloonOut.Game.Grid;
using BalloonOut.Game.Arrow;
using BalloonOut.Game.Gimmick;
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
        [SerializeField] private GameObject _topBarUI;
        [SerializeField] private GameObject _bottomBarUI;
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
        [SerializeField] private DotMatrixPulseEffect _dotMatrixEffect;
        [SerializeField] private PraiseTextEffect _praiseTextEffect;
        [SerializeField] private string _lobbySceneName = "LobbyScene";
        [SerializeField] private float _confettiDelayAfterPulse = 0.5f;
        [SerializeField] private float _returnToLobbyDelay = 1.5f;

        [Header("Camera")]
        [SerializeField] private CameraController _cameraController;

        // ========== 내부 상태 변수 ==========
        private GameState _state = GameState.Ready;
        private LevelData _currentLevel;
        private List<ArrowController> _arrows = new List<ArrowController>();
        private bool _isProcessing = false;
        private int _pendingHomingArrows = 0;  // 비행 중인 HomingArrow 개수
        private bool _isUILockedForBooster = false;  // 부스터 실행 중 UI 락

        // Undo용 이동 전 스냅샷 저장
        private Dictionary<int, ArrowSnapshot> _preMoveSnapshots = new Dictionary<int, ArrowSnapshot>();

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
        public bool IsUILockedForBooster => _isUILockedForBooster;

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
            _pendingHomingArrows = 0;

            // 그리드 초기화 (직사각형 지원)
            if (_gridSystem != null)
            {
                _gridSystem.Initialize(levelData.GetGridWidth(), levelData.GetGridHeight());
            }

            // 카메라 자동 줌 조절 (직사각형 지원)
            if (_cameraController != null && _gridSystem != null)
            {
                _cameraController.AdjustToGrid(levelData.GetGridWidth(), levelData.GetGridHeight(), _gridSystem.CellSize);
            }
            else if (CameraController.Instance != null && _gridSystem != null)
            {
                CameraController.Instance.AdjustToGrid(levelData.GetGridWidth(), levelData.GetGridHeight(), _gridSystem.CellSize);
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

            // BoosterManager 초기화 (Undo 히스토리 클리어)
            if (BoosterManager.Instance != null)
            {
                BoosterManager.Instance.OnLevelStart();
            }

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

            // 화살표 위치에 Dot 표시
            if (_gridSystem != null)
            {
                var cells = data.GetCells();
                foreach (var cell in cells)
                {
                    _gridSystem.ShowDotAt(cell);
                }
            }

            // HomingArrow 사용 시 탈출 시작 이벤트 구독
            if (_useHomingArrow && _homingArrowSpawner != null)
            {
                controller.OnExtractionStarted += OnArrowExtractionStartedHandler;
            }

            _arrows.Add(controller);
        }

        /// <summary>
        /// 화살표 복원 (Undo용) - 기존 ID와 데이터로 화살표를 다시 생성
        /// </summary>
        public ArrowController RestoreArrow(int id, ArrowData data)
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

            // 화살표 위치에 Dot 표시
            if (_gridSystem != null)
            {
                var cells = data.GetCells();
                foreach (var cell in cells)
                {
                    _gridSystem.ShowDotAt(cell);
                }
            }

            if (_useHomingArrow && _homingArrowSpawner != null)
            {
                controller.OnExtractionStarted += OnArrowExtractionStartedHandler;
            }

            _arrows.Add(controller);

            Debug.Log($"[GameManager] Arrow restored: ID={id}, Color={data.Color}, Direction={data.Direction}");
            return controller;
        }

        /// <summary>
        /// 화살표 탈출 시작 이벤트 핸들러 (HomingArrow 전환용)
        /// </summary>
        private void OnArrowExtractionStartedHandler(ArrowController arrow, Vector2 headPos, ArrowDirection exitDir)
        {
            // 탈출 시작 시 즉시 다음 입력 허용
            // (OnExtracted 이벤트는 화살표 파괴로 인해 호출되지 않을 수 있음)
            _isProcessing = false;
            Debug.Log($"[GameManager] Arrow extraction started, _isProcessing reset to false");

            if (_homingArrowSpawner != null)
            {
                // 저장해둔 이동 전 스냅샷 전달
                ArrowSnapshot preMoveSnapshot = null;
                if (_preMoveSnapshots.TryGetValue(arrow.Id, out var snapshot))
                {
                    preMoveSnapshot = snapshot;
                    _preMoveSnapshots.Remove(arrow.Id);
                }

                _homingArrowSpawner.HandleArrowExtractionStarted(arrow, headPos, exitDir, preMoveSnapshot);
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
            Debug.Log($"[GameManager] OnArrowTapped: Arrow={arrow?.Id}, State={_state}, IsProcessing={_isProcessing}, UILocked={_isUILockedForBooster}, CanLaunch={arrow?.CanLaunch}");

            if (_state != GameState.Playing) return;
            if (_isProcessing) return;
            if (_isUILockedForBooster) return;  // 부스터 실행 중 입력 차단
            if (!arrow.CanLaunch) return;

            _isProcessing = true;

            // 이동 전 스냅샷 캡처 (Undo용)
            _preMoveSnapshots[arrow.Id] = ArrowSnapshot.CreateFromController(arrow);

            // 힌트 하이라이트 해제
            if (BoosterManager.Instance != null)
            {
                BoosterManager.Instance.ClearHintHighlight();
            }

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
            Debug.Log($"[GameManager] Arrow extracted, _isProcessing reset to false");
        }

        /// <summary>
        /// 화살표 정지 이벤트 핸들러 (충돌/막힘)
        /// </summary>
        private void OnArrowStoppedHandler(ArrowController arrow)
        {
            // 이벤트 구독 해제
            arrow.OnExtracted -= OnArrowExtractedHandler;
            arrow.OnStopped -= OnArrowStoppedHandler;

            // 화살표가 막힌 경우 저장된 스냅샷 정리
            _preMoveSnapshots.Remove(arrow.Id);

            Debug.Log("[GameManager] Arrow BLOCKED! _isProcessing reset to false");
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
                _pendingHomingArrows++;
                Debug.Log($"Arrow extracted, waiting for HomingArrow. Color: {arrow.Color}, Pending: {_pendingHomingArrows}");
                // 승리/패배 조건은 OnHomingHitTargetHandler에서 풍선 팝 후 체크
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
            // 비행 중인 HomingArrow 카운터 감소
            _pendingHomingArrows--;
            Debug.Log($"[GameManager] HomingArrow hit target. Pending: {_pendingHomingArrows}");

            // 팝 전에 레인 인덱스 및 풍선 스냅샷 캡처 (Undo 복원용)
            int prePoppedLaneIndex = _queueUI?.FindLaneWithActiveBalloon(color) ?? -1;
            BalloonSnapshot balloonSnapshot = null;
            if (prePoppedLaneIndex >= 0)
            {
                balloonSnapshot = _queueUI?.CreateActiveBalloonSnapshot(prePoppedLaneIndex);
            }

            // 풍선 팝 시도 (기믹 지원)
            bool wasMatch = false;
            bool wasPartialHit = false;
            if (_queueUI != null)
            {
                var popResult = _queueUI.TryPopBalloonWithGimmick(color, out int poppedLaneIndex, out GimmickHitResult hitResult);
                wasMatch = (popResult == QueueUI.PopResult.Popped);
                wasPartialHit = (popResult == QueueUI.PopResult.Hit);

                if (wasPartialHit)
                {
                    Debug.Log($"[GameManager] Partial hit on balloon! {hitResult.FeedbackMessage}");
                    // Partial hit 시 스냅샷에 표시
                    if (balloonSnapshot != null)
                    {
                        balloonSnapshot.WasPartialHit = true;
                    }
                }
            }

            // Undo 히스토리 기록 - SourceArrowSnapshot null 체크 제거
            // 스냅샷이 null이어도 기록하여 히스토리 연속성 유지
            if (BoosterManager.Instance != null && homingArrow != null)
            {
                // SourceArrowSnapshot이 null이면 경고 로그 출력
                if (homingArrow.SourceArrowSnapshot == null)
                {
                    Debug.LogWarning($"[GameManager] SourceArrowSnapshot is null for color {color}");
                }

                BoosterManager.Instance.RecordArrowEscapeFromSnapshot(
                    homingArrow.SourceArrowSnapshot,
                    wasMatch || wasPartialHit,  // Partial hit도 "hit"으로 처리
                    prePoppedLaneIndex,
                    balloonSnapshot);  // 풍선 스냅샷 전달
            }

            OnArrowEscaped?.Invoke(color, wasMatch);
            CheckWinCondition();

            string resultMsg = wasMatch ? "POP!" : (wasPartialHit ? "PARTIAL HIT!" : "missed!");
            Debug.Log($"[GameManager] HomingArrow {resultMsg} Color: {color}, Lane: {prePoppedLaneIndex}");
            // _isProcessing은 OnArrowExtractedHandler에서 이미 false로 설정됨
            // 화살표 탈출 즉시 다음 입력 허용
        }

        /// <summary>
        /// 풍선과 매칭되지 않은 화살표 탈출 기록 (Undo용)
        /// HomingArrow가 생성되지 않은 경우 호출됨
        /// </summary>
        public void RecordMissedArrowEscape(ArrowSnapshot arrowSnapshot, GameColor color)
        {
            if (BoosterManager.Instance != null)
            {
                BoosterManager.Instance.RecordArrowEscapeFromSnapshot(arrowSnapshot, false, -1);
            }

            OnArrowEscaped?.Invoke(color, false);
            CheckWinCondition();

            Debug.Log($"[GameManager] Missed arrow escape recorded: Color={color}");
        }

        /// <summary>
        /// 외부에서 승리 조건 체크를 요청할 때 사용
        /// (Connected 그룹 POP 등 QueueUI 이벤트 핸들러에서 호출)
        /// </summary>
        public void RequestWinConditionCheck()
        {
            CheckWinCondition();
        }

        // ========== 부스터 UI 락 ==========

        /// <summary>
        /// 부스터 실행 중 UI 인터랙션 락
        /// (Triple Arrow 등 부스터 발사 중 다른 입력 차단)
        /// </summary>
        public void LockUIForBooster()
        {
            _isUILockedForBooster = true;
            Debug.Log("[GameManager] UI locked for booster");
        }

        /// <summary>
        /// 부스터 실행 완료 후 UI 인터랙션 해제
        /// </summary>
        public void UnlockUIForBooster()
        {
            _isUILockedForBooster = false;
            Debug.Log("[GameManager] UI unlocked after booster");
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

            // 연출을 위해 UI 숨기기
            HideUIForConfetti();

            // 카메라 입력 비활성화 (연출 중 드래그/줌 차단)
            if (_cameraController != null)
                _cameraController.SetInputEnabled(false);

            // DotMatrix Pulse 먼저 시작
            float pulseDuration = 0f;
            if (_dotMatrixEffect != null)
            {
                _dotMatrixEffect.Play();
                pulseDuration = _dotMatrixEffect.GetTotalDuration();
            }

            // Confetti + Praise는 약간의 딜레이 후
            Invoke(nameof(PlayConfettiAndPraise), _confettiDelayAfterPulse);

            // 총 시간 계산: max(pulseDuration, confettiDuration + delay) + returnDelay
            float confettiDuration = _confettiEffect != null ? _confettiEffect.Duration : 0f;
            float totalWait = Mathf.Max(pulseDuration, confettiDuration + _confettiDelayAfterPulse) + _returnToLobbyDelay;

            if (totalWait > 0f)
            {
                Invoke(nameof(GoToLobby), totalWait);
                Debug.Log($"[GameManager] Clear sequence started. Going to lobby in {totalWait:F1}s");
            }
            else
            {
                Debug.LogWarning("[GameManager] No clear effects assigned. Going to lobby immediately.");
                GoToLobby();
            }
        }

        /// <summary>
        /// Confetti + Praise 연출 재생 (DotMatrix 후 딜레이)
        /// </summary>
        private void PlayConfettiAndPraise()
        {
            if (_confettiEffect != null)
                _confettiEffect.Play();

            if (_praiseTextEffect != null)
                _praiseTextEffect.Show();
        }

        /// <summary>
        /// Confetti 연출을 위해 UI 숨기기
        /// </summary>
        private void HideUIForConfetti()
        {
            if (_topBarUI != null)
                _topBarUI.SetActive(false);

            if (_bottomBarUI != null)
                _bottomBarUI.SetActive(false);

            if (_queueUI != null)
                _queueUI.gameObject.SetActive(false);

            // 남은 화살표 숨김
            HideRemainingArrows();

            // 비행 중인 HomingArrow 숨김
            HideRemainingHomingArrows();

            Debug.Log("[GameManager] UI hidden for Confetti effect");
        }

        /// <summary>
        /// 남은 그리드 화살표 숨김
        /// </summary>
        private void HideRemainingArrows()
        {
            foreach (var arrow in _arrows)
            {
                if (arrow != null)
                {
                    arrow.HideImmediate();
                }
            }
            Debug.Log($"[GameManager] Hidden {_arrows.Count} remaining arrows");
        }

        /// <summary>
        /// 비행 중인 HomingArrow 숨김
        /// </summary>
        private void HideRemainingHomingArrows()
        {
            var homingArrows = FindObjectsOfType<HomingArrow>();
            foreach (var homing in homingArrows)
            {
                if (homing != null)
                {
                    homing.gameObject.SetActive(false);
                }
            }
            Debug.Log($"[GameManager] Hidden {homingArrows.Length} remaining homing arrows");
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

            // 풍선이 남아있는데 화살표가 없고 비행 중인 HomingArrow도 없으면 패배
            bool hasRemainingBalloons = _queueUI != null && !_queueUI.IsAllCleared();
            bool hasNoArrows = _arrows.Count == 0;
            bool hasNoPendingHomingArrows = _pendingHomingArrows <= 0;

            // 비행 중인 HomingArrow가 있으면 아직 패배 아님
            if (hasRemainingBalloons && hasNoArrows && hasNoPendingHomingArrows)
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