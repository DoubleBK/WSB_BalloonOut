using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;
using BalloonOut.Core;
using BalloonOut.Game.Grid;

namespace BalloonOut.Game.Arrow
{
    /// <summary>
    /// 화살표 시각적 렌더링 담당 (SpriteShape 또는 LineRenderer, Head 스프라이트, 색상)
    /// SpriteShapeController가 있으면 SpriteShape 사용, 없으면 LineRenderer 폴백
    /// </summary>
    public class ArrowVisualRenderer : MonoBehaviour
    {
        // ========== 인스펙터 노출 변수 ==========
        [Header("SpriteShape 참조 (선택)")]
        [SerializeField] private SpriteShapeController _shapeController;
        [SerializeField] private SpriteShapeRenderer _shapeRenderer;

        [Header("LineRenderer 폴백")]
        [SerializeField] private LineRenderer _lineRenderer;

        [Header("Head 스프라이트")]
        [SerializeField] private SpriteRenderer _headRenderer;

        [Header("외형 설정")]
        [SerializeField, Range(0.1f, 1f)] private float _lineWidth = 0.3f;
        [SerializeField, Range(0.1f, 0.5f)] private float _headTailOffset = 0.35f;
        [SerializeField, Range(0.1f, 2f)] private float _headScale = 1f;
        [SerializeField, Range(0, 20)] private int _numCapVertices = 10;
        [SerializeField, Range(0, 10)] private int _numCornerVertices = 5;

        // ========== 내부 상태 ==========
        private Color _currentColor = Color.white;
        private List<Vector3> _currentSplinePoints = new List<Vector3>();
        private bool _useSpriteShape = false;

        // ========== 프로퍼티 ==========
        public SpriteShapeController ShapeController => _shapeController;
        public SpriteShapeRenderer ShapeRenderer => _shapeRenderer;
        public LineRenderer LineRenderer => _lineRenderer;
        public SpriteRenderer HeadRenderer => _headRenderer;
        public float LineWidth => _lineWidth;
        public float HeadTailOffset => _headTailOffset;
        public bool UseSpriteShape => _useSpriteShape;

        // ========== 공개 인터페이스 ==========
        /// <summary>
        /// 초기 시각 설정
        /// </summary>
        public void Initialize(GameColor color, ArrowDirection headDirection)
        {
            Color unityColor = GetUnityColor(color);
            _currentColor = unityColor;

            // SpriteShapeController가 있으면 SpriteShape 사용
            if (_shapeController != null)
            {
                _useSpriteShape = true;
                SetupSpriteShape(unityColor);
            }
            else
            {
                _useSpriteShape = false;
                SetupLineRenderer(unityColor);
            }

            SetupHeadRenderer(unityColor, headDirection);
        }

        /// <summary>
        /// 시각 업데이트 (월드 좌표 기반)
        /// </summary>
        public void UpdateLineRenderer(List<Vector2> cellWorldPositions, Vector2Int moveDirection)
        {
            if (cellWorldPositions == null || cellWorldPositions.Count == 0)
                return;

            // Transform 위치를 첫 번째 셀로 설정
            transform.position = cellWorldPositions[0];

            // 오프셋 계산
            float cellSize = GridSystem.Instance != null ? GridSystem.Instance.CellSize : 1f;
            float offsetAmount = cellSize * _headTailOffset;

            // Head 방향
            Vector2 headOffsetVec = (Vector2)moveDirection * offsetAmount;

            // Tail 방향 계산
            Vector2 tailOffsetVec = CalculateTailOffset(cellWorldPositions, headOffsetVec, offsetAmount);

            if (_useSpriteShape)
            {
                SetSplinePositions(cellWorldPositions, headOffsetVec, tailOffsetVec);
            }
            else
            {
                SetLineRendererPositions(cellWorldPositions, headOffsetVec, tailOffsetVec);
            }

            // Head 스프라이트 위치 업데이트
            UpdateHeadPosition(cellWorldPositions, headOffsetVec);
        }

