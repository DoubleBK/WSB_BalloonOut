using System;
using System.Collections.Generic;
using UnityEngine;
using BalloonOut.Core;
using BalloonOut.Data;
using BalloonOut.Game.Grid;
using DG.Tweening;

namespace BalloonOut.Game.Arrow
{
    /// <summary>
    /// 화살표 컨트롤러 - 폴리라인(꺾이는) 화살표 지원, Snake 방식 이동
    /// 렌더링은 ArrowVisualRenderer, 연출은 ArrowAnimationHelper에 위임
    /// </summary>
    public class ArrowController : MonoBehaviour
    {
        // ========== 인스펙터 노출 변수 ==========
        [Header("필수 참조")]
        [SerializeField] private ArrowVisualRenderer _visualRenderer;
        [SerializeField] private ArrowAnimationHelper _animationHelper;
        [SerializeField] private ArrowInput _arrowInput;
        [SerializeField] private ArrowMovement _arrowMovement;
        [SerializeField] private BoxCollider2D _collider;

        [Header("애니메이션 설정")]
        [SerializeField] private float _launchPunchScale = 0.15f;
        [SerializeField] private float _launchPunchDuration = 0.1f;

        // ========== 내부 상태 변수 ==========
        private int _id;
        private GameColor _color;
        private ArrowState _state;
        private List<Vector2Int> _occupiedCells;
        private List<Vector2> _cellWorldPositions;
        private Vector2Int _headPosition;
        private ArrowDirection _headDirection;
        private Vector2Int _moveDirection;

        // 셀별 개별 콜라이더
        private List<BoxCollider2D> _cellColliders = new List<BoxCollider2D>();

        // 활성 화살표 수 추적 (디버그용)
        private static int _activeArrowCount = 0;

        // ========== 이벤트 ==========
        public event Action<ArrowController> OnTapped;
        public event Action<ArrowController> OnExtracted;
        public event Action<ArrowController> OnStateChanged;
        public event Action<ArrowController> OnStopped;
        public event Action<ArrowController> OnCollided;
        public event Action<ArrowController> OnWallHit;
        public event Action<ArrowController, Vector2, ArrowDirection> OnExtractionStarted;
        public event Action<ArrowController> OnMoveStarted;

        // ========== 프로퍼티 ==========
        public int Id => _id;
        public Vector2Int HeadPosition => _headPosition;
        public ArrowDirection HeadDirection => _headDirection;
        public GameColor Color => _color;
        public int TotalLength => _occupiedCells?.Count ?? 0;
        public ArrowState State => _state;
        public bool CanLaunch => _state == ArrowState.Idle && !(_animationHelper?.IsAppearing ?? false);
        public bool IsExtracting => _arrowMovement?.IsExtracting ?? false;
        public bool IsAppearing => _animationHelper?.IsAppearing ?? false;

        // ========== 유니티 라이프사이클 ==========
        private void OnEnable()
        {
            _activeArrowCount++;
            Debug.Log($"[ArrowController] Arrow {_id} OnEnable, active count: {_activeArrowCount}");
        }

        private void OnDisable()
        {
            _activeArrowCount--;
            Debug.Log($"[ArrowController] Arrow {_id} OnDisable, active count: {_activeArrowCount}");
        }

        private void OnDestroy()
        {
            ClearCellColliders();
        }

