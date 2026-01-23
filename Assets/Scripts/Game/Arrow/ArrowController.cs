using System.Collections.Generic;
using UnityEngine;
using BalloonOut.Core;
using BalloonOut.Data;
using BalloonOut.Game.Grid;

namespace BalloonOut.Game.Arrow
{
    /// <summary>
    /// 화살표 컨트롤러 (로직 전담 - 렌더링은 ArrowVisualRenderer에 위임)
    /// ArrowPopBall 방식: cells[0] = TAIL, cells[last] = HEAD
    /// </summary>
    public class ArrowController : MonoBehaviour
    {
        // ========== 이벤트 ==========
        public System.Action<ArrowController> OnTapped;
        public System.Action<ArrowController> OnEscaped;

        // ========== 인스펙터 노출 변수 ==========
        [Header("Rendering")]
        [SerializeField] private ArrowVisualRenderer _visualRenderer;

        // ========== 내부 상태 변수 ==========
        private int _id;
        private GameColor _color;
        private Direction _direction;
        private ArrowState _state = ArrowState.Idle;
        private List<Vector2Int> _cells = new List<Vector2Int>();

        // ========== 프로퍼티 ==========
        public int Id => _id;
        public GameColor Color => _color;
        public Direction Direction => _direction;
        public ArrowState State => _state;
        public List<Vector2Int> Cells => _cells;
        // HEAD는 마지막 셀 (cells[last])
        public Vector2Int HeadPosition => _cells.Count > 0 ? _cells[_cells.Count - 1] : Vector2Int.zero;
        // TAIL은 첫 번째 셀 (cells[0])
        public Vector2Int TailPosition => _cells.Count > 0 ? _cells[0] : Vector2Int.zero;
        public ArrowVisualRenderer VisualRenderer => _visualRenderer;

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// 화살표 초기화
        /// </summary>
        public void Initialize(int id, ArrowData data)
        {
            _id = id;
            _color = data.Color;
            _direction = data.Direction;
            _cells = data.GetCells();
            _state = ArrowState.Idle;

            // 렌더러 초기화
            SetupVisualRenderer();

            // 점유 등록
            if (GridSystem.Instance != null)
            {
                GridSystem.Instance.OccupyCells(_cells);
            }
        }

        /// <summary>
        /// 셀 목록 업데이트 (이동 후)
        /// </summary>
        public void UpdateCells(List<Vector2Int> newCells)
        {
            // 기존 점유 해제
            if (GridSystem.Instance != null)
            {
                GridSystem.Instance.ReleaseCells(_cells);
            }

            _cells = new List<Vector2Int>(newCells);

            // 새 점유 등록
            if (GridSystem.Instance != null)
            {
                GridSystem.Instance.OccupyCells(_cells);
            }

            // 렌더링 업데이트
            UpdateVisuals();
        }

        /// <summary>
        /// 상태 변경
        /// </summary>
        public void SetState(ArrowState state)
        {
            _state = state;
        }

        /// <summary>
        /// HashSet으로 셀 목록 반환
        /// </summary>
        public HashSet<Vector2Int> GetCellSet()
        {
            return new HashSet<Vector2Int>(_cells);
        }

        /// <summary>
        /// 셀의 월드 좌표 목록 반환
        /// </summary>
        public List<Vector3> GetWorldPositions()
        {
            var positions = new List<Vector3>();
            if (GridSystem.Instance == null) return positions;

            foreach (var cell in _cells)
            {
                positions.Add(GridSystem.Instance.GridToWorld(cell));
            }
            return positions;
        }

        /// <summary>
        /// 이동 방향 벡터 반환
        /// </summary>
        public Vector2Int GetMoveDirectionVector()
        {
            return DirectionHelper.Vectors[_direction];
        }

        /// <summary>
        /// 정리
        /// </summary>
        public void Cleanup()
        {
            // 점유 해제
            if (GridSystem.Instance != null)
            {
                GridSystem.Instance.ReleaseCells(_cells);
            }

            _cells.Clear();
        }

        // ========== 내부 유틸리티 ==========

        /// <summary>
        /// VisualRenderer 설정
        /// </summary>
        private void SetupVisualRenderer()
        {
            // VisualRenderer가 없으면 찾거나 추가
            if (_visualRenderer == null)
            {
                _visualRenderer = GetComponent<ArrowVisualRenderer>();
                if (_visualRenderer == null)
                {
                    _visualRenderer = gameObject.AddComponent<ArrowVisualRenderer>();
                }
            }

            // 초기화
            _visualRenderer.Initialize(_color, _direction);

            // 렌더링 업데이트
            UpdateVisuals();
        }

        /// <summary>
        /// 비주얼 업데이트
        /// </summary>
        private void UpdateVisuals()
        {
            if (_visualRenderer == null) return;
            if (_cells.Count < 2) return;

            var worldPositions = GetWorldPositions();
            var moveDir = GetMoveDirectionVector();

            _visualRenderer.UpdateLineRenderer(worldPositions, moveDir);
        }

        // ========== 입력 처리 ==========

        private void OnMouseDown()
        {
            if (_state == ArrowState.Idle)
            {
                OnTapped?.Invoke(this);
            }
        }

        // ========== 정리 ==========

        private void OnDestroy()
        {
            Cleanup();
        }
    }
}