        /// <summary>
        /// 화살표 색상 설정
        /// </summary>
        public void SetColor(Color color)
        {
            _currentColor = color;

            if (_useSpriteShape && _shapeRenderer != null)
            {
                _shapeRenderer.color = color;
            }
            else if (_lineRenderer != null)
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
        /// 색상 설정 (GameColor)
        /// </summary>
        public void SetColor(GameColor gameColor)
        {
            SetColor(GetUnityColor(gameColor));
        }

        /// <summary>
        /// Head 회전 업데이트
        /// </summary>
        public void UpdateHeadRotation(ArrowDirection direction)
        {
            if (_headRenderer == null)
                return;

            float rotation = direction switch
            {
                ArrowDirection.Up => 0f,
                ArrowDirection.Down => 180f,
                ArrowDirection.Left => 90f,
                ArrowDirection.Right => -90f,
                _ => 0f
            };
            _headRenderer.transform.rotation = Quaternion.Euler(0, 0, rotation);
        }

        /// <summary>
        /// 렌더러 숨기기
        /// </summary>
        public void HideLineRenderer()
        {
            if (_useSpriteShape)
            {
                HideSpriteShape();
            }
            else
            {
                if (_lineRenderer != null)
                {
                    _lineRenderer.positionCount = 0;
                }
            }
        }

        /// <summary>
        /// SpriteShape 숨기기
        /// </summary>
        public void HideSpriteShape()
        {
            if (_shapeController != null)
            {
                var spline = _shapeController.spline;
                spline.Clear();
                spline.InsertPointAt(0, Vector3.zero);
                spline.InsertPointAt(1, Vector3.zero);
                _shapeController.BakeMesh();
            }
            if (_shapeRenderer != null)
            {
                _shapeRenderer.enabled = false;
            }
        }

        /// <summary>
        /// 렌더러 표시
        /// </summary>
        public void ShowSpriteShape()
        {
            if (_useSpriteShape && _shapeRenderer != null)
            {
                _shapeRenderer.enabled = true;
            }
        }

        /// <summary>
        /// Head 스프라이트 알파 설정
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

        /// <summary>
        /// 렌더러 알파 설정
        /// </summary>
        public void SetShapeAlpha(float alpha)
        {
            if (_useSpriteShape && _shapeRenderer != null)
            {
                var color = _shapeRenderer.color;
                color.a = alpha;
                _shapeRenderer.color = color;
            }
            else if (_lineRenderer != null)
            {
                Color newColor = _lineRenderer.startColor;
                newColor.a = alpha;
                _lineRenderer.startColor = newColor;
                _lineRenderer.endColor = newColor;
            }
        }

        /// <summary>
        /// 현재 Spline 포인트 목록 반환
        /// </summary>
        public List<Vector3> GetCurrentSplinePoints()
        {
            return new List<Vector3>(_currentSplinePoints);
        }

        /// <summary>
        /// Spline 포인트 직접 설정 (애니메이션용 - 단순 Linear 모드)
        /// </summary>
        public void SetSplinePointsDirect(List<Vector3> points)
        {
            if (_shapeController == null || points == null || points.Count < 2)
                return;

            var spline = _shapeController.spline;
            spline.Clear();

            // 애니메이션용: 단순히 Linear 모드로 모든 포인트 추가
            for (int i = 0; i < points.Count; i++)
            {
                spline.InsertPointAt(i, points[i]);
                spline.SetTangentMode(i, ShapeTangentMode.Linear);
                spline.SetHeight(i, _lineWidth);
            }

            _shapeController.splineDetail = 4;
            _shapeController.BakeMesh();

            _currentSplinePoints = new List<Vector3>(points);

            if (_shapeRenderer != null)
            {
                _shapeRenderer.enabled = true;
            }
        }

        /// <summary>
        /// GameColor → Unity Color 변환
        /// </summary>
        public static Color GetUnityColor(GameColor gameColor)
        {
            return gameColor switch
            {
                GameColor.Red => new Color(0.9f, 0.2f, 0.2f),
                GameColor.Blue => new Color(0.2f, 0.4f, 0.9f),
                GameColor.Green => new Color(0.2f, 0.8f, 0.3f),
                GameColor.Yellow => new Color(0.95f, 0.85f, 0.2f),
                GameColor.Purple => new Color(0.7f, 0.3f, 0.9f),
                GameColor.Orange => new Color(1f, 0.65f, 0f),
                GameColor.Cyan => new Color(0f, 0.9f, 0.9f),
                GameColor.Pink => new Color(1f, 0.75f, 0.8f),
                GameColor.Brown => new Color(0.55f, 0.27f, 0.07f),
                GameColor.Lime => new Color(0.2f, 0.8f, 0.2f),
                GameColor.Navy => new Color(0.1f, 0.1f, 0.5f),
                GameColor.Magenta => new Color(1f, 0f, 1f),
                _ => Color.white
            };
        }

        // ========== 내부 유틸리티 ==========

        private void SetupLineRenderer(Color color)
        {
            if (_lineRenderer == null)
            {
                _lineRenderer = GetComponent<LineRenderer>();
                if (_lineRenderer == null)
                {
                    _lineRenderer = gameObject.AddComponent<LineRenderer>();
                }
            }

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
            _lineRenderer.useWorldSpace = false;
            _lineRenderer.sortingOrder = 1;
        }

        private void SetLineRendererPositions(List<Vector2> positions, Vector2 headOffset, Vector2 tailOffset)
        {
            if (_lineRenderer == null)
                return;

            _lineRenderer.positionCount = positions.Count + 2;

            _lineRenderer.SetPosition(0, (Vector3)tailOffset);

            for (int i = 0; i < positions.Count; i++)
            {
                Vector3 localPos = positions[i] - positions[0];
                _lineRenderer.SetPosition(i + 1, localPos);
            }

            Vector3 lastCellLocal = positions[positions.Count - 1] - positions[0];
            _lineRenderer.SetPosition(positions.Count + 1, lastCellLocal + (Vector3)headOffset);

            _lineRenderer.startWidth = _lineWidth;
            _lineRenderer.endWidth = _lineWidth;
        }

        private void SetupSpriteShape(Color color)
        {
            if (_shapeRenderer == null)
            {
                _shapeRenderer = GetComponent<SpriteShapeRenderer>();
            }

            if (_shapeController != null)
            {
                _shapeController.splineDetail = 4;

                var spline = _shapeController.spline;
                spline.isOpenEnded = true;

                spline.Clear();
                spline.InsertPointAt(0, Vector3.zero);
                spline.InsertPointAt(1, Vector3.right * 0.1f);
                spline.SetTangentMode(0, ShapeTangentMode.Linear);
                spline.SetTangentMode(1, ShapeTangentMode.Linear);
                // 선 두께 적용
                spline.SetHeight(0, _lineWidth);
                spline.SetHeight(1, _lineWidth);
            }

            if (_shapeRenderer != null)
            {
                _shapeRenderer.color = color;
                _shapeRenderer.sortingOrder = 1;
            }
        }

        private void SetSplinePositions(List<Vector2> positions, Vector2 headOffset, Vector2 tailOffset)
        {
            if (_shapeController == null)
                return;

            var spline = _shapeController.spline;
            spline.Clear();

            _currentSplinePoints.Clear();

            int pointIndex = 0;

            // Tail 포인트
            Vector3 tailPoint = (Vector3)tailOffset;
            spline.InsertPointAt(pointIndex, tailPoint);
            spline.SetTangentMode(pointIndex, ShapeTangentMode.Linear);
            spline.SetHeight(pointIndex, _lineWidth);
            _currentSplinePoints.Add(tailPoint);
            pointIndex++;

            // 코너 오프셋
            float cornerOffset = 0.15f;

            for (int i = 0; i < positions.Count; i++)
            {
                Vector3 localPos = positions[i] - positions[0];
                bool isCorner = IsCornerPoint(positions, i);

                if (isCorner)
                {
                    // 코너: 이전 방향에서 오는 포인트
                    Vector2 dirBefore = (positions[i] - positions[i - 1]).normalized;
                    Vector3 beforeCorner = localPos - (Vector3)(dirBefore * cornerOffset);
                    spline.InsertPointAt(pointIndex, beforeCorner);
                    spline.SetTangentMode(pointIndex, ShapeTangentMode.Linear);
                    spline.SetHeight(pointIndex, _lineWidth);
                    _currentSplinePoints.Add(beforeCorner);
                    pointIndex++;

                    // 코너: 실제 코너 포인트 (중간 연결점)
                    spline.InsertPointAt(pointIndex, localPos);
                    spline.SetTangentMode(pointIndex, ShapeTangentMode.Linear);
                    spline.SetHeight(pointIndex, _lineWidth);
                    _currentSplinePoints.Add(localPos);
                    pointIndex++;

                    // 코너: 다음 방향으로 나가는 포인트
                    Vector2 dirAfter = (positions[i + 1] - positions[i]).normalized;
                    Vector3 afterCorner = localPos + (Vector3)(dirAfter * cornerOffset);
                    spline.InsertPointAt(pointIndex, afterCorner);
                    spline.SetTangentMode(pointIndex, ShapeTangentMode.Linear);
                    spline.SetHeight(pointIndex, _lineWidth);
                    _currentSplinePoints.Add(afterCorner);
                    pointIndex++;
                }
                else
                {
                    // 직선 구간
                    spline.InsertPointAt(pointIndex, localPos);
                    spline.SetTangentMode(pointIndex, ShapeTangentMode.Linear);
                    spline.SetHeight(pointIndex, _lineWidth);
                    _currentSplinePoints.Add(localPos);
                    pointIndex++;
                }
            }

            // Head 포인트
            Vector3 lastCellLocal = positions[positions.Count - 1] - positions[0];
            Vector3 headPoint = lastCellLocal + (Vector3)headOffset;
            spline.InsertPointAt(pointIndex, headPoint);
            spline.SetTangentMode(pointIndex, ShapeTangentMode.Linear);
            spline.SetHeight(pointIndex, _lineWidth);
            _currentSplinePoints.Add(headPoint);

            _shapeController.BakeMesh();

            if (_shapeRenderer != null)
            {
                _shapeRenderer.enabled = true;
            }
        }

        /// <summary>
        /// 해당 인덱스가 코너(방향 전환) 포인트인지 판별
        /// </summary>
        private bool IsCornerPoint(List<Vector2> positions, int index)
        {
            if (index <= 0 || index >= positions.Count - 1)
                return false;

            Vector2 dirBefore = (positions[index] - positions[index - 1]).normalized;
            Vector2 dirAfter = (positions[index + 1] - positions[index]).normalized;

            float dot = Vector2.Dot(dirBefore, dirAfter);
            return dot < 0.99f;
        }

        private void SetupHeadRenderer(Color color, ArrowDirection direction)
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
                }
            }

