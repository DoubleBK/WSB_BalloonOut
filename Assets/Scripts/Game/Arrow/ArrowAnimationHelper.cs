using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BalloonOut.Core;
using BalloonOut.Game.Grid;
using DG.Tweening;

namespace BalloonOut.Game.Arrow
{
    /// <summary>
    /// 화살표 애니메이션/연출 담당 (등장, 페이드 아웃, 실수 표시)
    /// </summary>
    public class ArrowAnimationHelper : MonoBehaviour
    {
        // ========== 인스펙터 노출 변수 ==========
        [Header("등장 연출")]
        [SerializeField] private float _appearSpeedPerCell = 0.03f;
        [SerializeField] private float _headFadeInDuration = 0.1f;

        [Header("실수 표시 (깜빡임)")]
        [SerializeField] private bool _enableMistakeVisual = true;
        [SerializeField] private float _blinkDuration = 0.3f;
        [SerializeField] private int _blinkCount = 3;
        [SerializeField] private Color _warningColor = new Color(1f, 0.2f, 0.2f, 0.5f);

        // ========== 내부 상태 변수 ==========
        private bool _isAppearing;
        private Coroutine _appearCoroutine;
        private bool _hasMadeMistake;
        private Sequence _blinkSequence;

        // ========== 참조 ==========
        private ArrowVisualRenderer _visualRenderer;

        // ========== 프로퍼티 ==========
        public bool IsAppearing => _isAppearing;
        public bool HasMadeMistake => _hasMadeMistake;

        // ========== 유니티 라이프사이클 ==========
        private void OnDestroy()
        {
            _blinkSequence?.Kill();
        }

        // ========== 공개 인터페이스 ==========
        public void Initialize(ArrowVisualRenderer visualRenderer)
        {
            _visualRenderer = visualRenderer;
        }

        public void PlayAppearAnimation(List<Vector2> cellWorldPositions, Vector2Int moveDirection,
            float headTailOffset, float delay = 0f, Action onComplete = null)
        {
            if (_appearCoroutine != null)
            {
                StopCoroutine(_appearCoroutine);
            }
            _appearCoroutine = StartCoroutine(AppearAnimationCoroutine(
                cellWorldPositions, moveDirection, headTailOffset, delay, onComplete));
        }

        public void HideImmediate()
        {
            _isAppearing = true;

            if (_visualRenderer != null)
            {
                _visualRenderer.HideLineRenderer();
                _visualRenderer.SetHeadAlpha(0f);
            }
        }

        public void ShowImmediate()
        {
            _isAppearing = false;

            if (_visualRenderer != null)
            {
                _visualRenderer.ShowSpriteShape();
                _visualRenderer.SetHeadAlpha(1f);
            }
        }

        public void StartFadeOutTransition(float lineWidth, float duration, Action onComplete)
        {
            StartCoroutine(FadeOutCoroutine(duration, onComplete));
        }

        public void ApplyMistakeVisual(GameColor color)
        {
            if (!_enableMistakeVisual || _visualRenderer == null)
                return;

            _hasMadeMistake = true;

            _blinkSequence?.Kill();

            Color originalColor = ArrowVisualRenderer.GetUnityColor(color);
            originalColor.a = 0.4f;

            _blinkSequence = DOTween.Sequence();

            for (int i = 0; i < _blinkCount; i++)
            {
                _blinkSequence.AppendCallback(() => _visualRenderer.SetColor(_warningColor));
                _blinkSequence.AppendInterval(_blinkDuration * 0.5f);
                _blinkSequence.AppendCallback(() => _visualRenderer.SetColor(originalColor));
                _blinkSequence.AppendInterval(_blinkDuration * 0.5f);
            }

            _blinkSequence.OnComplete(() => _visualRenderer.SetColor(originalColor));
        }

        // ========== 내부 유틸리티 ==========
        private IEnumerator AppearAnimationCoroutine(List<Vector2> cellWorldPositions,
            Vector2Int moveDirection, float headTailOffset, float delay, Action onComplete)
        {
            _isAppearing = true;

            if (delay > 0)
            {
                yield return new WaitForSeconds(delay);
            }

            if (_visualRenderer == null || cellWorldPositions == null || cellWorldPositions.Count == 0)
            {
                _isAppearing = false;
                onComplete?.Invoke();
                yield break;
            }

            var headRenderer = _visualRenderer.HeadRenderer;

            transform.position = cellWorldPositions[0];

            float cellSize = GridSystem.Instance != null ? GridSystem.Instance.CellSize : 1f;
            float offsetAmount = cellSize * headTailOffset;

            Vector2 tailOffsetVec = CalculateTailOffset(cellWorldPositions, moveDirection, offsetAmount);
            Vector2 headOffsetVec = (Vector2)moveDirection * offsetAmount;

            if (_visualRenderer.UseSpriteShape)
            {
                yield return StartCoroutine(AppearWithSpriteShape(
                    cellWorldPositions, tailOffsetVec, headOffsetVec, headRenderer));
            }
            else
            {
                yield return StartCoroutine(AppearWithLineRenderer(
                    cellWorldPositions, tailOffsetVec, headOffsetVec, headRenderer));
            }

            _isAppearing = false;
            _appearCoroutine = null;

            onComplete?.Invoke();
        }

