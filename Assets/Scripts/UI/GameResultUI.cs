using UnityEngine;
using UnityEngine.UI;
using TMPro;
using BalloonOut.Core;

namespace BalloonOut.UI
{
    /// <summary>
    /// 게임 실패 UI
    /// </summary>
    public class GameResultUI : MonoBehaviour
    {
        // ========== 인스펙터 노출 변수 ==========
        [Header("Panel")]
        [SerializeField] private GameObject _failedPanel;

        [Header("Dim Background")]
        [SerializeField] private Image _dimBackground;

        [Header("Buttons")]
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _menuButton;
        [SerializeField] private Button _playOnButton;

        [Header("Texts (Optional)")]
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _messageText;

        [Header("Settings")]
        [SerializeField] private string _failedTitle = "FAILED";
        [SerializeField] private string _failedMessage = "Out of Arrows!";

        // ========== 유니티 라이프사이클 ==========
        private void Start()
        {
            // 버튼 이벤트 연결
            if (_restartButton != null)
            {
                _restartButton.onClick.AddListener(OnRestartClicked);
            }

            if (_menuButton != null)
            {
                _menuButton.onClick.AddListener(OnMenuClicked);
            }

            if (_playOnButton != null)
            {
                _playOnButton.onClick.AddListener(OnPlayOnClicked);
            }

            // GameManager 이벤트 구독
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnLevelFailed += OnLevelFailed;
                GameManager.Instance.OnGameStateChanged += OnGameStateChanged;
            }

            // 초기 상태: 패널 숨김
            Hide();
        }

        private void OnDestroy()
        {
            // 이벤트 구독 해제
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnLevelFailed -= OnLevelFailed;
                GameManager.Instance.OnGameStateChanged -= OnGameStateChanged;
            }

            // 버튼 이벤트 해제
            if (_restartButton != null)
            {
                _restartButton.onClick.RemoveListener(OnRestartClicked);
            }

            if (_menuButton != null)
            {
                _menuButton.onClick.RemoveListener(OnMenuClicked);
            }

            if (_playOnButton != null)
            {
                _playOnButton.onClick.RemoveListener(OnPlayOnClicked);
            }
        }

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// 실패 화면 표시
        /// </summary>
        public void Show()
        {
            // Dim 배경 활성화
            if (_dimBackground != null)
            {
                _dimBackground.gameObject.SetActive(true);
            }

            // 카메라 드래그 비활성화
            if (CameraController.Instance != null)
            {
                CameraController.Instance.SetInputEnabled(false);
            }

            if (_failedPanel != null)
            {
                _failedPanel.SetActive(true);
            }

            // 텍스트 업데이트
            if (_titleText != null)
            {
                _titleText.text = _failedTitle;
            }

            if (_messageText != null)
            {
                _messageText.text = _failedMessage;
            }
        }

        /// <summary>
        /// 패널 숨기기
        /// </summary>
        public void Hide()
        {
            // Dim 배경 비활성화
            if (_dimBackground != null)
            {
                _dimBackground.gameObject.SetActive(false);
            }

            // 카메라 드래그 복원
            if (CameraController.Instance != null)
            {
                CameraController.Instance.SetInputEnabled(true);
            }

            if (_failedPanel != null)
            {
                _failedPanel.SetActive(false);
            }
        }

        // ========== 이벤트 핸들러 ==========

        private void OnLevelFailed()
        {
            Show();
        }

        private void OnGameStateChanged(GameState newState)
        {
            // Playing 상태로 전환되면 결과 패널 숨김
            if (newState == GameState.Playing)
            {
                Hide();
            }
        }

        private void OnRestartClicked()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RestartLevel();
            }
        }

        private void OnMenuClicked()
        {
            // 로비 씬으로 이동
            if (GameManager.Instance != null)
            {
                GameManager.Instance.GoToLobby();
            }
        }

        private void OnPlayOnClicked()
        {
            // TODO: PlayOn 기능 구현 시 추가
            Debug.Log("[GameResultUI] PlayOn clicked - Not implemented yet");
        }
    }
}
