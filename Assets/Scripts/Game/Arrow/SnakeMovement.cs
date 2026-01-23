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
    /// Snake 방식 이동 시스템
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

                // 한 칸 이동
                var newCells = GetNextCells(arrow);
                arrow.UpdateCells(newCells);

                yield return StartCoroutine(AnimateStepCoroutine(arrow));
            }
        }

        /// <summary>
        /// 한 스텝 이동 가능 여부 확인
        /// </summary>
        private MoveResult TryStep(ArrowController arrow)
        {
            if (arrow.Cells.Count == 0) return MoveResult.Blocked;

            var head = arrow.Cells[0];
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
        /// </summary>
        private List<Vector2Int> GetNextCells(ArrowController arrow)
        {
            var cells = arrow.Cells;
            var dir = DirectionHelper.Vectors[arrow.Direction];
            var newHead = cells[0] + dir;

            var newCells = new List<Vector2Int> { newHead };
            for (int i = 0; i < cells.Count - 1; i++)
            {
                newCells.Add(cells[i]);
            }

            return newCells;
        }

        /// <summary>
        /// 스텝 애니메이션
        /// </summary>
        private IEnumerator AnimateStepCoroutine(ArrowController arrow)
        {
            var cellObjects = arrow.GetCellObjects();
            var cells = arrow.Cells;

            // 각 셀 오브젝트를 새 위치로 애니메이션
            float elapsed = 0f;
            var startPositions = new List<Vector3>();
            var targetPositions = new List<Vector3>();

            foreach (var obj in cellObjects)
            {
                startPositions.Add(obj.transform.position);
            }

            for (int i = 0; i < cells.Count && i < cellObjects.Count; i++)
            {
                targetPositions.Add(GridSystem.Instance.GridToWorld(cells[i]));
            }

            while (elapsed < _stepDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _stepDuration);
                t = EaseOutQuad(t);

                for (int i = 0; i < cellObjects.Count && i < targetPositions.Count; i++)
                {
                    cellObjects[i].transform.position = Vector3.Lerp(startPositions[i], targetPositions[i], t);
                }

                yield return null;
            }

            // 최종 위치 보정
            for (int i = 0; i < cellObjects.Count && i < targetPositions.Count; i++)
            {
                cellObjects[i].transform.position = targetPositions[i];
            }
        }

        /// <summary>
        /// 탈출 코루틴
        /// </summary>
        private IEnumerator EscapeCoroutine(ArrowController arrow)
        {
            var dir = DirectionHelper.Vectors[arrow.Direction];
            var cellObjects = new List<GameObject>(arrow.GetCellObjects());

            // 모든 셀이 사라질 때까지
            while (cellObjects.Count > 0)
            {
                // 모든 셀을 한 칸 앞으로 이동
                var startPositions = new List<Vector3>();
                var targetPositions = new List<Vector3>();

                foreach (var obj in cellObjects)
                {
                    startPositions.Add(obj.transform.position);
                    var targetPos = obj.transform.position + new Vector3(dir.x, dir.y, 0) * GridSystem.Instance.CellSize;
                    targetPositions.Add(targetPos);
                }

                // 애니메이션
                float elapsed = 0f;
                while (elapsed < _escapeDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(elapsed / _escapeDuration);

                    for (int i = 0; i < cellObjects.Count; i++)
                    {
                        if (cellObjects[i] != null)
                        {
                            cellObjects[i].transform.position = Vector3.Lerp(startPositions[i], targetPositions[i], t);
                        }
                    }

                    yield return null;
                }

                // 마지막 셀(꼬리) 제거
                var tail = cellObjects[cellObjects.Count - 1];
                cellObjects.RemoveAt(cellObjects.Count - 1);
                if (tail != null)
                {
                    Destroy(tail);
                }
            }
        }

        /// <summary>
        /// 바운스백 코루틴
        /// </summary>
        private IEnumerator BounceBackCoroutine(ArrowController arrow, List<List<Vector2Int>> history)
        {
            // 히스토리 역순으로 복귀
            for (int i = history.Count - 1; i >= 0; i--)
            {
                arrow.UpdateCells(history[i]);
                yield return StartCoroutine(AnimateStepCoroutine(arrow));
            }
        }

        /// <summary>
        /// 바운스 애니메이션 (첫 칸에서 막힘)
        /// </summary>
        private IEnumerator BounceAnimationCoroutine(ArrowController arrow)
        {
            var cellObjects = arrow.GetCellObjects();
            if (cellObjects.Count == 0) yield break;

            var dir = DirectionHelper.Vectors[arrow.Direction];
            var bounceOffset = new Vector3(dir.x, dir.y, 0) * _bounceDistance * GridSystem.Instance.CellSize;

            var startPositions = new List<Vector3>();
            foreach (var obj in cellObjects)
            {
                startPositions.Add(obj.transform.position);
            }

            // 앞으로 살짝 이동
            float elapsed = 0f;
            float halfDuration = _bounceDuration * 0.4f;

            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / halfDuration);

                for (int i = 0; i < cellObjects.Count; i++)
                {
                    cellObjects[i].transform.position = startPositions[i] + bounceOffset * t;
                }

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

                for (int i = 0; i < cellObjects.Count; i++)
                {
                    cellObjects[i].transform.position = Vector3.Lerp(startPositions[i] + bounceOffset, startPositions[i], t);
                }

                yield return null;
            }

            // 최종 위치 보정
            for (int i = 0; i < cellObjects.Count; i++)
            {
                cellObjects[i].transform.position = startPositions[i];
            }
        }

        // ========== 유틸리티 ==========

        private float EaseOutQuad(float t)
        {
            return 1 - (1 - t) * (1 - t);
        }
    }
}