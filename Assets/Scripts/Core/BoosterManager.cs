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

        // ========== 참조 ==========
        [Header("References")]
        [SerializeField] private QueueUI _queueUI;

        // ========== 내부 상태 ==========
        private UndoHistoryManager _undoHistory;
        private HintCalculator _hintCalculator;
        private ArrowController _currentHintArrow;
        private bool _isUndoInProgress;

        // ========== 프로퍼티 ==========
        public bool CanUndo => !_isUndoInProgress && _undoHistory != null && _undoHistory.HasHistory;
        public int UndoHistoryCount => _undoHistory?.HistoryCount ?? 0;
        public bool IsHintActive => _currentHintArrow != null;

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
        public void RecordArrowEscapeFromSnapshot(ArrowSnapshot arrowSnapshot, bool wasMatch, int laneIndex = -1)
        {
            if (arrowSnapshot == null) return;

            // 힌트 화살표가 탈출했으면 하이라이트 해제
            ClearHintHighlight();

            BalloonSnapshot balloonSnapshot = null;
            if (wasMatch && laneIndex >= 0)
            {
                balloonSnapshot = new BalloonSnapshot(arrowSnapshot.Color, laneIndex, 0);
            }

            _undoHistory?.RecordArrowEscapeFromSnapshot(arrowSnapshot, balloonSnapshot);

            Debug.Log($"[BoosterManager] Recorded escape from snapshot: Arrow={arrowSnapshot.ArrowId}, Color={arrowSnapshot.Color}, WasMatch={wasMatch}");
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

            var arrowData = arrowSnapshot.ToArrowData();
            GameManager.Instance.RestoreArrow(arrowSnapshot.ArrowId, arrowData);
        }

        private void RestoreBalloon(BalloonSnapshot balloonSnapshot)
        {
            if (balloonSnapshot == null || _queueUI == null) return;

            _queueUI.RestoreBalloon(balloonSnapshot.Color, balloonSnapshot.LaneIndex);

            Debug.Log($"[BoosterManager] Restoring balloon: Color={balloonSnapshot.Color}, Lane={balloonSnapshot.LaneIndex}");
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
