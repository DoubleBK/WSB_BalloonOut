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
        [SerializeField] private BoxCollider2D _collider;

        [Header("이동 설정")]
        [SerializeField] private float _moveSpeed = 8f;

        [Header("애니메이션 설정")]
        [SerializeField] private Ease _moveEase = Ease.OutQuad;
        [SerializeField] private float _launchPunchScale = 0.15f;
        [SerializeField] private float _launchPunchDuration = 0.1f;

        // ========== 내부 상태 변수 ==========
        private int _id;
        private GameColor _color;
        private ArrowState _state;
        private List<Vector2Int> _occupiedCells;
        private Vector2Int _headPosition;
        private ArrowDirection _headDirection;
        private Vector2Int _moveDirection;

        // Snake 이동용 변수
        private bool _isExtracting;
        private bool _isReturning;
        private bool _ignoreCollision;
        private float _moveProgress;
        private List<Vector2> _cellWorldPositions;
        private List<Vector2> _previousWorldPositions;
        private Tween _moveTween;

        // 발사 시점 위치 백업 (충돌 시 복원용)
        private List<Vector2Int> _launchOccupiedCells;
        private List<Vector2> _launchWorldPositions;
        private Vector2Int _launchHeadPosition;
        private int _returnStepsRemaining;

        // 이동 경로 기록 (역방향 복귀용)
        private List<List<Vector2Int>> _movementHistory;
        private int _currentHistoryIndex;

        // 셀별 개별 콜라이더
        private List<BoxCollider2D> _cellColliders = new List<BoxCollider2D>();

        // 전역 입력 처리용 정적 변수 (같은 프레임에서 중복 터치 방지)
        private static int _lastInputFrame = -1;
        private static ArrowController _lastTouchedArrow = null;

        // 활성 화살표 수 추적 (디버그용)
        private static int _activeArrowCount = 0;

        // ========== 탭/드래그 판정용 ==========
        private bool _isPotentialTap = false;       // 탭 후보 상태
        private Vector2 _tapStartScreenPos;         // 시작 화면 좌표
        private float _tapStartTime;                // 시작 시간
        private const float TAP_MAX_DISTANCE = 20f; // 탭 최대 이동 거리 (픽셀)
        private const float TAP_MAX_DURATION = 0.5f; // 탭 최대 지속 시간 (초)

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
        public bool IsExtracting => _isExtracting;
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
            _isPotentialTap = false;  // 탭 상태 초기화
            Debug.Log($"[ArrowController] Arrow {_id} OnDisable, active count: {_activeArrowCount}");

            // 파괴된 화살표가 _lastTouchedArrow면 클리어
            if (_lastTouchedArrow == this)
            {
                _lastTouchedArrow = null;
                Debug.Log($"[ArrowController] Cleared _lastTouchedArrow (was Arrow {_id})");
            }
        }

        private void OnDestroy()
        {
            _moveTween?.Kill();
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
            _isExtracting = false;

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

            _ignoreCollision = false;
            BackupLaunchPosition();

            Debug.Log($"[ArrowController] Arrow {_id} starting movement, Direction: {_headDirection}");

            // 이동 시작 이벤트 발생
            OnMoveStarted?.Invoke(this);

            // 펀치 애니메이션과 이동을 동시에 시작
            transform.DOPunchScale(Vector3.one * _launchPunchScale, _launchPunchDuration, 1, 0f);
            TryMoveToNext();
        }

        /// <summary>
        /// 충돌 무시하고 즉시 탈출 (Arrow Dash용)
        /// </summary>
        public void LaunchWithoutCollision()
        {
            if (_state != ArrowState.Idle)
                return;

            _ignoreCollision = true;

            OnMoveStarted?.Invoke(this);

            _isExtracting = true;
            RegisterOccupiedCells(false);

            Vector2 headWorldPos = GetHeadWorldPosition();
            OnExtractionStarted?.Invoke(this, headWorldPos, _headDirection);

            transform.DOPunchScale(Vector3.one * _launchPunchScale, _launchPunchDuration, 1, 0f);

            Vector2Int nextHeadPos = _headPosition + _moveDirection;
            StartSnakeExtract(nextHeadPos);
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
            RegisterOccupiedCells(false);
            _occupiedCells?.Clear();
        }

        // ========== 내부 유틸리티 ==========
        private void BackupLaunchPosition()
        {
            _launchOccupiedCells = new List<Vector2Int>(_occupiedCells);
            _launchWorldPositions = new List<Vector2>(_cellWorldPositions);
            _launchHeadPosition = _headPosition;

            _movementHistory = new List<List<Vector2Int>>();
            _movementHistory.Add(new List<Vector2Int>(_occupiedCells));
        }

        private void RecordMovementSnapshot()
        {
            _movementHistory?.Add(new List<Vector2Int>(_occupiedCells));
        }

        private void StartReverseReturn()
        {
            if (_movementHistory == null || _movementHistory.Count <= 1)
            {
                _animationHelper?.ApplyMistakeVisual(_color);
                SetState(ArrowState.Idle);
                OnStopped?.Invoke(this);
                return;
            }

            _previousWorldPositions = new List<Vector2>(_cellWorldPositions);
            _currentHistoryIndex = _movementHistory.Count - 1;
            _returnStepsRemaining = _movementHistory.Count - 1;
            _isReturning = true;
            SetState(ArrowState.Moving);

            StartReverseMoveTween();
        }

        private void StartReverseMoveTween()
        {
            _moveTween?.Kill();

            float duration = 1f / _moveSpeed;

            _moveProgress = 0f;
            _moveTween = DOTween.To(
                () => _moveProgress,
                x =>
                {
                    _moveProgress = x;
                    UpdateReverseReturnAnimation(_moveProgress);
                },
                1f,
                duration
            )
            .SetEase(_moveEase)
            .OnComplete(CompleteReverseStep);
        }

        private void UpdateReverseReturnAnimation(float t)
        {
            if (_previousWorldPositions == null || _currentHistoryIndex <= 0)
                return;

            List<Vector2Int> targetCells = _movementHistory[_currentHistoryIndex - 1];
            List<Vector2> targetPositions = new List<Vector2>();
            foreach (var cell in targetCells)
            {
                targetPositions.Add(GridSystem.Instance.GridToWorld(cell));
            }

            List<Vector2> animatedPositions = new List<Vector2>();
            int cellCount = Mathf.Max(_previousWorldPositions.Count, targetPositions.Count);

            for (int i = 0; i < cellCount; i++)
            {
                Vector2 startPos = i < _previousWorldPositions.Count
                    ? _previousWorldPositions[i]
                    : targetPositions[i];

                Vector2 targetPos = i < targetPositions.Count
                    ? targetPositions[i]
                    : startPos;

                animatedPositions.Add(Vector2.Lerp(startPos, targetPos, t));
            }

            UpdateLineRendererWithPositions(animatedPositions);
        }

        private void CompleteReverseStep()
        {
            _currentHistoryIndex--;
            if (_currentHistoryIndex < 0)
                _currentHistoryIndex = 0;

            List<Vector2Int> targetCells = _movementHistory[_currentHistoryIndex];

            RegisterOccupiedCells(false);
            _occupiedCells = new List<Vector2Int>(targetCells);
            CacheWorldPositions();
            RegisterOccupiedCells(true);

            if (_occupiedCells.Count > 0)
            {
                _headPosition = _occupiedCells[_occupiedCells.Count - 1];
            }

            _returnStepsRemaining--;
            _previousWorldPositions = new List<Vector2>(_cellWorldPositions);

            UpdateLineRenderer();

            if (_currentHistoryIndex <= 0 || _returnStepsRemaining <= 0)
            {
                RegisterOccupiedCells(false);
                _occupiedCells = new List<Vector2Int>(_launchOccupiedCells);
                _cellWorldPositions = new List<Vector2>(_launchWorldPositions);
                _headPosition = _launchHeadPosition;
                RegisterOccupiedCells(true);

                _isReturning = false;
                _movementHistory = null;
                UpdateLineRenderer();

                _animationHelper?.ApplyMistakeVisual(_color);

                SetState(ArrowState.Idle);
                OnStopped?.Invoke(this);
            }
            else
            {
                StartReverseMoveTween();
            }
        }

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

        private void TryMoveToNext()
        {
            Vector2Int nextHeadPos = _headPosition + _moveDirection;

            if (GridSystem.Instance.IsOutOfWorldBounds(nextHeadPos))
            {
                if (!_isExtracting)
                {
                    _isExtracting = true;
                    RegisterOccupiedCells(false);

                    Vector2 headWorldPos = GetHeadWorldPosition();
                    OnExtractionStarted?.Invoke(this, headWorldPos, _headDirection);
                }

                StartSnakeExtract(nextHeadPos);
                return;
            }

            if (!CanMoveTo(nextHeadPos))
            {
                bool isArrowCollision = GridSystem.Instance.IsOccupied(nextHeadPos);

                if (isArrowCollision)
                {
                    OnCollided?.Invoke(this);
                }
                else
                {
                    OnWallHit?.Invoke(this);
                }

                StartReverseReturn();
                return;
            }

            StartSnakeMove(nextHeadPos);
        }

        private void StartSnakeMove(Vector2Int nextHeadPos)
        {
            _previousWorldPositions = new List<Vector2>(_cellWorldPositions);

            Vector2Int tailPos = _occupiedCells[0];
            if (GridSystem.Instance.IsValidPosition(tailPos))
            {
                GridSystem.Instance.SetOccupied(tailPos, false);
            }

            if (GridSystem.Instance.IsValidPosition(nextHeadPos))
            {
                GridSystem.Instance.SetOccupied(nextHeadPos, true);
            }

            SetState(ArrowState.Moving);
            StartMoveTween();
        }

        private void StartSnakeExtract(Vector2Int nextHeadPos)
        {
            _previousWorldPositions = new List<Vector2>(_cellWorldPositions);
            SetState(ArrowState.Moving);
            StartMoveTween();
        }

        private void StartMoveTween()
        {
            _moveTween?.Kill();

            float duration = 1f / _moveSpeed;

            _moveProgress = 0f;
            _moveTween = DOTween.To(
                () => _moveProgress,
                x =>
                {
                    _moveProgress = x;
                    UpdateSnakeAnimation(_moveProgress);
                },
                1f,
                duration
            )
            .SetEase(_moveEase)
            .OnComplete(CompleteOneStep);
        }

        /// <summary>
        /// Snake 애니메이션 업데이트 - 경로 기반 슬라이딩
        /// </summary>
        private void UpdateSnakeAnimation(float t)
        {
            if (_previousWorldPositions == null || _previousWorldPositions.Count == 0)
                return;

            List<Vector2> animatedPositions = new List<Vector2>();
            float cellSize = GridSystem.Instance.CellSize;

            Vector2 headStartPos = _previousWorldPositions[_previousWorldPositions.Count - 1];
            Vector2 headTargetPos = headStartPos + (Vector2)_moveDirection * cellSize;

            if (_isExtracting)
            {
                // 탈출 중: 꼬리가 수축하면서 경로를 따라 이동
                Vector2 tailTargetPos = _previousWorldPositions.Count > 1
                    ? _previousWorldPositions[1]
                    : headTargetPos;

                Vector2 shrinkingTailPos = Vector2.Lerp(_previousWorldPositions[0], tailTargetPos, t);
                animatedPositions.Add(shrinkingTailPos);

                for (int i = 1; i < _previousWorldPositions.Count; i++)
                {
                    Vector2 startPos = _previousWorldPositions[i];
                    Vector2 targetPos = (i == _previousWorldPositions.Count - 1)
                        ? headTargetPos
                        : _previousWorldPositions[i + 1];

                    animatedPositions.Add(Vector2.Lerp(startPos, targetPos, t));
                }
            }
            else
            {
                // 개선된 경로 기반 슬라이딩
                List<Vector2> fullPath = new List<Vector2>(_previousWorldPositions);
                fullPath.Add(headTargetPos);

                int cellCount = _cellWorldPositions.Count;

                for (int i = 0; i < cellCount; i++)
                {
                    float virtualIndex = i + t;
                    Vector2 pos = GetPointOnPath(fullPath, virtualIndex);
                    animatedPositions.Add(pos);
                }
            }

            UpdateLineRendererWithPositions(animatedPositions);
        }

        /// <summary>
        /// 경로 상의 특정 위치(index) 좌표를 반환
        /// </summary>
        private Vector2 GetPointOnPath(List<Vector2> path, float index)
        {
            if (path == null || path.Count == 0)
                return Vector2.zero;

            if (index <= 0)
                return path[0];
            if (index >= path.Count - 1)
                return path[path.Count - 1];

            int floorIndex = Mathf.FloorToInt(index);
            float fraction = index - floorIndex;

            Vector2 p0 = path[floorIndex];
            Vector2 p1 = path[floorIndex + 1];

            return Vector2.Lerp(p0, p1, fraction);
        }

        private void CompleteOneStep()
        {
            Vector2Int nextHeadPos = _headPosition + _moveDirection;

            if (_isExtracting)
            {
                if (_occupiedCells.Count > 0)
                {
                    _occupiedCells.RemoveAt(0);
                    _cellWorldPositions.RemoveAt(0);
                }

                _occupiedCells.Add(nextHeadPos);
                _cellWorldPositions.Add(GridSystem.Instance.GridToWorld(nextHeadPos));
                _headPosition = nextHeadPos;

                if (_occupiedCells.Count == 0 || AreAllCellsOutOfBounds())
                {
                    SetState(ArrowState.Extracted);
                    OnExtracted?.Invoke(this);
                    Destroy(gameObject);
                    return;
                }

                _previousWorldPositions = new List<Vector2>(_cellWorldPositions);
            }
            else
            {
                _occupiedCells.RemoveAt(0);
                _occupiedCells.Add(nextHeadPos);
                _headPosition = nextHeadPos;

                CacheWorldPositions();
                RecordMovementSnapshot();
                _previousWorldPositions = new List<Vector2>(_cellWorldPositions);
            }

            UpdateLineRenderer();
            TryMoveToNext();
        }

        private bool AreAllCellsOutOfBounds()
        {
            foreach (var cell in _occupiedCells)
            {
                if (!GridSystem.Instance.IsOutOfWorldBounds(cell))
                    return false;
            }
            return true;
        }

        private bool CanMoveTo(Vector2Int nextHeadPos)
        {
            if (_ignoreCollision)
                return true;

            if (_occupiedCells.Count > 0 && nextHeadPos == _occupiedCells[0])
                return true;

            for (int i = 1; i < _occupiedCells.Count; i++)
            {
                if (nextHeadPos == _occupiedCells[i])
                    return false;
            }

            if (GridSystem.Instance.IsOutOfBounds(nextHeadPos) &&
                !GridSystem.Instance.IsOutOfWorldBounds(nextHeadPos))
            {
                return true;
            }

            if (GridSystem.Instance.IsOccupied(nextHeadPos))
                return false;

            return true;
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

        // ========== 입력 처리 ==========
        private void Update()
        {
            // ===== 1. 입력 감지 =====
            bool inputDown = false;
            bool inputUp = false;
            Vector2 inputPos = Vector2.zero;

#if UNITY_EDITOR || UNITY_STANDALONE
            if (Input.GetMouseButtonDown(0))
            {
                inputDown = true;
                inputPos = Input.mousePosition;
            }
            if (Input.GetMouseButtonUp(0))
            {
                inputUp = true;
                inputPos = Input.mousePosition;
            }
#else
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    inputDown = true;
                    inputPos = touch.position;
                }
                else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    inputUp = true;
                    inputPos = touch.position;
                }
            }
