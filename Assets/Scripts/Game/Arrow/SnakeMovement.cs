using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BalloonOut.Core;
using BalloonOut.Game.Grid;

namespace BalloonOut.Game.Arrow
{
    /// <summary>
    /// 이동 결과
    /// </summary>
    public enum MoveResult
    {
        CanMove,    // 이동 가능
        Blocked,    // 다른 화살표에 막힘
        Escaped     // 그리드 밖으로 탈출
    }

    /// <summary>
    /// Snake 방식 이동 시스템 (ArrowPopBall 방식)
    /// cells[0] = TAIL, cells[last] = HEAD
    /// </summary>
    public class SnakeMovement : MonoBehaviour
    {
        // ========== 인스펙터 노출 변수 ==========
        [Header("Animation Settings")]
        [SerializeField] private float _stepDuration = 0.08f;
        [SerializeField] private float _escapeDuration = 0.06f;
        [SerializeField] private float _bounceDuration = 0.1f;
        [SerializeField] private float _bounceDistance = 0.3f;

        // ========== 싱글톤 ==========
        public static SnakeMovement Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// 화살표 이동 시작
        /// </summary>
        public void StartMovement(ArrowController arrow, System.Action<bool> onComplete)
        {
            StartCoroutine(MovementCoroutine(arrow, onComplete));
        }

        // ========== 코루틴 ==========

        /// <summary>
        /// 이동 코루틴
        /// </summary>
        private IEnumerator MovementCoroutine(ArrowController arrow, System.Action<bool> onComplete)
        {
            arrow.SetState(ArrowState.Moving);
            var moveHistory = new List<List<Vector2Int>>();

            while (true)
            {
                var result = TryStep(arrow);

                if (result == MoveResult.Blocked)
                {
                    // 충돌! 바운스백
                    if (moveHistory.Count > 0)
                    {
                        yield return StartCoroutine(BounceBackCoroutine(arrow, moveHistory));
                    }
                    else
                    {
                        yield return StartCoroutine(BounceAnimationCoroutine(arrow));
                    }

                    arrow.SetState(ArrowState.Idle);
                    onComplete?.Invoke(false);
                    yield break;
                }

                if (result == MoveResult.Escaped)
                {
                    // 탈출 시작
                    arrow.SetState(ArrowState.Escaping);
                    yield return StartCoroutine(EscapeCoroutine(arrow));

                    arrow.SetState(ArrowState.Escaped);
                    onComplete?.Invoke(true);
                    yield break;
                }

                // 이동 전 위치 저장
                moveHistory.Add(new List<Vector2Int>(arrow.Cells));

                // 다음 셀 계산
                var newCells = GetNextCells(arrow);

                // 애니메이션
                yield return StartCoroutine(AnimateStepCoroutine(arrow, newCells));

                // 셀 업데이트
                arrow.UpdateCells(newCells);
            }
        }

        /// <summary>
        /// 한 스텝 이동 가능 여부 확인
        /// HEAD는 cells[last]
        /// </summary>
        private MoveResult TryStep(ArrowController arrow)
        {
            if (arrow.Cells.Count == 0) return MoveResult.Blocked;

            // HEAD는 마지막 셀
            var head = arrow.HeadPosition;
            var dir = DirectionHelper.Vectors[arrow.Direction];
            var newHead = head + dir;

            // 1. 그리드 밖으로 나가면 탈출
            if (GridSystem.Instance.IsOutOfBounds(newHead))
            {
                return MoveResult.Escaped;
            }

            // 2. 다른 화살표와 충돌 체크
            var excludeCells = arrow.GetCellSet();
            if (GridSystem.Instance.IsCellOccupiedExcluding(newHead, excludeCells))
            {
                return MoveResult.Blocked;
            }

            return MoveResult.CanMove;
        }

        /// <summary>
        /// 다음 셀 목록 계산 (Snake 이동)
        /// TAIL(cells[0]) 제거, 새 HEAD(cells[last]) 추가
        /// </summary>
        private List<Vector2Int> GetNextCells(ArrowController arrow)
        {
            var cells = arrow.Cells;
            var dir = DirectionHelper.Vectors[arrow.Direction];
            var newHead = arrow.HeadPosition + dir;

            // TAIL 제거 (index 0), 나머지 유지, 새 HEAD 추가
            var newCells = new List<Vector2Int>();
            for (int i = 1; i < cells.Count; i++)
            {
                newCells.Add(cells[i]);
            }
            newCells.Add(newHead);

            return newCells;
        }

        /// <summary>
        /// 스텝 애니메이션 (Snake 방식 - 경로 따라 슬라이딩)
        /// ArrowPopBall 방식
        /// </summary>
        private IEnumerator AnimateStepCoroutine(ArrowController arrow, List<Vector2Int> targetCells)
        {
            if (GridSystem.Instance == null) yield break;

            var visualRenderer = arrow.VisualRenderer;
            if (visualRenderer == null) yield break;

            // 현재 월드 좌표 (TAIL → HEAD 순서)
            var currentPositions = arrow.GetWorldPositions();
            if (currentPositions.Count < 2) yield break;

            // 목표 월드 좌표
            var targetPositions = new List<Vector3>();
            foreach (var cell in targetCells)
            {
                targetPositions.Add(GridSystem.Instance.GridToWorld(cell));
            }

            // 경로 생성: [현재 위치들] + [새 HEAD 위치]
            // ArrowPopBall 방식: fullPath의 마지막에 새 HEAD 위치 추가
            var fullPath = new List<Vector3>(currentPositions);
            fullPath.Add(targetPositions[targetPositions.Count - 1]); // 새 HEAD 위치

            Vector2Int moveDir = arrow.GetMoveDirectionVector();

            float elapsed = 0f;
            while (elapsed < _stepDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _stepDuration);
                t = EaseOutQuad(t);

                // 각 셀이 경로 상에서 가상 인덱스로 이동
                var animatedPositions = new List<Vector3>();
                for (int i = 0; i < targetCells.Count; i++)
                {
                    // t=0일 때 index i, t=1일 때 index i+1
                    float virtualIndex = i + t;
                    Vector3 pos = GetPointOnPath(fullPath, virtualIndex);
                    animatedPositions.Add(pos);
                }

                // VisualRenderer로 업데이트
                visualRenderer.UpdateLineRenderer(animatedPositions, moveDir);

                yield return null;
            }