            _headRenderer.sprite = CreateDefaultArrowheadSprite();
            _headRenderer.color = color;
            _headRenderer.sortingOrder = 2;

            float cellSize = GridSystem.Instance != null ? GridSystem.Instance.CellSize : 1f;
            _headRenderer.transform.localScale = Vector3.one * _headScale * cellSize * 0.5f;

            UpdateHeadRotation(direction);
        }

        private Vector2 CalculateTailOffset(List<Vector2> positions, Vector2 headOffset, float offsetAmount)
        {
            if (positions.Count >= 2)
            {
                Vector2 tailDiff = positions[1] - positions[0];
                float tailDist = tailDiff.magnitude;

                if (tailDist > 0.01f)
                {
                    Vector2 tailToSecond = tailDiff / tailDist;
                    return -tailToSecond * offsetAmount;
                }
            }

            return -headOffset;
        }

        private void UpdateHeadPosition(List<Vector2> positions, Vector2 headOffset)
        {
            if (_headRenderer == null || positions.Count == 0)
                return;

            Vector3 lastCellLocal = positions[positions.Count - 1] - positions[0];
            _headRenderer.transform.localPosition = lastCellLocal + (Vector3)headOffset;
        }

        private Sprite CreateDefaultArrowheadSprite()
        {
            int size = 64;
            Texture2D tex = new Texture2D(size, size);
            Color[] pixels = new Color[size * size];

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.clear;
            }

            for (int y = 0; y < size; y++)
            {
                int halfWidth = (size - y) / 2;
                int startX = size / 2 - halfWidth;
                int endX = size / 2 + halfWidth;

                for (int x = startX; x < endX; x++)
                {
                    if (x >= 0 && x < size)
                    {
                        pixels[y * size + x] = Color.white;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
