using System.Collections.Generic;
using UnityEngine;
using BalloonOut.Core;
using BalloonOut.Game.Grid;

namespace BalloonOut.Game.Arrow
{
    /// <summary>
    /// 화살표 비주얼 렌더러 (ArrowPopBall 방식 복사)
    /// cells[0] = TAIL, cells[last] = HEAD
    /// transform.position = TAIL 위치 (첫 번째 셀)
    /// LineRenderer는 로컬 좌표 사용
    /// </summary>
    public class ArrowVisualRenderer : MonoBehaviour
    {
        // ========== 인스펙터 노출 변수 ==========
        [Header("필수 참조")]
        [SerializeField] private LineRenderer _lineRenderer;
        [SerializeField] private SpriteRenderer _headRenderer;

        [Header("외형 설정")]
        [SerializeField, Range(0.1f, 1f)] private float _lineWidth = 0.3f;
        [SerializeField, Range(0, 20)] private int _numCapVertices = 10;
        [SerializeField, Range(0, 10)] private int _numCornerVertices = 5;
        [SerializeField, Range(0.1f, 0.5f)] private float _headTailOffset = 0.35f;
        [SerializeField, Range(0.1f, 2f)] private float _headScale = 1f;

        // ========== 프로퍼티 ==========
        public LineRenderer LineRenderer => _lineRenderer;
        public SpriteRenderer HeadRenderer => _headRenderer;
        public float LineWidth => _lineWidth;
        public float HeadTailOffset => _headTailOffset;

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// 초기 시각 설정
        /// </summary>
        public void Initialize(GameColor color, Direction direction)
        {
            UnityEngine.Color unityColor = ColorHelper.GetColor(color);
            SetupLineRenderer(unityColor);
            SetupHeadRenderer(unityColor, direction);
        }

        /// <summary>
        /// LineRenderer 업데이트 (월드 좌표 기반)
        /// positions[0] = TAIL, positions[last] = HEAD
        /// </summary>
        public void UpdateLineRenderer(List<Vector3> cellWorldPositions, Vector2Int moveDirection)
        {
            if (_lineRenderer == null || cellWorldPositions == null || cellWorldPositions.Count == 0)
                return;

            // Transform 위치를 첫 번째 셀(TAIL)로 설정
            transform.position = cellWorldPositions[0];

            // 오프셋 계산
            float cellSize = GridSystem.Instance != null ? GridSystem.Instance.CellSize : 1f;
            float offsetAmount = cellSize * _headTailOffset;

            // Head 방향 (이동 방향)
            Vector2 headOffsetVec = new Vector2(moveDirection.x, moveDirection.y) * offsetAmount;

            // Tail 방향 계산 (Head 반대 방향)
            Vector2 tailOffsetVec = CalculateTailOffset(cellWorldPositions, headOffsetVec, offsetAmount);

            // LineRenderer 포인트 설정 (로컬 좌표)
            SetLineRendererPositions(cellWorldPositions, headOffsetVec, tailOffsetVec);

            // Head 스프라이트 위치 업데이트
            UpdateHeadPosition(cellWorldPositions, headOffsetVec);
        }

        /// <summary>
        /// 색상 설정
        /// </summary>
        public void SetColor(UnityEngine.Color color)
        {
            if (_lineRenderer != null)
            {
                _lineRenderer.startColor = color;
                _lineRenderer.endColor = color;
            }
            if (_headRenderer != null)
            {
                _headRenderer.color = color;
            }
        }

        /// <summary>
        /// Head 회전 업데이트
        /// </summary>
        public void UpdateHeadRotation(Direction direction)
        {
            if (_headRenderer == null)
                return;

            float rotation = direction switch
            {
                Direction.U => 0f,
                Direction.D => 180f,
                Direction.L => 90f,
                Direction.R => -90f,
                _ => 0f
            };
            _headRenderer.transform.rotation = Quaternion.Euler(0, 0, rotation);
        }

        /// <summary>
        /// LineRenderer 숨기기
        /// </summary>
        public void HideLine()
        {
            if (_lineRenderer != null)
            {
                _lineRenderer.positionCount = 0;
            }
        }

