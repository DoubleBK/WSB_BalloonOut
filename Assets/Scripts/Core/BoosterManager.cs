using System;
using UnityEngine;
using NGFE.Data;
using BalloonOut.Data;
using BalloonOut.Game.Arrow;
using BalloonOut.UI;

namespace BalloonOut.Core
{
    /// <summary>
    /// 부스터 시스템 통합 관리자
    /// Undo, Hint 등 부스터 기능 제어
    /// </summary>
    public class BoosterManager : MonoBehaviour
    {
        // ========== 싱글톤 ==========
        public static BoosterManager Instance { get; private set; }

        // ========== 이벤트 ==========
        public event Action<ITEM_TYPE, int> OnBoosterQuantityChanged;
        public event Action<bool> OnUndoAvailabilityChanged;
        public event Action<ArrowController> OnHintArrowSelected;
        public event Action OnHintCleared;
        public event Action OnTripleArrowStateChanged;  // Triple Arrow 실행 완료 시 발생

        // ========== 참조 ==========
        [Header("References")]
        [SerializeField] private QueueUI _queueUI;
        [SerializeField] private TripleArrowController _tripleArrowController;

        // ========== 내부 상태 ==========
        private UndoHistoryManager _undoHistory;
        private HintCalculator _hintCalculator;
        private ArrowController _currentHintArrow;
        private bool _isUndoInProgress;
        private bool _isTripleArrowInProgress;

        // ========== 프로퍼티 ==========
        public bool CanUndo => !_isUndoInProgress && _undoHistory != null && _undoHistory.HasHistory;
        public int UndoHistoryCount => _undoHistory?.HistoryCount ?? 0;
        public bool IsHintActive => _currentHintArrow != null;
        public bool IsTripleArrowInProgress => _isTripleArrowInProgress;

        // ========== 유니티 라이프사이클 ==========
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _undoHistory = new UndoHistoryManager();
            _hintCalculator = new HintCalculator();

            // 히스토리 변경 이벤트 구독
            _undoHistory.OnHistoryChanged += OnUndoHistoryChanged;
        }

        private void Start()
        {
            // BoosterInventory 이벤트 구독
            BoosterInventory.Instance.OnQuantityChanged += OnInventoryQuantityChanged;

            // QueueUI 참조 확인
            if (_queueUI == null)
            {
                _queueUI = FindObjectOfType<QueueUI>();
            }

            // TripleArrowController 참조 확인
            if (_tripleArrowController == null)
            {
                _tripleArrowController = FindObjectOfType<TripleArrowController>();
            }

            // TripleArrowController 초기화
            if (_tripleArrowController != null)
            {
                _tripleArrowController.Initialize(_queueUI);
            }
        }

        private void OnDestroy()
        {
            if (_undoHistory != null)
            {
                _undoHistory.OnHistoryChanged -= OnUndoHistoryChanged;
            }

            if (BoosterInventory.Instance != null)
            {
                BoosterInventory.Instance.OnQuantityChanged -= OnInventoryQuantityChanged;
            }
        }

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// 레벨 시작/재시작 시 호출
        /// </summary>
        public void OnLevelStart()
        {
            _undoHistory?.ClearHistory();
            ClearHintHighlight();
            _isUndoInProgress = false;

            Debug.Log("[BoosterManager] Level started, history cleared");
        }

        /// <summary>
        /// 화살표 탈출 기록 (GameManager에서 호출 - ArrowController 버전)
        /// </summary>
        public void RecordArrowEscape(ArrowController arrow, bool wasMatch, int laneIndex = -1)
        {
            if (arrow == null) return;

            // 힌트 화살표가 탈출했으면 하이라이트 해제
            ClearHintHighlight();

            BalloonSnapshot balloonSnapshot = null;
            if (wasMatch && laneIndex >= 0)
            {
                balloonSnapshot = new BalloonSnapshot(arrow.Color, laneIndex, 0);
            }

            _undoHistory?.RecordArrowEscape(arrow, balloonSnapshot);

            Debug.Log($"[BoosterManager] Recorded escape: Arrow={arrow.Id}, Color={arrow.Color}, WasMatch={wasMatch}");
        }

