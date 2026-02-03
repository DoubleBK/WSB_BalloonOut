using System;
using System.Collections.Generic;
using UnityEngine;
using BalloonOut.Core;
using BalloonOut.Game.Grid;
using DG.Tweening;

namespace BalloonOut.Game.Arrow
{
    /// <summary>
    /// 화살표 Snake 이동 로직 담당
    /// 이동 애니메이션, 충돌 판정, 복귀 처리
    /// </summary>
    public class ArrowMovement : MonoBehaviour
    {
        // ========== 이동 설정 ==========
        [Header("Movement Settings")]
        [SerializeField] private float _moveSpeed = 8f;
        [SerializeField] private Ease _moveEase = Ease.OutQuad;

        // ========== 이동 상태 ==========
        private bool _isExtracting;
        private bool _isReturning;
        private bool _ignoreCollision;
        private float _moveProgress;
        private Tween _moveTween;

        // ========== 위치 데이터 ==========
        private List<Vector2Int> _occupiedCells;
        private List<Vector2> _cellWorldPositions;
        private List<Vector2> _previousWorldPositions;
        private Vector2Int _headPosition;
        private Vector2Int _moveDirection;

        // ========== 복귀용 백업 데이터 ==========
        private List<Vector2Int> _launchOccupiedCells;
        private List<Vector2> _launchWorldPositions;
        private Vector2Int _launchHeadPosition;

        // ========== 이동 히스토리 (역방향 복귀용) ==========
        private List<List<Vector2Int>> _movementHistory;
        private int _currentHistoryIndex;
        private int _returnStepsRemaining;

        // ========== 이벤트 ==========
        public event Action<List<Vector2>> OnPositionsChanged;
        public event Action OnStepComplete;
        public event Action OnExtractionStarted;
        public event Action OnExtracted;
        public event Action<bool> OnBlocked;  // true: 화살표 충돌, false: 벽 충돌
        public event Action OnReturnComplete;

        // ========== 프로퍼티 ==========
        public bool IsExtracting => _isExtracting;
        public bool IsReturning => _isReturning;
        public bool IsMoving => _moveTween != null && _moveTween.IsActive() && _moveTween.IsPlaying();
        public Vector2Int HeadPosition => _headPosition;
        public List<Vector2Int> OccupiedCells => _occupiedCells;
        public List<Vector2> CellWorldPositions => _cellWorldPositions;

        // ========== 초기화 ==========
        /// <summary>
        /// 이동 시스템 초기화
        /// </summary>
        public void Initialize(List<Vector2Int> occupiedCells, Vector2Int headPosition, Vector2Int moveDirection)
        {
            _occupiedCells = new List<Vector2Int>(occupiedCells);
            _headPosition = headPosition;
            _moveDirection = moveDirection;
            _isExtracting = false;
            _isReturning = false;
            _ignoreCollision = false;

            CacheWorldPositions();
        }

        /// <summary>
        /// 위치 데이터 업데이트 (외부에서 변경 시)
        /// </summary>
        public void UpdatePositions(List<Vector2Int> occupiedCells, Vector2Int headPosition)
        {
            _occupiedCells = new List<Vector2Int>(occupiedCells);
            _headPosition = headPosition;
            CacheWorldPositions();
        }

        // ========== 이동 시작 ==========
        /// <summary>
        /// 일반 이동 시작 (충돌 판정 포함)
        /// </summary>
        public void StartMove()
        {
            _ignoreCollision = false;
            BackupLaunchPosition();

            // 화살표 탭/클릭 효과음 재생
            SFXManager.Instance?.PlayArrowPick();

            TryMoveToNext();
        }

        /// <summary>
        /// 충돌 무시 이동 시작 (Arrow Dash용)
        /// </summary>
        public void StartMoveIgnoreCollision()
        {
            _ignoreCollision = true;
            _isExtracting = true;
            RegisterOccupiedCells(false);

            OnExtractionStarted?.Invoke();

            Vector2Int nextHeadPos = _headPosition + _moveDirection;
            StartSnakeExtract(nextHeadPos);
        }

