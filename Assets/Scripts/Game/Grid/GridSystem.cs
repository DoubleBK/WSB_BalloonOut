using System.Collections.Generic;
using UnityEngine;
using BalloonOut.Core;

namespace BalloonOut.Game.Grid
{
    /// <summary>
    /// 그리드 좌표 시스템
    /// </summary>
    public class GridSystem : MonoBehaviour
    {
        // ========== 싱글톤 ==========
        public static GridSystem Instance { get; private set; }

        // ========== 인스펙터 노출 변수 ==========
        [Header("Grid Settings")]
        [SerializeField] private float _cellSize = 1f;
        [SerializeField] private Vector2 _gridOffset = Vector2.zero;

        [Header("Debug")]
        [SerializeField] private bool _drawGizmos = true;
        [SerializeField] private Color _gridColor = new Color(0.5f, 0.5f, 0.5f, 0.3f);

        // ========== 내부 상태 변수 ==========
        private int _gridSize;
        private HashSet<Vector2Int> _occupiedCells = new HashSet<Vector2Int>();

        // ========== 프로퍼티 ==========
        public int GridSize => _gridSize;
        public float CellSize => _cellSize;

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

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// 그리드 초기화
        /// </summary>
        public void Initialize(int gridSize)
        {
            _gridSize = gridSize;
            _occupiedCells.Clear();

            // 그리드가 화면 중앙에 오도록 오프셋 계산
            float halfGrid = (gridSize - 1) * _cellSize * 0.5f;
            _gridOffset = new Vector2(-halfGrid, -halfGrid);

            Debug.Log($"GridSystem initialized: {gridSize}x{gridSize}, CellSize: {_cellSize}");
        }

        /// <summary>
        /// 그리드 좌표 → 월드 좌표
        /// </summary>
        public Vector3 GridToWorld(Vector2Int gridPos)
        {
            float x = gridPos.x * _cellSize + _gridOffset.x;
            float y = gridPos.y * _cellSize + _gridOffset.y;
            return new Vector3(x, y, 0);
        }

        /// <summary>
        /// 월드 좌표 → 그리드 좌표
        /// </summary>
        public Vector2Int WorldToGrid(Vector3 worldPos)
        {
            int x = Mathf.RoundToInt((worldPos.x - _gridOffset.x) / _cellSize);
            int y = Mathf.RoundToInt((worldPos.y - _gridOffset.y) / _cellSize);
            return new Vector2Int(x, y);
        }

        /// <summary>
        /// 그리드 범위 내인지 확인
        /// </summary>
        public bool IsInBounds(Vector2Int pos)
        {
            return pos.x >= 0 && pos.x < _gridSize &&
                   pos.y >= 0 && pos.y < _gridSize;
        }

        /// <summary>
        /// 그리드 범위 밖인지 확인
        /// </summary>
        public bool IsOutOfBounds(Vector2Int pos)
        {
            return !IsInBounds(pos);
        }

        /// <summary>
        /// 셀 점유 등록
        /// </summary>
        public void OccupyCell(Vector2Int pos)
        {
            _occupiedCells.Add(pos);
        }

        /// <summary>
        /// 여러 셀 점유 등록
        /// </summary>
        public void OccupyCells(IEnumerable<Vector2Int> positions)
        {
            foreach (var pos in positions)
            {
                _occupiedCells.Add(pos);
            }
        }

        /// <summary>
        /// 셀 점유 해제
        /// </summary>
        public void ReleaseCell(Vector2Int pos)
        {
            _occupiedCells.Remove(pos);
        }

        /// <summary>
        /// 여러 셀 점유 해제
        /// </summary>
        public void ReleaseCells(IEnumerable<Vector2Int> positions)
        {
            foreach (var pos in positions)
            {
                _occupiedCells.Remove(pos);
            }
        }

        /// <summary>
        /// 셀이 점유되었는지 확인
        /// </summary>
        public bool IsCellOccupied(Vector2Int pos)
        {
            return _occupiedCells.Contains(pos);
        }

        /// <summary>
        /// 특정 셀들을 제외하고 점유 확인
        /// </summary>
        public bool IsCellOccupiedExcluding(Vector2Int pos, HashSet<Vector2Int> excludeCells)
        {
            if (excludeCells != null && excludeCells.Contains(pos))
                return false;
            return _occupiedCells.Contains(pos);
        }

        /// <summary>
        /// 모든 점유 해제
        /// </summary>
        public void ClearAllOccupied()
        {
            _occupiedCells.Clear();
        }

        /// <summary>
        /// 점유된 셀 목록 반환
        /// </summary>
        public HashSet<Vector2Int> GetOccupiedCells()
        {
            return new HashSet<Vector2Int>(_occupiedCells);
        }

        // ========== 디버그 ==========

        private void OnDrawGizmos()
        {
            if (!_drawGizmos || _gridSize <= 0) return;

            Gizmos.color = _gridColor;

            // 그리드 셀 그리기
            for (int x = 0; x < _gridSize; x++)
            {
                for (int y = 0; y < _gridSize; y++)
                {
                    Vector3 pos = GridToWorld(new Vector2Int(x, y));
                    Gizmos.DrawWireCube(pos, Vector3.one * _cellSize * 0.9f);
                }
            }

            // 점유된 셀 표시
            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            foreach (var cell in _occupiedCells)
            {
                Vector3 pos = GridToWorld(cell);
                Gizmos.DrawCube(pos, Vector3.one * _cellSize * 0.8f);
            }
        }
    }
}