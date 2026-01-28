using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BalloonOut.Core;
using DG.Tweening;

namespace BalloonOut.UI
{
    /// <summary>
    /// 게임 화면 상단 UI - 레벨 표시, Exit/Retry 버튼 관리
    /// </summary>
    public class GameTopBarUI : MonoBehaviour
    {
        [Header("Level Text")]
        [SerializeField] private TextMeshProUGUI _levelText;

        [Header("Buttons")]
        [SerializeField] private Button _exitButton;
        [SerializeField] private Button _retryButton;

        [Header("Animation")]
        [SerializeField] private float _clickScale = 0.9f;
        [SerializeField] private float _clickDuration = 0.1f;

        private void Start()
        {
            _exitButton?.onClick.AddListener(OnExitClicked);
            _retryButton?.onClick.AddListener(OnRetryClicked);
            UpdateLevelText();
        }

        private void OnDestroy()
        {
            _exitButton?.onClick.RemoveListener(OnExitClicked);
            _retryButton?.onClick.RemoveListener(OnRetryClicked);
        }

        private void OnExitClicked()
        {
            PlayClickAnimation(_exitButton.transform, () =>
            {
                GameManager.Instance.GoToLobby();
            });
        }

        private void OnRetryClicked()
        {
            PlayClickAnimation(_retryButton.transform, () =>
            {
                GameManager.Instance.RestartLevel();
            });
        }

        private void UpdateLevelText()
        {
            if (_levelText == null) return;

            int level = GameManager.Instance != null ? GameManager.Instance.CurrentLevelIdx : 1;
            _levelText.text = $"Level {level}";
        }

        private void PlayClickAnimation(Transform target, TweenCallback onComplete)
        {
            target.DOScale(_clickScale, _clickDuration)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    target.DOScale(1f, _clickDuration)
                        .SetEase(Ease.OutQuad)
                        .OnComplete(onComplete);
                });
        }
    }
}