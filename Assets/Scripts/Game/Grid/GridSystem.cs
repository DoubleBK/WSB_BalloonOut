using System.Collections.Generic;
using UnityEngine;
using BalloonOut.Core;

namespace BalloonOut.Game.Grid
{
    /// <summary>
    /// 그리드 시스템 - 점(Dot) 기반 좌표 관리
    /// </summary>
    public class GridSystem : MonoBehaviour
    {
        // ========== 싱글톤 ==========
        private static GridSystem _instance;
        public static GridSystem Instance => _instance;

        // ========== 인스펙터 노출 변수 ==========
        [Header("그리드 설정")]
        [SerializeField] private int _gridWidth = 7;
        [SerializeField] private int _gridHeight = 9;
        [SerializeField] private float _cellSize = 1f;

        [Header("비주얼")]
        [SerializeField] private GameObject _dotPrefab;
        [SerializeField] private Color _dotColor = new Color(0.8f, 0.8f, 0.8f, 0.5f);

        [Header("탈출 판정")]
        [SerializeField, Tooltip("바운딩 박스 외부로 확장되는 패딩")]
        private int _boundingBoxPadding = 2;

        // ========== 내부 상태 변수 ==========
        private Vector2 _gridOrigin;
        private GameObject[,] _dots;
        private bool[,] _occupiedCells;
        private bool[,] _validCells;

        private Vector2Int _boundingMin;
        private Vector2Int _boundingMax;
        private bool _hasBoundingBox;

        // ========== 프로퍼티 ==========
        public int GridWidth => _gridWidth;
        public int GridHeight => _gridHeight;
        public float CellSize => _cellSize;
        public Vector2 GridOrigin => _gridOrigin;
        public Vector2Int BoundingMin => _boundingMin;
        public Vector2Int BoundingMax => _boundingMax;
        public bool HasBoundingBox => _hasBoundingBox;

        // ========== 유니티 라이프사이클 ==========
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            CalculateGridOrigin();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// 그리드 초기화
        /// </summary>
        public void Initialize(int width, int height)
        {
            _gridWidth = width;
            _gridHeight = height;
            _occupiedCells = new bool[width, height];
            _validCells = new bool[width, height];
            _hasBoundingBox = false;

            CalculateGridOrigin();
            ClearDotVisuals();
            _dots = new GameObject[width, height];

            SetFullGridAsBoundingBox();
        }

        /// <summary>
        /// 정사각형 그리드 초기화 (호환용)
        /// </summary>
        public void Initialize(int gridSize)
        {
            Initialize(gridSize, gridSize);
        }

        /// <summary>
        /// 특정 위치에 Dot 표시
        /// </summary>
        public void ShowDotAt(Vector2Int gridPos)
        {
            if (!IsValidPosition(gridPos) || _dotPrefab == null)
                return;

            if (_dots != null && _dots[gridPos.x, gridPos.y] != null)
                return;

            if (_dots == null)
                _dots = new GameObject[_gridWidth, _gridHeight];

            Vector2 worldPos = GridToWorld(gridPos);
            var dot = Instantiate(_dotPrefab, worldPos, Quaternion.identity, transform);
            dot.name = $"Dot_{gridPos.x}_{gridPos.y}";

            var sr = dot.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.color = _dotColor;
            }

            _dots[gridPos.x, gridPos.y] = dot;
        }

        /// <summary>
        /// 여러 위치에 Dot 표시
        /// </summary>
        public void ShowDotsAt(Vector2Int[] positions)
        {
            foreach (var pos in positions)
            {
                ShowDotAt(pos);
            }
        }

        /// <summary>
        /// 그리드 좌표를 월드 좌표로 변환
        /// </summary>
        public Vector2 GridToWorld(Vector2Int gridPos)
        {
            return new Vector2(
                _gridOrigin.x + gridPos.x * _cellSize,
                _gridOrigin.y + gridPos.y * _cellSize
            );
        }