        // ========== 공개 인터페이스 ==========
        /// <summary>
        /// 화살표 초기화 (ArrowData 기반)
        /// </summary>
        public void Initialize(int id, ArrowData data)
        {
            _id = id;
            _color = data.Color;
            _state = ArrowState.Idle;

            // 셀 목록 계산
            _occupiedCells = data.GetCells();

            // 머리 위치 및 방향 설정
            if (_occupiedCells.Count > 0)
            {
                _headPosition = _occupiedCells[_occupiedCells.Count - 1];
            }
            _headDirection = data.Direction;
            _moveDirection = DirectionHelper.Vectors[_headDirection];

            // 월드 좌표 캐시
            CacheWorldPositions();

            // 시각 렌더러 초기화
            if (_visualRenderer != null)
            {
                _visualRenderer.Initialize(_color, _headDirection);
                _visualRenderer.UpdateLineRenderer(_cellWorldPositions, _moveDirection);
            }

            // 애니메이션 헬퍼 초기화
            if (_animationHelper != null)
            {
                _animationHelper.Initialize(_visualRenderer);
            }

            // 입력 처리기 초기화
            if (_arrowInput != null)
            {
                _arrowInput.Initialize(this, _id);
                _arrowInput.OnTapDetected += HandleTapDetected;
            }

            // 이동 처리기 초기화
            if (_arrowMovement != null)
            {
                _arrowMovement.Initialize(_occupiedCells, _headPosition, _moveDirection);
                _arrowMovement.OnPositionsChanged += HandlePositionsChanged;
                _arrowMovement.OnStepComplete += HandleStepComplete;
                _arrowMovement.OnExtractionStarted += HandleExtractionStarted;
                _arrowMovement.OnExtracted += HandleExtracted;
                _arrowMovement.OnBlocked += HandleBlocked;
                _arrowMovement.OnReturnComplete += HandleReturnComplete;
            }

            RegisterOccupiedCells(true);
            UpdateCollider();
        }

        /// <summary>
        /// 화살표 발사 (탭 시 호출)
        /// </summary>
        public void Launch()
        {
            Debug.Log($"[ArrowController] Launch() called for Arrow {_id}, State: {_state}");

            if (_state != ArrowState.Idle)
                return;

            Debug.Log($"[ArrowController] Arrow {_id} starting movement, Direction: {_headDirection}");

            // 이동 시작 이벤트 발생
            OnMoveStarted?.Invoke(this);

            // 펀치 애니메이션과 이동을 동시에 시작
            transform.DOPunchScale(Vector3.one * _launchPunchScale, _launchPunchDuration, 1, 0f);

            if (_arrowMovement != null)
            {
                SetState(ArrowState.Moving);
                _arrowMovement.StartMove();
            }
        }

        /// <summary>
        /// 충돌 무시하고 즉시 탈출 (Arrow Dash용)
        /// </summary>
        public void LaunchWithoutCollision()
        {
            if (_state != ArrowState.Idle)
                return;

            OnMoveStarted?.Invoke(this);

            transform.DOPunchScale(Vector3.one * _launchPunchScale, _launchPunchDuration, 1, 0f);

            if (_arrowMovement != null)
            {
                SetState(ArrowState.Moving);
                _arrowMovement.StartMoveIgnoreCollision();
            }
        }

        /// <summary>
        /// 화살표가 차지하는 모든 그리드 위치 반환
        /// </summary>
        public Vector2Int[] GetOccupiedPositions()
        {
            return _occupiedCells?.ToArray() ?? new Vector2Int[0];
        }

        /// <summary>
        /// Head의 현재 월드 좌표 반환 (셀 중심)
        /// </summary>
        public Vector2 GetHeadWorldPosition()
        {
            return GridSystem.Instance.GridToWorld(_headPosition);
        }

        /// <summary>
        /// Head의 뾰족한 끝(Tip) 월드 좌표 반환
        /// </summary>
        public Vector2 GetHeadTipWorldPosition()
        {
            Vector2 headCenter = GetHeadWorldPosition();
            float halfCell = GridSystem.Instance.CellSize * 0.5f;
            Vector2 direction = GetDirectionVector(_headDirection);
            return headCenter + direction * halfCell;
        }

        private Vector2 GetDirectionVector(ArrowDirection dir)
        {
            return dir switch
            {
                ArrowDirection.Up => Vector2.up,
                ArrowDirection.Down => Vector2.down,
                ArrowDirection.Left => Vector2.left,
                ArrowDirection.Right => Vector2.right,
                _ => Vector2.up
            };
        }