        private IEnumerator AppearWithSpriteShape(List<Vector2> cellWorldPositions,
            Vector2 tailOffsetVec, Vector2 headOffsetVec, SpriteRenderer headRenderer)
        {
            List<Vector3> splinePoints = new List<Vector3>();

            splinePoints.Add((Vector3)tailOffsetVec);
            _visualRenderer.SetSplinePointsDirect(new List<Vector3> { splinePoints[0], splinePoints[0] + Vector3.right * 0.01f });

            yield return new WaitForSeconds(_appearSpeedPerCell);

            for (int i = 0; i < cellWorldPositions.Count; i++)
            {
                Vector3 localPos = cellWorldPositions[i] - cellWorldPositions[0];
                splinePoints.Add(localPos);

                _visualRenderer.SetSplinePointsDirect(splinePoints);

                yield return new WaitForSeconds(_appearSpeedPerCell);
            }

            Vector3 lastCellLocal = cellWorldPositions[cellWorldPositions.Count - 1] - cellWorldPositions[0];
            Vector3 headPoint = lastCellLocal + (Vector3)headOffsetVec;
            splinePoints.Add(headPoint);

            _visualRenderer.SetSplinePointsDirect(splinePoints);

            if (headRenderer != null)
            {
                headRenderer.transform.localPosition = headPoint;
                headRenderer.DOFade(1f, _headFadeInDuration);
            }

            yield return new WaitForSeconds(_headFadeInDuration);
        }

        private IEnumerator AppearWithLineRenderer(List<Vector2> cellWorldPositions,
            Vector2 tailOffsetVec, Vector2 headOffsetVec, SpriteRenderer headRenderer)
        {
            var lineRenderer = _visualRenderer.LineRenderer;
            if (lineRenderer == null)
                yield break;

            lineRenderer.positionCount = 1;
            lineRenderer.SetPosition(0, (Vector3)tailOffsetVec);

            yield return new WaitForSeconds(_appearSpeedPerCell);

            for (int i = 0; i < cellWorldPositions.Count; i++)
            {
                Vector3 localPos = cellWorldPositions[i] - cellWorldPositions[0];

                lineRenderer.positionCount = i + 2;
                lineRenderer.SetPosition(i + 1, localPos);

                yield return new WaitForSeconds(_appearSpeedPerCell);
            }

            Vector3 lastCellLocal = cellWorldPositions[cellWorldPositions.Count - 1] - cellWorldPositions[0];
            lineRenderer.positionCount = cellWorldPositions.Count + 2;
            lineRenderer.SetPosition(cellWorldPositions.Count + 1, lastCellLocal + (Vector3)headOffsetVec);

            if (headRenderer != null)
            {
                headRenderer.transform.localPosition = lastCellLocal + (Vector3)headOffsetVec;
                headRenderer.DOFade(1f, _headFadeInDuration);
            }

            yield return new WaitForSeconds(_headFadeInDuration);
        }

        private IEnumerator FadeOutCoroutine(float duration, Action onComplete)
        {
            if (_visualRenderer == null)
            {
                onComplete?.Invoke();
                yield break;
            }

            var headRenderer = _visualRenderer.HeadRenderer;
            float elapsed = 0f;

            if (_visualRenderer.UseSpriteShape)
            {
                var shapeRenderer = _visualRenderer.ShapeRenderer;
                if (shapeRenderer == null)
                {
                    onComplete?.Invoke();
                    yield break;
                }

                Color startColor = shapeRenderer.color;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;

                    Color newColor = startColor;
                    newColor.a = Mathf.Lerp(1f, 0f, t);
                    shapeRenderer.color = newColor;

                    if (headRenderer != null)
                    {
                        Color headColor = headRenderer.color;
                        headColor.a = Mathf.Lerp(1f, 0f, t);
                        headRenderer.color = headColor;
                    }

                    yield return null;
                }
            }
            else
            {
                var lineRenderer = _visualRenderer.LineRenderer;
                if (lineRenderer == null)
                {
                    onComplete?.Invoke();
                    yield break;
                }

                float startWidth = lineRenderer.startWidth;
                Color startColor = lineRenderer.startColor;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = elapsed / duration;

                    float newWidth = Mathf.Lerp(startWidth, 0f, t);
                    lineRenderer.startWidth = newWidth;
                    lineRenderer.endWidth = newWidth;

                    Color newColor = startColor;
                    newColor.a = Mathf.Lerp(1f, 0f, t);
                    lineRenderer.startColor = newColor;
                    lineRenderer.endColor = newColor;

                    if (headRenderer != null)
                    {
                        Color headColor = headRenderer.color;
                        headColor.a = Mathf.Lerp(1f, 0f, t);
                        headRenderer.color = headColor;
                    }

                    yield return null;
                }
            }

            onComplete?.Invoke();
        }

        private Vector2 CalculateTailOffset(List<Vector2> positions, Vector2Int moveDirection, float offsetAmount)
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

            return -(Vector2)moveDirection * offsetAmount;
        }
    }
}
