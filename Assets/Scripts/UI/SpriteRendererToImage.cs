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
            if (_spriteRenderer != null && _image != null && _spriteRenderer.sprite != null)
            {
                _image.sprite = _spriteRenderer.sprite;
            }
        }
    }
}
