using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;
using DG.Tweening;
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
        [SerializeField, Range(0, 10)] private int _numCornerVertices = 5;  // 5 = 부드러운 코너

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

        // ========== 힌트 하이라이트 ==========
        private Color _originalColor;
        private bool _isHighlighted;
        private Tween _pulseTween;

        /// <summary>
        /// 하이라이트 설정 (Hint용)
        /// </summary>
        public void SetHighlight(bool highlight)
        {
            if (_isHighlighted == highlight) return;
            _isHighlighted = highlight;

            if (highlight)
            {
                // 원본 색상 저장
                _originalColor = _currentColor;

                // 밝은 흰색으로 하이라이트
                Color highlightColor = Color.Lerp(_currentColor, Color.white, 0.5f);
                ApplyHighlightColor(highlightColor);
            }
            else
            {
                // 원본 색상 복원
                ApplyHighlightColor(_originalColor);
            }
        }

        /// <summary>
        /// 색상 펄스 애니메이션 시작
        /// </summary>
        public void StartPulseAnimation()
        {
            StopPulseAnimation();

            // 밝기 0.3 ~ 1.0 사이로 펄스
            float brightness = 0.5f;
            _pulseTween = DOTween.To(
                () => brightness,
                x =>
                {
                    brightness = x;
                    ApplyBrightness(x);
                },
                1f,
                0.5f)
                .SetLoops(-1, LoopType.Yoyo)
                .SetEase(Ease.InOutSine);
        }

        /// <summary>
        /// 색상 펄스 애니메이션 중지
        /// </summary>
        public void StopPulseAnimation()
        {
            _pulseTween?.Kill();
            _pulseTween = null;
        }

        /// <summary>
        /// 밝기 적용 (0.3 = 어둡게, 1.0 = 원본)
        /// </summary>
        private void ApplyBrightness(float brightness)
        {
            Color targetColor = Color.Lerp(_originalColor * 0.5f, Color.Lerp(_originalColor, Color.white, 0.5f), brightness);
            ApplyHighlightColor(targetColor);
        }

        private void ApplyHighlightColor(Color color)
        {
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
        /// GameColor → Unity Color 변환 (HDR NEON 색상)
        /// </summary>
        public static Color GetUnityColor(GameColor gameColor)
        {
            // HDR 강도 적용 (NEON 효과)
            const float neonIntensity = 2.0f;

            return gameColor switch
            {
                GameColor.Red => new Color(0.9f * neonIntensity, 0.2f * neonIntensity, 0.2f * neonIntensity),
                GameColor.Blue => new Color(0.2f * neonIntensity, 0.4f * neonIntensity, 0.9f * neonIntensity),
                GameColor.Green => new Color(0.2f * neonIntensity, 0.8f * neonIntensity, 0.3f * neonIntensity),
                GameColor.Yellow => new Color(0.95f * neonIntensity, 0.85f * neonIntensity, 0.2f * neonIntensity),
                GameColor.Purple => new Color(0.7f * neonIntensity, 0.3f * neonIntensity, 0.9f * neonIntensity),
                GameColor.Orange => new Color(1f * neonIntensity, 0.65f * neonIntensity, 0f),
                GameColor.Cyan => new Color(0f, 0.9f * neonIntensity, 0.9f * neonIntensity),
                GameColor.Pink => new Color(1f * neonIntensity, 0.75f * neonIntensity, 0.8f * neonIntensity),
                GameColor.Brown => new Color(0.55f * 1.5f, 0.27f * 1.5f, 0.07f * 1.5f),  // 갈색은 약하게
                GameColor.Lime => new Color(0.2f * neonIntensity, 0.8f * neonIntensity, 0.2f * neonIntensity),
                GameColor.Navy => new Color(0.1f * neonIntensity, 0.1f * neonIntensity, 0.5f * neonIntensity),
                GameColor.Magenta => new Color(1f * neonIntensity, 0f, 1f * neonIntensity),
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

            // ArrowPopBall 방식: 단순하게 셀 위치 + tail/head 오프셋만 사용
            // numCornerVertices가 부드러운 코너 처리를 담당
            _lineRenderer.positionCount = positions.Count + 2;

            // Tail 돌출점
            _lineRenderer.SetPosition(0, (Vector3)tailOffset);

            // 셀 포인트들 (로컬 좌표)
            for (int i = 0; i < positions.Count; i++)
            {
                Vector3 localPos = positions[i] - positions[0];
                _lineRenderer.SetPosition(i + 1, localPos);
            }

            // Head 돌출점
            Vector3 lastCellLocal = positions[positions.Count - 1] - positions[0];
            _lineRenderer.SetPosition(positions.Count + 1, lastCellLocal + (Vector3)headOffset);

            // 두께 유지
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

            // 단순하게 모든 셀 포인트 추가 (ArrowPopBall 방식)
            for (int i = 0; i < positions.Count; i++)
            {
                Vector3 localPos = positions[i] - positions[0];
                spline.InsertPointAt(pointIndex, localPos);
                spline.SetTangentMode(pointIndex, ShapeTangentMode.Linear);
                spline.SetHeight(pointIndex, _lineWidth);
                _currentSplinePoints.Add(localPos);
                pointIndex++;
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