        /// <summary>
        /// 모든 셀의 월드 좌표 반환
        /// </summary>
        public List<Vector2> GetAllWorldPositions()
        {
            return new List<Vector2>(_cellWorldPositions);
        }

        /// <summary>
        /// 등장 연출 시작 (Tail → Head 순차 등장)
        /// </summary>
        public void PlayAppearAnimation(float delay = 0f, Action onComplete = null)
        {
            if (_animationHelper != null)
            {
                _animationHelper.PlayAppearAnimation(
                    _cellWorldPositions,
                    _moveDirection,
                    _visualRenderer?.HeadTailOffset ?? 0.35f,
                    delay,
                    () =>
                    {
                        UpdateCollider();
                        onComplete?.Invoke();
                    });
            }
            else
            {
                onComplete?.Invoke();
            }
        }

        /// <summary>
        /// 즉시 숨기기 (등장 연출 전 호출)
        /// </summary>
        public void HideImmediate()
        {
            _animationHelper?.HideImmediate();
        }

        /// <summary>
        /// 즉시 표시 (연출 없이)
        /// </summary>
        public void ShowImmediate()
        {
            _animationHelper?.ShowImmediate();
            UpdateLineRenderer();
        }

        /// <summary>
        /// LineRenderer 페이드 아웃 전환 시작 (HomingArrow 전환 연출용)
        /// </summary>
        public void StartFadeOutTransition(float duration, Action onComplete)
        {
            if (_animationHelper != null)
            {
                _animationHelper.StartFadeOutTransition(
                    _visualRenderer?.LineWidth ?? 0.3f,
                    duration,
                    onComplete);
            }
            else
            {
                onComplete?.Invoke();
            }
        }

        /// <summary>
        /// 정리
        /// </summary>
        public void Cleanup()
        {
            // 입력 이벤트 구독 해제
            if (_arrowInput != null)
            {
                _arrowInput.OnTapDetected -= HandleTapDetected;
            }

            // 이동 이벤트 구독 해제
            if (_arrowMovement != null)
            {
                _arrowMovement.OnPositionsChanged -= HandlePositionsChanged;
                _arrowMovement.OnStepComplete -= HandleStepComplete;
                _arrowMovement.OnExtractionStarted -= HandleExtractionStarted;
                _arrowMovement.OnExtracted -= HandleExtracted;
                _arrowMovement.OnBlocked -= HandleBlocked;
                _arrowMovement.OnReturnComplete -= HandleReturnComplete;
                _arrowMovement.Cleanup();
            }

            RegisterOccupiedCells(false);
            _occupiedCells?.Clear();
        }

        // ========== 내부 유틸리티 ==========
        private void CacheWorldPositions()
        {
            _cellWorldPositions = new List<Vector2>();
            foreach (var cell in _occupiedCells)
            {
                _cellWorldPositions.Add(GridSystem.Instance.GridToWorld(cell));
            }
        }

        private void SetState(ArrowState newState)
        {
            if (_state == newState)
                return;

            _state = newState;
            OnStateChanged?.Invoke(this);
        }

        private void RegisterOccupiedCells(bool occupied)
        {
            if (_occupiedCells == null) return;

            foreach (var pos in _occupiedCells)
            {
                if (GridSystem.Instance != null && GridSystem.Instance.IsValidPosition(pos))
                {
                    GridSystem.Instance.SetOccupied(pos, occupied);
                }
            }
        }

        private void UpdateLineRenderer()
        {
            if (_occupiedCells == null || _occupiedCells.Count == 0)
                return;

            CacheWorldPositions();
            _visualRenderer?.UpdateLineRenderer(_cellWorldPositions, _moveDirection);
            UpdateCollider();
        }

        private void UpdateLineRendererWithPositions(List<Vector2> worldPositions)
        {
            _visualRenderer?.UpdateLineRenderer(worldPositions, _moveDirection);
        }

