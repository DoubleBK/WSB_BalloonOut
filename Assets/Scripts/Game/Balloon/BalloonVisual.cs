using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BalloonOut.Core;
using BalloonOut.Data;

namespace BalloonOut.Game.Balloon
{
    /// <summary>
    /// 풍선 비주얼 컴포넌트
    /// 기믹에 따른 오버레이, 텍스트, 애니메이션 처리
    /// </summary>
    public class BalloonVisual : MonoBehaviour
    {
        // ========== 참조 ==========
        [SerializeField] private Image _baseImage;

        // ========== 동적 생성 요소 ==========
        private Image _overlayImage;
        private TextMeshProUGUI _overlayText;
        private RectTransform _rectTransform;

        // ========== 상태 ==========
        private GameColor _currentColor;
        private bool _isOverlayVisible;
        private Coroutine _animationCoroutine;

        // ========== 초기화 ==========

        private void Awake()
        {
            if (_baseImage == null)
            {
                _baseImage = GetComponent<Image>();
            }
            _rectTransform = GetComponent<RectTransform>();
        }

        /// <summary>
        /// 색상 설정
        /// </summary>
        public void SetColor(GameColor color)
        {
            _currentColor = color;
            if (_baseImage != null)
            {
                _baseImage.color = ColorHelper.GetColor(color);
            }
        }

        /// <summary>
        /// 현재 색상 가져오기
        /// </summary>
        public GameColor GetColor()
        {
            return _currentColor;
        }

        // ========== Marked 상태 (Connected 기믹용) ==========

        /// <summary>
        /// Marked 상태 설정 (Connected 기믹용)
        /// Marked 풍선은 검은색/회색으로 변경되고 체크마크 표시
        /// </summary>
        /// <param name="isMarked">Marked 상태 여부</param>
        /// <param name="def">기믹 정의 (null 가능)</param>
        public void SetMarkedState(bool isMarked, GimmickDefinitionSO def = null)
        {
            if (isMarked)
            {
                // Marked 상태 - 검은색/어두운 회색으로 변경
                if (_baseImage != null)
                {
                    _baseImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);
                }

                // 체크마크 오버레이 표시
                EnsureOverlayText();
                _overlayText.text = "✓";
                _overlayText.fontSize = def?.fontSize ?? 48f;
                _overlayText.color = Color.white;
                _overlayText.gameObject.SetActive(true);
            }
            else
            {
                // Normal 상태 - 원래 색상 복원
                if (_baseImage != null)
                {
                    _baseImage.color = ColorHelper.GetColor(_currentColor);
                }

                // 체크마크 숨기기 (다른 기믹 텍스트는 유지)
                // 단, Number 기믹 텍스트가 없을 때만 숨김
                if (_overlayText != null && _overlayText.text == "✓")
                {
                    _overlayText.gameObject.SetActive(false);
                }
            }
        }

        // ========== 오버레이 ==========

        /// <summary>
        /// 회색 오버레이 설정 (Surprise 기믹용)
        /// </summary>
        public void SetGrayOverlay(bool show, float alpha = 0.8f)
        {
            SetGrayOverlay(show, null, alpha);
        }