        // ========== 정리 ==========
        public void Cleanup()
        {
            _moveTween?.Kill();
            _movementHistory?.Clear();
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        // ========== 내부 이동 로직 ==========
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

        private void TryMoveToNext()
        {
            Vector2Int nextHeadPos = _headPosition + _moveDirection;

            // 그리드 밖으로 나가면 탈출 시작
            if (GridSystem.Instance.IsOutOfWorldBounds(nextHeadPos))
            {
                if (!_isExtracting)
                {
                    _isExtracting = true;
                    RegisterOccupiedCells(false);
                    OnExtractionStarted?.Invoke();
                }

                StartSnakeExtract(nextHeadPos);
                return;
            }

            // 이동 가능 여부 확인
            if (!CanMoveTo(nextHeadPos))
            {
                bool isArrowCollision = GridSystem.Instance.IsOccupied(nextHeadPos);
                OnBlocked?.Invoke(isArrowCollision);
                StartReverseReturn();
                return;
            }

            StartSnakeMove(nextHeadPos);
        }

        private void StartSnakeMove(Vector2Int nextHeadPos)
        {
            _previousWorldPositions = new List<Vector2>(_cellWorldPositions);

            // 꼬리 셀 해제
            Vector2Int tailPos = _occupiedCells[0];
            if (GridSystem.Instance.IsValidPosition(tailPos))
            {
                GridSystem.Instance.SetOccupied(tailPos, false);
            }

            // 새 머리 위치 점유
            if (GridSystem.Instance.IsValidPosition(nextHeadPos))
            {
                GridSystem.Instance.SetOccupied(nextHeadPos, true);
            }

            StartMoveTween();
        }

        private void StartSnakeExtract(Vector2Int nextHeadPos)
        {
            _previousWorldPositions = new List<Vector2>(_cellWorldPositions);
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
                // 경로 기반 슬라이딩
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

            OnPositionsChanged?.Invoke(animatedPositions);
        }

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
                // 탈출 중: 꼬리 제거, 머리 추가
                if (_occupiedCells.Count > 0)
                {
                    _occupiedCells.RemoveAt(0);
                    _cellWorldPositions.RemoveAt(0);
                }

                _occupiedCells.Add(nextHeadPos);
                _cellWorldPositions.Add(GridSystem.Instance.GridToWorld(nextHeadPos));
                _headPosition = nextHeadPos;

                // 모든 셀이 그리드 밖으로 나갔는지 확인
                if (_occupiedCells.Count == 0 || AreAllCellsOutOfBounds())
                {
                    OnExtracted?.Invoke();
                    return;
                }

                _previousWorldPositions = new List<Vector2>(_cellWorldPositions);
            }
            else
            {
                // 일반 이동: 꼬리 제거, 머리 추가
                _occupiedCells.RemoveAt(0);
                _occupiedCells.Add(nextHeadPos);
                _headPosition = nextHeadPos;

                CacheWorldPositions();
                RecordMovementSnapshot();
                _previousWorldPositions = new List<Vector2>(_cellWorldPositions);
            }

            OnStepComplete?.Invoke();
            TryMoveToNext();
        }

        // ========== 역방향 복귀 ==========
        private void StartReverseReturn()
        {
            if (_movementHistory == null || _movementHistory.Count <= 1)
            {
                OnReturnComplete?.Invoke();
                return;
            }

            _previousWorldPositions = new List<Vector2>(_cellWorldPositions);
            _currentHistoryIndex = _movementHistory.Count - 1;
            _returnStepsRemaining = _movementHistory.Count - 1;
            _isReturning = true;

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

            OnPositionsChanged?.Invoke(animatedPositions);
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

            OnStepComplete?.Invoke();

            if (_currentHistoryIndex <= 0 || _returnStepsRemaining <= 0)
            {
                // 원래 위치로 완전 복귀
                RegisterOccupiedCells(false);
                _occupiedCells = new List<Vector2Int>(_launchOccupiedCells);
                _cellWorldPositions = new List<Vector2>(_launchWorldPositions);
                _headPosition = _launchHeadPosition;
                RegisterOccupiedCells(true);

                _isReturning = false;
                _movementHistory = null;

                OnReturnComplete?.Invoke();
            }
            else
            {
                StartReverseMoveTween();
            }
        }

        // ========== 유틸리티 ==========
        private void CacheWorldPositions()
        {
            _cellWorldPositions = new List<Vector2>();
            foreach (var cell in _occupiedCells)
            {
                _cellWorldPositions.Add(GridSystem.Instance.GridToWorld(cell));
            }
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

            // 꼬리 위치로 이동하는 것은 허용
            if (_occupiedCells.Count > 0 && nextHeadPos == _occupiedCells[0])
                return true;

            // 자기 몸과 충돌 체크
            for (int i = 1; i < _occupiedCells.Count; i++)
            {
                if (nextHeadPos == _occupiedCells[i])
                    return false;
            }

            // 그리드 경계 밖이지만 월드 경계 안이면 허용 (탈출 구역)
            if (GridSystem.Instance.IsOutOfBounds(nextHeadPos) &&
                !GridSystem.Instance.IsOutOfWorldBounds(nextHeadPos))
            {
                return true;
            }

            // 다른 화살표와 충돌 체크
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
    }
}