        /// <summary>
        /// 화살표 탈출 기록 (GameManager에서 호출 - ArrowSnapshot 버전)
        /// HomingArrow 사용 시 화살표가 이미 파괴된 경우를 위한 오버로드
        /// </summary>
        /// <param name="arrowSnapshot">화살표 스냅샷</param>
        /// <param name="wasMatch">풍선과 매칭되었는지</param>
        /// <param name="laneIndex">레인 인덱스</param>
        /// <param name="existingBalloonSnapshot">기존 풍선 스냅샷 (기믹 상태 포함, null이면 자동 생성)</param>
        public void RecordArrowEscapeFromSnapshot(ArrowSnapshot arrowSnapshot, bool wasMatch, int laneIndex = -1, BalloonSnapshot existingBalloonSnapshot = null)
        {
            // 힌트 화살표가 탈출했으면 하이라이트 해제
            ClearHintHighlight();

            BalloonSnapshot balloonSnapshot = existingBalloonSnapshot;
            if (balloonSnapshot == null && wasMatch && laneIndex >= 0 && arrowSnapshot != null)
            {
                // 기믹 정보 없는 기본 스냅샷 생성 (하위 호환성)
                balloonSnapshot = new BalloonSnapshot(arrowSnapshot.Color, laneIndex, 0);
            }

            // arrowSnapshot이 null이면 빈 스냅샷 생성 (히스토리 연속성 유지)
            if (arrowSnapshot == null)
            {
                Debug.LogWarning("[BoosterManager] Recording escape with null arrow snapshot");
                arrowSnapshot = new ArrowSnapshot();
            }

            _undoHistory?.RecordArrowEscapeFromSnapshot(arrowSnapshot, balloonSnapshot);

            Debug.Log($"[BoosterManager] Recorded escape from snapshot: Arrow={arrowSnapshot.ArrowId}, Color={arrowSnapshot.Color}, WasMatch={wasMatch}, HasGimmicks={balloonSnapshot?.GimmickSnapshots?.Count ?? 0}");
        }

        // ========== Undo 부스터 ==========

        /// <summary>
        /// Undo 부스터 사용
        /// </summary>
        public bool UseUndo()
        {
            if (!CanUndo)
            {
                Debug.Log("[BoosterManager] Cannot use Undo: no history or already in progress");
                return false;
            }

            if (!BoosterInventory.Instance.TryUse(ITEM_TYPE.UNDO))
            {
                Debug.Log("[BoosterManager] Cannot use Undo: no boosters remaining");
                return false;
            }

            _isUndoInProgress = true;
            var snapshot = _undoHistory.PopState();

            if (snapshot == null)
            {
                _isUndoInProgress = false;
                return false;
            }

            // 화살표 복원
            RestoreArrow(snapshot.EscapedArrow);

            // 풍선 복원 (있을 경우)
            if (snapshot.PoppedBalloon != null)
            {
                RestoreBalloon(snapshot.PoppedBalloon);
            }

            _isUndoInProgress = false;

            Debug.Log("[BoosterManager] Undo completed");
            return true;
        }

        private void RestoreArrow(ArrowSnapshot arrowSnapshot)
        {
            if (arrowSnapshot == null || GameManager.Instance == null) return;

            // 빈 스냅샷 (OccupiedCells가 null이거나 비어있음)은 복원 스킵
            if (arrowSnapshot.OccupiedCells == null || arrowSnapshot.OccupiedCells.Count == 0)
            {
                Debug.LogWarning("[BoosterManager] Skipping arrow restore - empty snapshot");
                return;
            }

            var arrowData = arrowSnapshot.ToArrowData();
            GameManager.Instance.RestoreArrow(arrowSnapshot.ArrowId, arrowData);
        }

        private void RestoreBalloon(BalloonSnapshot balloonSnapshot)
        {
            if (balloonSnapshot == null || _queueUI == null) return;

            // Partial hit인 경우: 풍선 자체는 복원하지 않고 기믹 상태만 복원
            if (balloonSnapshot.WasPartialHit)
            {
                _queueUI.RestorePartialHit(balloonSnapshot.LaneIndex, balloonSnapshot.GimmickSnapshots);
                Debug.Log($"[BoosterManager] Restoring partial hit gimmick state: Lane={balloonSnapshot.LaneIndex}, Gimmicks={balloonSnapshot.GimmickSnapshots?.Count ?? 0}");
            }
            else
            {
                // 완전 팝된 풍선: 풍선 전체 복원 (기믹 포함)
                _queueUI.RestoreBalloon(balloonSnapshot);
                Debug.Log($"[BoosterManager] Restoring popped balloon: Color={balloonSnapshot.Color}, Lane={balloonSnapshot.LaneIndex}, Gimmicks={balloonSnapshot.GimmickSnapshots?.Count ?? 0}");
            }
        }

        // ========== Hint 부스터 ==========