        /// <summary>
        /// 월드 좌표를 그리드 좌표로 변환
        /// </summary>
        public Vector2Int WorldToGrid(Vector2 worldPos)
        {
            int x = Mathf.RoundToInt((worldPos.x - _gridOrigin.x) / _cellSize);
            int y = Mathf.RoundToInt((worldPos.y - _gridOrigin.y) / _cellSize);
            return new Vector2Int(x, y);
        }

        /// <summary>
        /// 그리드 좌표가 유효한지 확인
        /// </summary>
        public bool IsValidPosition(Vector2Int gridPos)
        {
            return gridPos.x >= 0 && gridPos.x < _gridWidth &&
                   gridPos.y >= 0 && gridPos.y < _gridHeight;
        }

        /// <summary>
        /// 그리드 경계 밖인지 확인
        /// </summary>
        public bool IsOutOfBounds(Vector2Int gridPos)
        {
            return gridPos.x < 0 || gridPos.x >= _gridWidth ||
                   gridPos.y < 0 || gridPos.y >= _gridHeight;
        }

        /// <summary>
        /// 월드 바운딩 박스 밖인지 확인 (탈출 판정)
        /// </summary>
        public bool IsOutOfWorldBounds(Vector2Int gridPos)
        {
            if (!_hasBoundingBox)
            {
                return gridPos.x < -_boundingBoxPadding || gridPos.x >= _gridWidth + _boundingBoxPadding ||
                       gridPos.y < -_boundingBoxPadding || gridPos.y >= _gridHeight + _boundingBoxPadding;
            }

            return gridPos.x < _boundingMin.x - _boundingBoxPadding || gridPos.x > _boundingMax.x + _boundingBoxPadding ||
                   gridPos.y < _boundingMin.y - _boundingBoxPadding || gridPos.y > _boundingMax.y + _boundingBoxPadding;
        }

        /// <summary>
        /// 유효 셀 목록으로 바운딩 박스 계산
        /// </summary>
        public void CalculateBoundingBox(Vector2Int[] validPositions)
        {
            if (validPositions == null || validPositions.Length == 0)
            {
                _hasBoundingBox = false;
                return;
            }

            _boundingMin = new Vector2Int(int.MaxValue, int.MaxValue);
            _boundingMax = new Vector2Int(int.MinValue, int.MinValue);

            foreach (var pos in validPositions)
            {
                _boundingMin.x = Mathf.Min(_boundingMin.x, pos.x);
                _boundingMin.y = Mathf.Min(_boundingMin.y, pos.y);
                _boundingMax.x = Mathf.Max(_boundingMax.x, pos.x);
                _boundingMax.y = Mathf.Max(_boundingMax.y, pos.y);

                SetValidCell(pos, true);
            }

            _hasBoundingBox = true;
        }

        /// <summary>
        /// 그리드 전체를 바운딩 박스로 설정
        /// </summary>
        public void SetFullGridAsBoundingBox()
        {
            _boundingMin = Vector2Int.zero;
            _boundingMax = new Vector2Int(_gridWidth - 1, _gridHeight - 1);
            _hasBoundingBox = true;

            _validCells = new bool[_gridWidth, _gridHeight];
            for (int x = 0; x < _gridWidth; x++)
            {
                for (int y = 0; y < _gridHeight; y++)
                {
                    _validCells[x, y] = true;
                }
            }
        }

        /// <summary>
        /// 유효 셀 설정
        /// </summary>
        public void SetValidCell(Vector2Int gridPos, bool valid)
        {
            if (IsValidPosition(gridPos))
            {
                if (_validCells == null)
                    _validCells = new bool[_gridWidth, _gridHeight];
                _validCells[gridPos.x, gridPos.y] = valid;
            }
        }

        /// <summary>
        /// 셀 점유 상태 설정
        /// </summary>
        public void SetOccupied(Vector2Int gridPos, bool occupied)
        {
            if (IsValidPosition(gridPos))
            {
                _occupiedCells[gridPos.x, gridPos.y] = occupied;
            }
        }

