using System.Collections.Generic;
using UnityEngine;
using BalloonOut.Core;
using BalloonOut.Data;
using BalloonOut.Game.Grid;

namespace BalloonOut.Game.Arrow
{
    /// <summary>
    /// 화살표 컨트롤러
    /// </summary>
    public class ArrowController : MonoBehaviour
    {
        // ========== 이벤트 ==========
        public System.Action<ArrowController> OnTapped;
        public System.Action<ArrowController> OnEscaped;

        // ========== 인스펙터 노출 변수 ==========
        [Header("Prefabs")]
        [SerializeField] private GameObject _cellPrefab;
        [SerializeField] private GameObject _headPrefab;

        [Header("Settings")]
        [SerializeField] private float _cellScale = 0.9f;

        // ========== 내부 상태 변수 ==========
        private int _id;
        private GameColor _color;
        private Direction _direction;
        private ArrowState _state = ArrowState.Idle;
        private List<Vector2Int> _cells = new List<Vector2Int>();
        private List<GameObject> _cellObjects = new List<GameObject>();
        private GameObject _headObject;

        // ========== 프로퍼티 ==========
        public int Id => _id;
        public GameColor Color => _color;
        public Direction Direction => _direction;
        public ArrowState State => _state;
        public List<Vector2Int> Cells => _cells;
        public Vector2Int HeadPosition => _cells.Count > 0 ? _cells[0] : Vector2Int.zero;

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

            CreateVisuals();
            UpdateVisuals();

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
        /// 셀 오브젝트 반환 (애니메이션용)
        /// </summary>
        public List<GameObject> GetCellObjects()
        {
            return _cellObjects;
        }

        /// <summary>
        /// Head 오브젝트 반환
        /// </summary>
        public GameObject GetHeadObject()
        {
            return _headObject;
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
        /// 비주얼 생성
        /// </summary>
        private void CreateVisuals()
        {
            ClearVisuals();

            if (GridSystem.Instance == null) return;

            Color unityColor = ColorHelper.GetColor(_color);
            float cellSize = GridSystem.Instance.CellSize;

            // 각 셀에 대해 오브젝트 생성
            for (int i = 0; i < _cells.Count; i++)
            {
                bool isHead = (i == 0);
                var prefab = isHead && _headPrefab != null ? _headPrefab : _cellPrefab;

                GameObject cellObj;
                if (prefab != null)
                {
                    cellObj = Instantiate(prefab, transform);
                }
                else
                {
                    // 프리팹이 없으면 기본 스프라이트 생성
                    cellObj = CreateDefaultCell(isHead);
                }

                cellObj.name = isHead ? "Head" : $"Body_{i}";

                // 색상 설정
                var sr = cellObj.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.color = unityColor;
                }

                // 스케일 설정
                cellObj.transform.localScale = Vector3.one * cellSize * _cellScale;

                if (isHead)
                {
                    _headObject = cellObj;
                    // Head 회전
                    float rotation = DirectionHelper.Rotation[_direction];
                    cellObj.transform.rotation = Quaternion.Euler(0, 0, rotation);
                }

                _cellObjects.Add(cellObj);
            }
        }

        /// <summary>
        /// 기본 셀 생성 (프리팹이 없을 때)
        /// </summary>
        private GameObject CreateDefaultCell(bool isHead)
        {
            var obj = new GameObject();
            var sr = obj.AddComponent<SpriteRenderer>();

            // 기본 사각형 스프라이트 생성
            Texture2D tex = new Texture2D(64, 64);
            Color[] colors = new Color[64 * 64];
            for (int i = 0; i < colors.Length; i++)
            {
                colors[i] = Color.white;
            }
            tex.SetPixels(colors);
            tex.Apply();

            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 64);
            sr.sortingOrder = isHead ? 2 : 1;

            // Head에는 방향 표시용 자식 추가
            if (isHead)
            {
                var arrowIndicator = new GameObject("ArrowIndicator");
                arrowIndicator.transform.SetParent(obj.transform);
                arrowIndicator.transform.localPosition = new Vector3(0, 0.3f, 0);
                arrowIndicator.transform.localScale = Vector3.one * 0.3f;

                var arrowSr = arrowIndicator.AddComponent<SpriteRenderer>();
                arrowSr.sprite = sr.sprite;
                arrowSr.color = Color.white;
                arrowSr.sortingOrder = 3;
            }

            // Collider 추가
            var collider = obj.AddComponent<BoxCollider2D>();
            collider.size = Vector2.one;

            return obj;
        }

        /// <summary>
        /// 비주얼 업데이트 (위치)
        /// </summary>
        private void UpdateVisuals()
        {
            if (GridSystem.Instance == null) return;

            for (int i = 0; i < _cells.Count && i < _cellObjects.Count; i++)
            {
                Vector3 worldPos = GridSystem.Instance.GridToWorld(_cells[i]);
                _cellObjects[i].transform.position = worldPos;
            }
        }

        /// <summary>
        /// 비주얼 정리
        /// </summary>
        private void ClearVisuals()
        {
            foreach (var obj in _cellObjects)
            {
                if (obj != null)
                {
                    Destroy(obj);
                }
            }
            _cellObjects.Clear();
            _headObject = null;
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
            ClearVisuals();
        }
    }
}