        /// <summary>
        /// Hint 부스터 사용
        /// </summary>
        public bool UseHint()
        {
            if (GameManager.Instance == null || GameManager.Instance.State != GameState.Playing)
            {
                Debug.Log("[BoosterManager] Cannot use Hint: game not playing");
                return false;
            }

            if (!BoosterInventory.Instance.TryUse(ITEM_TYPE.HINT))
            {
                Debug.Log("[BoosterManager] Cannot use Hint: no boosters remaining");
                return false;
            }

            // 기존 하이라이트 제거
            ClearHintHighlight();

            // 힌트 계산
            var hintArrow = _hintCalculator.CalculateHint();

            if (hintArrow != null)
            {
                _currentHintArrow = hintArrow;
                hintArrow.ShowHintHighlight();
                OnHintArrowSelected?.Invoke(hintArrow);

                Debug.Log($"[BoosterManager] Hint arrow: ID={hintArrow.Id}, Color={hintArrow.Color}");
                return true;
            }

            Debug.Log("[BoosterManager] No valid hint arrow found");
            return false;
        }

        /// <summary>
        /// 힌트 하이라이트 해제
        /// </summary>
        public void ClearHintHighlight()
        {
            if (_currentHintArrow != null)
            {
                _currentHintArrow.HideHintHighlight();
                _currentHintArrow = null;
                OnHintCleared?.Invoke();
            }
        }

        // ========== Triple Arrow 부스터 ==========

        /// <summary>
        /// Triple Arrow 사용 가능 여부
        /// </summary>
        public bool CanUseTripleArrow()
        {
            if (_isTripleArrowInProgress)
                return false;

            if (!BoosterInventory.Instance.CanUse(ITEM_TYPE.TRIPLEARROW))
                return false;

            // 유효 타겟이 1개 이상 있어야 함
            if (_queueUI == null)
                return false;

            var validTargets = _queueUI.GetValidTargetsForTripleArrow(1);
            return validTargets.Count > 0;
        }

        /// <summary>
        /// Triple Arrow 부스터 사용
        /// </summary>
        public bool UseTripleArrow()
        {
            if (!CanUseTripleArrow())
            {
                Debug.Log("[BoosterManager] Cannot use Triple Arrow");
                return false;
            }

            if (_tripleArrowController == null)
            {
                Debug.LogError("[BoosterManager] TripleArrowController not found!");
                return false;
            }

            // 수량 차감
            if (!BoosterInventory.Instance.TryUse(ITEM_TYPE.TRIPLEARROW))
            {
                Debug.Log("[BoosterManager] Cannot use Triple Arrow: no boosters remaining");
                return false;
            }

            _isTripleArrowInProgress = true;

            // UI 락
            GameManager.Instance?.LockUIForBooster();

            // 타겟 선택
            var targets = _queueUI.GetValidTargetsForTripleArrow(3);

            Debug.Log($"[BoosterManager] Using Triple Arrow with {targets.Count} targets");

            // 발사 완료 이벤트 구독
            _tripleArrowController.OnTripleArrowComplete += OnTripleArrowComplete;

            // 발사
            _tripleArrowController.Execute(targets);

            return true;
        }

        /// <summary>
        /// Triple Arrow 완료 핸들러
        /// </summary>
        private void OnTripleArrowComplete()
        {
            _tripleArrowController.OnTripleArrowComplete -= OnTripleArrowComplete;
            _isTripleArrowInProgress = false;

            // UI 락 해제
            GameManager.Instance?.UnlockUIForBooster();

            // Triple Arrow 상태 변경 알림 (버튼 상태 업데이트용)
            OnTripleArrowStateChanged?.Invoke();

            // 승리 조건 체크
            GameManager.Instance?.RequestWinConditionCheck();

            Debug.Log("[BoosterManager] Triple Arrow complete");
        }

        // ========== 부스터 수량 조회 ==========

        public int GetBoosterQuantity(ITEM_TYPE type)
        {
            return BoosterInventory.Instance.GetQuantity(type);
        }

        public bool CanUseBooster(ITEM_TYPE type)
        {
            return BoosterInventory.Instance.CanUse(type);
        }

        public bool IsBoosterUnlocked(ITEM_TYPE type)
        {
            int currentStage = GameManager.Instance?.CurrentLevelIdx ?? 1;
            return BoosterInventory.Instance.IsUnlocked(type, currentStage);
        }

        // ========== 이벤트 핸들러 ==========

        private void OnUndoHistoryChanged()
        {
            OnUndoAvailabilityChanged?.Invoke(CanUndo);
        }

        private void OnInventoryQuantityChanged(ITEM_TYPE type, int newQuantity)
        {
            OnBoosterQuantityChanged?.Invoke(type, newQuantity);
        }
    }
}