            // 최종 위치 설정
            visualRenderer.UpdateLineRenderer(targetPositions, moveDir);
        }

        /// <summary>
        /// 경로 상의 특정 인덱스(소수점 포함) 위치 반환
        /// </summary>
        private Vector3 GetPointOnPath(List<Vector3> path, float index)
        {
            if (path.Count == 0) return Vector3.zero;
            if (index <= 0) return path[0];
            if (index >= path.Count - 1) return path[path.Count - 1];

            int floorIndex = Mathf.FloorToInt(index);
            float frac = index - floorIndex;

            if (floorIndex + 1 >= path.Count) return path[path.Count - 1];

            return Vector3.Lerp(path[floorIndex], path[floorIndex + 1], frac);
        }

        /// <summary>
        /// 탈출 코루틴 (꼬리 수축)
        /// TAIL(cells[0])부터 순차적으로 제거
        /// </summary>
        private IEnumerator EscapeCoroutine(ArrowController arrow)
        {
            var visualRenderer = arrow.VisualRenderer;
            if (visualRenderer == null) yield break;

            var dir = DirectionHelper.Vectors[arrow.Direction];
            float cellSize = GridSystem.Instance != null ? GridSystem.Instance.CellSize : 1f;
            Vector3 moveOffset = new Vector3(dir.x, dir.y, 0) * cellSize;
            Vector2Int moveDir = arrow.GetMoveDirectionVector();

            var currentPositions = arrow.GetWorldPositions();

            // Head 먼저 숨기기
            visualRenderer.HideHead();

            // 셀이 2개 이상인 동안 애니메이션
            while (currentPositions.Count >= 2)
            {
                // 시작 위치
                var startPositions = new List<Vector3>(currentPositions);

                // 목표 위치 (모두 한 칸 앞으로)
                var targetPositions = new List<Vector3>();
                foreach (var pos in startPositions)
                {
                    targetPositions.Add(pos + moveOffset);
                }

                // 애니메이션
                float elapsed = 0f;
                while (elapsed < _escapeDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / _escapeDuration);

                    var animatedPositions = new List<Vector3>();
                    for (int i = 0; i < startPositions.Count; i++)
                    {
                        animatedPositions.Add(Vector3.Lerp(startPositions[i], targetPositions[i], t));
                    }

                    visualRenderer.UpdateLineRenderer(animatedPositions, moveDir);
                    yield return null;
                }

                // TAIL 제거 (index 0)
                currentPositions.RemoveAt(0);

                // 남은 위치들을 이동된 위치로 업데이트
                for (int i = 0; i < currentPositions.Count; i++)
                {
                    currentPositions[i] = currentPositions[i] + moveOffset;
                }
            }

            // LineRenderer 즉시 숨기기 (잔상 방지)
            visualRenderer.HideLine();
        }

        /// <summary>
        /// 바운스백 코루틴
        /// </summary>
        private IEnumerator BounceBackCoroutine(ArrowController arrow, List<List<Vector2Int>> history)
        {
            // 히스토리 역순으로 복귀
            for (int i = history.Count - 1; i >= 0; i--)
            {
                yield return StartCoroutine(AnimateStepCoroutine(arrow, history[i]));
                arrow.UpdateCells(history[i]);
            }
        }

        /// <summary>
        /// 바운스 애니메이션 (첫 칸에서 막힘)
        /// </summary>
        private IEnumerator BounceAnimationCoroutine(ArrowController arrow)
        {
            var visualRenderer = arrow.VisualRenderer;
            if (visualRenderer == null) yield break;

            var dir = DirectionHelper.Vectors[arrow.Direction];
            float cellSize = GridSystem.Instance != null ? GridSystem.Instance.CellSize : 1f;
            Vector3 bounceOffset = new Vector3(dir.x, dir.y, 0) * _bounceDistance * cellSize;
            Vector2Int moveDir = arrow.GetMoveDirectionVector();

            var startPositions = arrow.GetWorldPositions();
            if (startPositions.Count < 2) yield break;

            // 앞으로 살짝 이동
            float elapsed = 0f;
            float halfDuration = _bounceDuration * 0.4f;

            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);

                var animatedPositions = new List<Vector3>();
                foreach (var pos in startPositions)
                {
                    animatedPositions.Add(pos + bounceOffset * t);
                }

                visualRenderer.UpdateLineRenderer(animatedPositions, moveDir);
                yield return null;
            }

            // 원위치로 복귀
            elapsed = 0f;
            float returnDuration = _bounceDuration * 0.6f;

            while (elapsed < returnDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / returnDuration);
                t = EaseOutQuad(t);

                var animatedPositions = new List<Vector3>();
                foreach (var pos in startPositions)
                {
                    animatedPositions.Add(pos + bounceOffset * (1 - t));
                }

                visualRenderer.UpdateLineRenderer(animatedPositions, moveDir);
                yield return null;
            }

            // 최종 위치 복원
            visualRenderer.UpdateLineRenderer(startPositions, moveDir);
        }

        // ========== 유틸리티 ==========

        private float EaseOutQuad(float t)
        {
            return 1 - (1 - t) * (1 - t);
        }
    }
}