#endif

            // ===== 2. 클릭 시작 처리 (탭 후보 등록) =====
            if (inputDown)
            {
                // 상태 체크 (Idle 상태에서만 탭 가능)
                if (_state != ArrowState.Idle) return;
                if (_animationHelper != null && _animationHelper.IsAppearing) return;

                // 화면 좌표를 월드 좌표로 변환
                Vector3 worldPos3D = Camera.main.ScreenToWorldPoint(inputPos);
                Vector2 touchWorldPos = new Vector2(worldPos3D.x, worldPos3D.y);

                // 화살표 위에서 시작했는지 확인
                if (IsTouchOnArrowCells(touchWorldPos))
                {
                    // 같은 프레임에서 다른 화살표가 이미 터치됐으면 무시
                    if (_lastInputFrame == Time.frameCount && _lastTouchedArrow != null)
                        return;

                    // 탭 후보로 등록
                    _isPotentialTap = true;
                    _tapStartScreenPos = inputPos;
                    _tapStartTime = Time.time;
                    _lastInputFrame = Time.frameCount;
                    _lastTouchedArrow = this;

                    Debug.Log($"[ArrowController] Arrow {_id} tap started at {inputPos}");
                }
            }

            // ===== 3. 클릭 종료 처리 (탭 판정) =====
            if (inputUp && _isPotentialTap)
            {
                _isPotentialTap = false;

                // 상태 재확인 (드래그 중 상태가 변경됐을 수 있음)
                if (_state != ArrowState.Idle)
                {
                    Debug.Log($"[ArrowController] Arrow {_id} tap cancelled - state changed to {_state}");
                    return;
                }

                float distance = Vector2.Distance(inputPos, _tapStartScreenPos);
                float duration = Time.time - _tapStartTime;

                Debug.Log($"[ArrowController] Arrow {_id} input ended: distance={distance:F1}px, duration={duration:F2}s");

                // 탭 판정: 거리와 시간 모두 임계값 이하
                if (distance < TAP_MAX_DISTANCE && duration < TAP_MAX_DURATION)
                {
                    Debug.Log($"[ArrowController] Arrow {_id} TAP detected! Invoking OnTapped");
                    OnTapped?.Invoke(this);
                }
                else
                {
                    Debug.Log($"[ArrowController] Arrow {_id} was DRAG or HOLD, not TAP");
                }
            }

            // ===== 4. 탭 취소 조건 =====
            // 드래그가 감지되면 탭 후보 취소
            if (_isPotentialTap && CameraController.Instance != null && CameraController.Instance.HasDragged)
            {
                Debug.Log($"[ArrowController] Arrow {_id} tap cancelled - drag detected");
                _isPotentialTap = false;
            }
        }

        /// <summary>
        /// 터치 위치가 화살표 셀 내에 있는지 확인
        /// </summary>
        private bool IsTouchOnArrowCells(Vector2 worldPos)
        {
            // GridSystem이 없으면 기존 동작 유지 (터치 허용)
            if (GridSystem.Instance == null)
            {
                Debug.LogWarning($"[ArrowController] GridSystem.Instance is null, allowing touch");
                return true;
            }

            // _cellWorldPositions가 비어있으면 기존 동작 유지 (터치 허용)
            if (_cellWorldPositions == null || _cellWorldPositions.Count == 0)
            {
                Debug.LogWarning($"[ArrowController] _cellWorldPositions is null or empty for Arrow {_id}, allowing touch");
                return true;
            }

            float cellSize = GridSystem.Instance.CellSize;
            // 여유분 20% 추가 (터치 영역 확장)
            float halfCell = cellSize * 0.6f;

            foreach (var cellPos in _cellWorldPositions)
            {
                // 셀 중심에서 반경 내에 있는지 확인
                float dx = Mathf.Abs(worldPos.x - cellPos.x);
                float dy = Mathf.Abs(worldPos.y - cellPos.y);

                if (dx <= halfCell && dy <= halfCell)
                {
                    return true;  // 터치가 이 셀 안에 있음
                }
            }

            // 디버그: 첫 번째 셀과의 거리 출력
            if (_cellWorldPositions.Count > 0)
            {
                var firstCell = _cellWorldPositions[0];
                Debug.Log($"[ArrowController] Arrow {_id}: touch={worldPos}, firstCell={firstCell}, cellSize={cellSize}, halfCell={halfCell}");
            }

            return false;  // 어떤 셀에도 속하지 않음
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