        private void UpdateCollider()
        {
            if (_occupiedCells == null || _occupiedCells.Count == 0)
                return;

            // 메인 Collider가 없으면 추가
            if (_collider == null)
            {
                _collider = GetComponent<BoxCollider2D>();
                if (_collider == null)
                {
                    _collider = gameObject.AddComponent<BoxCollider2D>();
                }
            }

            // 모든 셀을 포함하는 바운딩 박스 계산
            float cellSize = GridSystem.Instance.CellSize;
            Vector2 min = _cellWorldPositions[0];
            Vector2 max = _cellWorldPositions[0];

            foreach (var pos in _cellWorldPositions)
            {
                min = Vector2.Min(min, pos);
                max = Vector2.Max(max, pos);
            }

            // Collider 중심과 크기 설정
            Vector2 center = (min + max) * 0.5f;
            Vector2 size = (max - min) + Vector2.one * cellSize;

            // 로컬 좌표로 변환 (transform.position 기준)
            _collider.offset = center - (Vector2)transform.position;
            _collider.size = size;
            _collider.enabled = true;
        }

        private void ClearCellColliders()
        {
            foreach (var col in _cellColliders)
            {
                if (col != null)
                    Destroy(col.gameObject);
            }
            _cellColliders.Clear();
        }

        // ========== 입력 이벤트 핸들러 ==========
        /// <summary>
        /// ArrowInput에서 탭 감지 시 호출
        /// </summary>
        private void HandleTapDetected()
        {
            Debug.Log($"[ArrowController] Arrow {_id} HandleTapDetected, invoking OnTapped");
            OnTapped?.Invoke(this);
        }

        // ========== 이동 이벤트 핸들러 ==========
        private void HandlePositionsChanged(List<Vector2> positions)
        {
            UpdateLineRendererWithPositions(positions);
        }

        private void HandleStepComplete()
        {
            // ArrowMovement에서 위치 데이터 동기화
            if (_arrowMovement != null)
            {
                _occupiedCells = new List<Vector2Int>(_arrowMovement.OccupiedCells);
                _cellWorldPositions = new List<Vector2>(_arrowMovement.CellWorldPositions);
                _headPosition = _arrowMovement.HeadPosition;
            }
            UpdateLineRenderer();
        }

        private void HandleExtractionStarted()
        {
            Vector2 headWorldPos = GetHeadWorldPosition();
            OnExtractionStarted?.Invoke(this, headWorldPos, _headDirection);
        }

        private void HandleExtracted()
        {
            SetState(ArrowState.Extracted);
            OnExtracted?.Invoke(this);
            Destroy(gameObject);
        }

        private void HandleBlocked(bool isArrowCollision)
        {
            if (isArrowCollision)
            {
                OnCollided?.Invoke(this);
            }
            else
            {
                OnWallHit?.Invoke(this);
            }
        }

        private void HandleReturnComplete()
        {
            // ArrowMovement에서 위치 데이터 동기화
            if (_arrowMovement != null)
            {
                _occupiedCells = new List<Vector2Int>(_arrowMovement.OccupiedCells);
                _cellWorldPositions = new List<Vector2>(_arrowMovement.CellWorldPositions);
                _headPosition = _arrowMovement.HeadPosition;
            }

            UpdateLineRenderer();
            _animationHelper?.ApplyMistakeVisual(_color);

            SetState(ArrowState.Idle);
            OnStopped?.Invoke(this);
        }

        // ========== 에디터 전용 ==========
#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_occupiedCells == null || GridSystem.Instance == null)
                return;

            Gizmos.color = UnityEngine.Color.cyan;
            foreach (var pos in _occupiedCells)
            {
                Vector2 worldPos = GridSystem.Instance.GridToWorld(pos);
                Gizmos.DrawWireCube(worldPos, Vector3.one * 0.8f);
            }

            Gizmos.color = UnityEngine.Color.yellow;
            Vector2 headWorld = GridSystem.Instance.GridToWorld(_headPosition);
            Gizmos.DrawWireSphere(headWorld, 0.3f);
            Gizmos.DrawLine(headWorld, headWorld + (Vector2)_moveDirection * 1.5f);
        }
#endif
    }
}