        /// <summary>
        /// 여러 셀 점유
        /// </summary>
        public void OccupyCells(IEnumerable<Vector2Int> positions)
        {
            foreach (var pos in positions)
            {
                SetOccupied(pos, true);
            }
        }

        /// <summary>
        /// 여러 셀 점유 해제
        /// </summary>
        public void ReleaseCells(IEnumerable<Vector2Int> positions)
        {
            foreach (var pos in positions)
            {
                SetOccupied(pos, false);
            }
        }

        /// <summary>
        /// 셀이 점유되었는지 확인
        /// </summary>
        public bool IsOccupied(Vector2Int gridPos)
        {
            if (!IsValidPosition(gridPos))
                return true;
            return _occupiedCells[gridPos.x, gridPos.y];
        }

        /// <summary>
        /// 특정 셀들을 제외하고 점유 확인
        /// </summary>
        public bool IsCellOccupiedExcluding(Vector2Int pos, HashSet<Vector2Int> excludeCells)
        {
            if (excludeCells != null && excludeCells.Contains(pos))
                return false;
            return IsOccupied(pos);
        }

        /// <summary>
        /// 모든 점유 상태 초기화
        /// </summary>
        public void ClearAllOccupied()
        {
            if (_occupiedCells != null)
            {
                for (int x = 0; x < _gridWidth; x++)
                {
                    for (int y = 0; y < _gridHeight; y++)
                    {
                        _occupiedCells[x, y] = false;
                    }
                }
            }
        }

        /// <summary>
        /// 방향에 따른 이동 벡터 반환
        /// </summary>
        public Vector2Int GetDirectionVector(ArrowDirection direction)
        {
            return DirectionHelper.Vectors[direction];
        }

        /// <summary>
        /// 모든 활성 Dot 객체 반환
        /// </summary>
        public Dictionary<Vector2Int, GameObject> GetAllDots()
        {
            var result = new Dictionary<Vector2Int, GameObject>();

            if (_dots == null)
                return result;

            for (int x = 0; x < _dots.GetLength(0); x++)
            {
                for (int y = 0; y < _dots.GetLength(1); y++)
                {
                    if (_dots[x, y] != null && _dots[x, y].activeInHierarchy)
                    {
                        result[new Vector2Int(x, y)] = _dots[x, y];
                    }
                }
            }

            return result;
        }

        // ========== 내부 유틸리티 ==========
        private void CalculateGridOrigin()
        {
            _gridOrigin = new Vector2(
                -(_gridWidth - 1) * _cellSize * 0.5f,
                -(_gridHeight - 1) * _cellSize * 0.5f
            );
        }

        private void ClearDotVisuals()
        {
            if (_dots != null)
            {
                for (int x = 0; x < _dots.GetLength(0); x++)
                {
                    for (int y = 0; y < _dots.GetLength(1); y++)
                    {
                        if (_dots[x, y] != null)
                        {
                            Destroy(_dots[x, y]);
                        }
                    }
                }
                _dots = null;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            CalculateGridOrigin();

            Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);

            for (int x = 0; x < _gridWidth; x++)
            {
                for (int y = 0; y < _gridHeight; y++)
                {
                    Vector2 pos = GridToWorld(new Vector2Int(x, y));
                    Gizmos.DrawWireSphere(pos, 0.1f);
                }
            }

            Gizmos.color = Color.yellow;
            Vector2 bottomLeft = GridToWorld(Vector2Int.zero) - Vector2.one * _cellSize * 0.5f;
            Vector2 topRight = GridToWorld(new Vector2Int(_gridWidth - 1, _gridHeight - 1)) + Vector2.one * _cellSize * 0.5f;
            Vector2 size = topRight - bottomLeft;
            Gizmos.DrawWireCube((bottomLeft + topRight) * 0.5f, size);
        }
#endif
    }
}