        /// <summary>
        /// Head 숨기기
        /// </summary>
        public void HideHead()
        {
            if (_headRenderer != null)
            {
                _headRenderer.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Head 알파 설정
        /// </summary>
        public void SetHeadAlpha(float alpha)
        {
            if (_headRenderer != null)
            {
                var color = _headRenderer.color;
                color.a = alpha;
                _headRenderer.color = color;
            }
        }

        // ========== 내부 유틸리티 ==========

        private void SetupLineRenderer(UnityEngine.Color color)
        {
            if (_lineRenderer == null)
            {
                _lineRenderer = GetComponent<LineRenderer>();
                if (_lineRenderer == null)
                {
                    _lineRenderer = gameObject.AddComponent<LineRenderer>();
                }
            }

            // 기본 머티리얼 설정
            if (_lineRenderer.material == null || _lineRenderer.material.shader.name == "Hidden/InternalErrorShader")
            {
                _lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            }

            _lineRenderer.startColor = color;
            _lineRenderer.endColor = color;
            _lineRenderer.startWidth = _lineWidth;
            _lineRenderer.endWidth = _lineWidth;
            _lineRenderer.numCapVertices = _numCapVertices;
            _lineRenderer.numCornerVertices = _numCornerVertices;
            _lineRenderer.textureMode = LineTextureMode.Tile;

            // 로컬 좌표 사용 (ArrowPopBall 방식)
            _lineRenderer.useWorldSpace = false;
            _lineRenderer.sortingOrder = 1;
        }

        private void SetupHeadRenderer(UnityEngine.Color color, Direction direction)
        {
            if (_headRenderer == null)
            {
                var headObj = transform.Find("Head");
                if (headObj == null)
                {
                    headObj = new GameObject("Head").transform;
                    headObj.SetParent(transform);
                    headObj.localPosition = Vector3.zero;
                }

                _headRenderer = headObj.GetComponent<SpriteRenderer>();
                if (_headRenderer == null)
                {
                    _headRenderer = headObj.gameObject.AddComponent<SpriteRenderer>();
                    _headRenderer.sprite = CreateDefaultArrowheadSprite();
                }
            }

            _headRenderer.color = color;
            _headRenderer.sortingOrder = 2;

            float cellSize = GridSystem.Instance != null ? GridSystem.Instance.CellSize : 1f;
            _headRenderer.transform.localScale = Vector3.one * _headScale * cellSize * 0.5f;

            UpdateHeadRotation(direction);
        }

        /// <summary>
        /// Tail 오프셋 계산 (TAIL에서 두 번째 셀 방향의 반대)
        /// </summary>
        private Vector2 CalculateTailOffset(List<Vector3> positions, Vector2 headOffset, float offsetAmount)
        {
            if (positions.Count >= 2)
            {
                // TAIL(positions[0]) → 두 번째 셀(positions[1]) 방향 계산
                Vector2 tailDiff = (Vector2)(positions[1] - positions[0]);
                float tailDist = tailDiff.magnitude;

                if (tailDist > 0.01f)
                {
                    Vector2 tailToSecond = tailDiff / tailDist;
                    // Tail 돌출은 반대 방향
                    return -tailToSecond * offsetAmount;
                }
            }

            // Fallback: Head 방향의 반대
            return -headOffset;
        }

        /// <summary>
        /// LineRenderer 포인트 설정 (ArrowPopBall 방식 - 로컬 좌표)
        /// </summary>
        private void SetLineRendererPositions(List<Vector3> positions, Vector2 headOffset, Vector2 tailOffset)
        {
            _lineRenderer.positionCount = positions.Count + 2;

            // [0] Tail 돌출점 (로컬 좌표)
            _lineRenderer.SetPosition(0, (Vector3)tailOffset);

            // [1 ~ N] 셀 포인트들 (로컬 좌표: 첫 셀 기준)
            for (int i = 0; i < positions.Count; i++)
            {
                Vector3 localPos = positions[i] - positions[0];
                _lineRenderer.SetPosition(i + 1, localPos);
            }

            // [N+1] Head 돌출점 (로컬 좌표)
            Vector3 lastCellLocal = positions[positions.Count - 1] - positions[0];
            _lineRenderer.SetPosition(positions.Count + 1, lastCellLocal + (Vector3)headOffset);

            // 두께 유지
            _lineRenderer.startWidth = _lineWidth;
            _lineRenderer.endWidth = _lineWidth;
        }

        /// <summary>
        /// Head 스프라이트 위치 업데이트 (로컬 좌표)
        /// </summary>
        private void UpdateHeadPosition(List<Vector3> positions, Vector2 headOffset)
        {
            if (_headRenderer == null || positions.Count == 0)
                return;

            // HEAD는 마지막 셀 (positions[last])
            Vector3 lastCellLocal = positions[positions.Count - 1] - positions[0];
            _headRenderer.transform.localPosition = lastCellLocal + (Vector3)headOffset;
        }

        /// <summary>
        /// 기본 화살촉 스프라이트 생성 (삼각형)
        /// </summary>
        private Sprite CreateDefaultArrowheadSprite()
        {
            int size = 64;
            Texture2D tex = new Texture2D(size, size);
            UnityEngine.Color[] pixels = new UnityEngine.Color[size * size];

            // 투명으로 초기화
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = UnityEngine.Color.clear;
            }

            // 삼각형 그리기 (위쪽을 향하는 화살촉)
            for (int y = 0; y < size; y++)
            {
                int halfWidth = (size - y) / 2;
                int startX = size / 2 - halfWidth;
                int endX = size / 2 + halfWidth;

                for (int x = startX; x < endX; x++)
                {
                    if (x >= 0 && x < size)
                    {
                        pixels[y * size + x] = UnityEngine.Color.white;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
