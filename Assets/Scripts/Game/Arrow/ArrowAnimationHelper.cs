using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using BalloonOut.Core;
using BalloonOut.Game.Grid;

namespace BalloonOut.Game.Arrow
{
    /// <summary>
    /// 화살표 애니메이션 헬퍼 (등장/페이드 연출)
    /// </summary>
    public class ArrowAnimationHelper : MonoBehaviour
    {
        // ========== 인스펙터 노출 변수 ==========
        [Header("Appear Animation")]
        [SerializeField] private float _appearSpeedPerCell = 0.03f;
        [SerializeField] private float _headFadeInDuration = 0.1f;

        [Header("Blink Animation")]
        [SerializeField] private float _blinkDuration = 0.3f;
        [SerializeField] private int _blinkCount = 3;

        // ========== 내부 상태 ==========
        private ArrowVisualRenderer _visualRenderer;
        private bool _isAnimating = false;

        // ========== 프로퍼티 ==========
        public bool IsAnimating => _isAnimating;

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// 초기화
        /// </summary>
        public void Initialize(ArrowVisualRenderer renderer)
        {
            _visualRenderer = renderer;
        }

        /// <summary>
        /// 등장 애니메이션 재생
        /// </summary>
        public IEnumerator PlayAppearAnimation(List<Vector3> cellWorldPositions, Vector2Int moveDirection)
        {
            if (_visualRenderer == null || cellWorldPositions == null || cellWorldPositions.Count < 2)
                yield break;

            _isAnimating = true;

            var lineRenderer = _visualRenderer.LineRenderer;
            var headRenderer = _visualRenderer.HeadRenderer;

            // Head 숨기기
            if (headRenderer != null)
            {
                headRenderer.color = new UnityEngine.Color(headRenderer.color.r, headRenderer.color.g, headRenderer.color.b, 0f);
            }

            float cellSize = GridSystem.Instance != null ? GridSystem.Instance.CellSize : 1f;
            float offsetAmount = cellSize * 0.35f;

            // Transform 위치 설정
            transform.position = cellWorldPositions[0];

            // 오프셋 계산
            Vector2 headOffsetVec = new Vector2(moveDirection.x, moveDirection.y) * offsetAmount;
            Vector2 tailOffsetVec = CalculateTailOffset(cellWorldPositions, offsetAmount);

            Vector3 origin = cellWorldPositions[0];
            int cellCount = cellWorldPositions.Count;

            // 초기: Tail 돌출점만
            lineRenderer.positionCount = 1;
            Vector3 tailLocal = cellWorldPositions[cellCount - 1] - origin;
            lineRenderer.SetPosition(0, tailLocal + (Vector3)tailOffsetVec);

            // 각 셀 순차 추가 (TAIL → HEAD)
            for (int i = 0; i < cellCount; i++)
            {
                lineRenderer.positionCount = i + 2;
                Vector3 localPos = cellWorldPositions[cellCount - 1 - i] - origin;
                lineRenderer.SetPosition(i + 1, localPos);

                yield return new WaitForSeconds(_appearSpeedPerCell);
            }

            // Head 돌출점 추가
            lineRenderer.positionCount = cellCount + 2;
            lineRenderer.SetPosition(cellCount + 1, (Vector3)headOffsetVec);

            // Head 스프라이트 페이드인
            if (headRenderer != null)
            {
                headRenderer.transform.localPosition = (Vector3)headOffsetVec;
                headRenderer.DOFade(1f, _headFadeInDuration);
            }

            yield return new WaitForSeconds(_headFadeInDuration);

            _isAnimating = false;
        }

        /// <summary>
        /// 깜빡임 애니메이션 (충돌/실패 시)
        /// </summary>
        public void PlayBlinkAnimation(System.Action onComplete = null)
        {
            if (_visualRenderer == null)
            {
                onComplete?.Invoke();
                return;
            }

            _isAnimating = true;

            var lineRenderer = _visualRenderer.LineRenderer;
            var headRenderer = _visualRenderer.HeadRenderer;

            // 원래 색상 저장
            UnityEngine.Color originalLineColor = lineRenderer.startColor;
            UnityEngine.Color originalHeadColor = headRenderer != null ? headRenderer.color : UnityEngine.Color.white;

            Sequence blinkSeq = DOTween.Sequence();

            float halfBlink = _blinkDuration / (_blinkCount * 2);

            for (int i = 0; i < _blinkCount; i++)
            {
                // 페이드 아웃
                blinkSeq.Append(DOTween.To(
                    () => lineRenderer.startColor.a,
                    (a) =>
                    {
                        var c = originalLineColor;
                        c.a = a;
                        lineRenderer.startColor = c;
                        lineRenderer.endColor = c;
                        if (headRenderer != null)
                        {
                            var hc = originalHeadColor;
                            hc.a = a;
                            headRenderer.color = hc;
                        }
                    },
                    0.3f,
                    halfBlink
                ));

                // 페이드 인
                blinkSeq.Append(DOTween.To(
                    () => lineRenderer.startColor.a,
                    (a) =>
                    {
                        var c = originalLineColor;
                        c.a = a;
                        lineRenderer.startColor = c;
                        lineRenderer.endColor = c;
                        if (headRenderer != null)
                        {
                            var hc = originalHeadColor;
                            hc.a = a;
                            headRenderer.color = hc;
                        }
                    },
                    1f,
                    halfBlink
                ));
            }

            blinkSeq.OnComplete(() =>
            {
                // 원래 색상 복원
                lineRenderer.startColor = originalLineColor;
                lineRenderer.endColor = originalLineColor;
                if (headRenderer != null)
                {
                    headRenderer.color = originalHeadColor;
                }

                _isAnimating = false;
                onComplete?.Invoke();
            });
        }

        /// <summary>
        /// 페이드 아웃 애니메이션
        /// </summary>
        public void PlayFadeOutAnimation(float duration, System.Action onComplete = null)
        {
            if (_visualRenderer == null)
            {
                onComplete?.Invoke();
                return;
            }

            _isAnimating = true;

            var lineRenderer = _visualRenderer.LineRenderer;
            var headRenderer = _visualRenderer.HeadRenderer;

            UnityEngine.Color originalLineColor = lineRenderer.startColor;
            UnityEngine.Color originalHeadColor = headRenderer != null ? headRenderer.color : UnityEngine.Color.white;

            DOTween.To(
                () => lineRenderer.startColor.a,
                (a) =>
                {
                    var c = originalLineColor;
                    c.a = a;
                    lineRenderer.startColor = c;
                    lineRenderer.endColor = c;
                    if (headRenderer != null)
                    {
                        var hc = originalHeadColor;
                        hc.a = a;
                        headRenderer.color = hc;
                    }
                },
                0f,
                duration
            ).OnComplete(() =>
            {
                _isAnimating = false;
                onComplete?.Invoke();
            });
        }

        // ========== 내부 유틸리티 ==========

        /// <summary>
        /// Tail 오프셋 계산
        /// </summary>
        private Vector2 CalculateTailOffset(List<Vector3> positions, float offsetAmount)
        {
            int lastIdx = positions.Count - 1;

            if (positions.Count >= 2)
            {
                Vector2 tailToSecond = positions[lastIdx - 1] - positions[lastIdx];
                float dist = tailToSecond.magnitude;

                if (dist > 0.01f)
                {
                    Vector2 tailDir = tailToSecond / dist;
                    return -tailDir * offsetAmount;
                }
            }

            return Vector2.down * offsetAmount;
        }
    }
}
