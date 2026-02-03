using UnityEngine;
using UnityEngine.UI;

namespace BalloonOut.UI
{
    /// <summary>
    /// SpriteRenderer의 sprite를 Image로 매 프레임 복사하는 브릿지 컴포넌트.
    /// Animator가 SpriteRenderer.sprite를 애니메이션하면, 이 스크립트가 Image.sprite로 동기화.
    /// </summary>
    [RequireComponent(typeof(Image))]
    public class SpriteRendererToImage : MonoBehaviour
    {
        private SpriteRenderer _spriteRenderer;
        private Image _image;
        private Sprite _lastSprite;  // 캐싱용

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _image = GetComponent<Image>();

            // SpriteRenderer는 Canvas에서 보이지 않으므로 비활성화
            if (_spriteRenderer != null)
            {
                _spriteRenderer.enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (_spriteRenderer == null || _image == null) return;

            // 스프라이트가 변경된 경우에만 할당 (매 프레임 불필요한 할당 방지)
            if (_spriteRenderer.sprite != _lastSprite)
            {
                _lastSprite = _spriteRenderer.sprite;
                _image.sprite = _lastSprite;
            }
        }
    }
}