        /// <summary>
        /// 회색 오버레이 설정 (스프라이트 지정 가능)
        /// </summary>
        /// <param name="show">오버레이 표시 여부</param>
        /// <param name="overlaySprite">오버레이 스프라이트 (null이면 기본 이미지 색상만 변경)</param>
        /// <param name="alpha">오버레이 알파값</param>
        public void SetGrayOverlay(bool show, Sprite overlaySprite, float alpha = 0.8f)
        {
            _isOverlayVisible = show;

            if (show)
            {
                // 기본 이미지를 회색으로 변경
                if (_baseImage != null)
                {
                    _baseImage.color = new Color(0.5f, 0.5f, 0.5f, 1f);
                }

                // 오버레이 스프라이트가 있으면 오버레이 이미지 표시
                if (overlaySprite != null)
                {
                    EnsureOverlayImage();
                    _overlayImage.sprite = overlaySprite;
                    _overlayImage.color = new Color(0.5f, 0.5f, 0.5f, alpha);
                    _overlayImage.gameObject.SetActive(true);
                }
                else
                {
                    // 스프라이트 없으면 오버레이 이미지 숨김
                    if (_overlayImage != null)
                    {
                        _overlayImage.gameObject.SetActive(false);
                    }
                }
            }
            else
            {
                // 원래 색상으로 복원
                if (_baseImage != null)
                {
                    _baseImage.color = ColorHelper.GetColor(_currentColor);
                }

                // 오버레이 이미지 숨김
                if (_overlayImage != null)
                {
                    _overlayImage.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 오버레이 이미지 보장
        /// </summary>
        private void EnsureOverlayImage()
        {
            if (_overlayImage != null) return;

            var overlayObj = new GameObject("Overlay");
            overlayObj.transform.SetParent(transform, false);

            _overlayImage = overlayObj.AddComponent<Image>();
            _overlayImage.raycastTarget = false;

            var rect = overlayObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // ========== 텍스트 ==========

        /// <summary>
        /// 오버레이 텍스트 설정
        /// </summary>
        public void SetOverlayText(string text, float fontSize = 36f, Color? color = null)
        {
            if (string.IsNullOrEmpty(text))
            {
                if (_overlayText != null)
                {
                    _overlayText.gameObject.SetActive(false);
                }
                return;
            }

            EnsureOverlayText();
            _overlayText.text = text;
            _overlayText.fontSize = fontSize;
            _overlayText.color = color ?? Color.white;
            _overlayText.gameObject.SetActive(true);
        }

        /// <summary>
        /// 텍스트 설정 (GimmickDefinitionSO 기반)
        /// </summary>
        public void SetOverlayText(string text, GimmickDefinitionSO def)
        {
            if (def == null || !def.showText || string.IsNullOrEmpty(text))
            {
                if (_overlayText != null)
                {
                    _overlayText.gameObject.SetActive(false);
                }
                return;
            }

            EnsureOverlayText();
            _overlayText.text = text;
            _overlayText.fontSize = def.fontSize;
            _overlayText.color = def.textColor;

            if (def.font != null)
            {
                _overlayText.font = def.font;
            }

            _overlayText.outlineWidth = def.textOutlineWidth;
            _overlayText.outlineColor = def.textOutlineColor;
            _overlayText.gameObject.SetActive(true);
        }

        /// <summary>
        /// 오버레이 텍스트 숨기기
        /// </summary>
        public void HideOverlayText()
        {
            if (_overlayText != null)
            {
                _overlayText.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 오버레이 텍스트 보장
        /// </summary>
        private void EnsureOverlayText()
        {
            if (_overlayText != null) return;

            var textObj = new GameObject("OverlayText");
            textObj.transform.SetParent(transform, false);

            _overlayText = textObj.AddComponent<TextMeshProUGUI>();
            _overlayText.alignment = TextAlignmentOptions.Center;
            _overlayText.raycastTarget = false;
            _overlayText.enableWordWrapping = false;
            _overlayText.overflowMode = TextOverflowModes.Overflow;

            var rect = textObj.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // ========== 애니메이션 ==========

        /// <summary>
        /// 색상 공개 애니메이션 (Surprise 기믹용)
        /// </summary>
        public void PlayRevealAnimation(float duration = 0.3f, System.Action onComplete = null)
        {
            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
            }
            _animationCoroutine = StartCoroutine(RevealCoroutine(duration, onComplete));
        }

        private IEnumerator RevealCoroutine(float duration, System.Action onComplete)
        {
            Color startColor = new Color(0.5f, 0.5f, 0.5f, 1f);
            Color targetColor = ColorHelper.GetColor(_currentColor);
            Color? overlayStartColor = null;

            // 오버레이 이미지가 활성화되어 있으면 시작 색상 저장
            if (_overlayImage != null && _overlayImage.gameObject.activeSelf)
            {
                overlayStartColor = _overlayImage.color;
            }

            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // 기본 이미지 색상 전환
                if (_baseImage != null)
                {
                    _baseImage.color = Color.Lerp(startColor, targetColor, t);
                }

                // 오버레이 이미지 페이드아웃
                if (overlayStartColor.HasValue && _overlayImage != null)
                {
                    _overlayImage.color = new Color(
                        overlayStartColor.Value.r,
                        overlayStartColor.Value.g,
                        overlayStartColor.Value.b,
                        Mathf.Lerp(overlayStartColor.Value.a, 0f, t)
                    );
                }

                yield return null;
            }

            // 최종 상태 적용
            if (_baseImage != null)
            {
                _baseImage.color = targetColor;
            }

            // 오버레이 이미지 숨김
            if (_overlayImage != null)
            {
                _overlayImage.gameObject.SetActive(false);
            }

            // 텍스트 숨기기 (? 제거)
            HideOverlayText();

            _isOverlayVisible = false;
            onComplete?.Invoke();
        }

        /// <summary>
        /// 히트 애니메이션 (Number 기믹용)
        /// </summary>
        public void PlayHitAnimation(float duration = 0.2f)
        {
            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
            }
            _animationCoroutine = StartCoroutine(HitCoroutine(duration));
        }

        private IEnumerator HitCoroutine(float duration)
        {
            Vector3 originalScale = transform.localScale;
            Vector3 punchScale = originalScale * 1.3f;

            float elapsed = 0f;
            float halfDuration = duration * 0.5f;

            // 확대
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / halfDuration;
                transform.localScale = Vector3.Lerp(originalScale, punchScale, t);
                yield return null;
            }

            // 축소
            elapsed = 0f;
            while (elapsed < halfDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / halfDuration;
                transform.localScale = Vector3.Lerp(punchScale, originalScale, t);
                yield return null;
            }

            transform.localScale = originalScale;
        }

        /// <summary>
        /// 파티클 이펙트 스폰
        /// </summary>
        public void SpawnParticleEffect(ParticleSystem prefab, Vector3? position = null)
        {
            if (prefab == null) return;

            Vector3 spawnPos = position ?? transform.position;
            var particle = Instantiate(prefab, spawnPos, Quaternion.identity);

            // 색상 적용
            var main = particle.main;
            main.startColor = ColorHelper.GetColor(_currentColor);

            // 자동 파괴
            Destroy(particle.gameObject, main.duration + main.startLifetime.constantMax);
        }

        // ========== 정리 ==========

        private void OnDestroy()
        {
            if (_animationCoroutine != null)
            {
                StopCoroutine(_animationCoroutine);
            }
        }
    }
}
