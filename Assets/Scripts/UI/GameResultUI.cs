using UnityEngine;
using UnityEngine.UI;
using BalloonOut.Core;

namespace BalloonOut.UI
{
    /// <summary>
    /// 게임 결과 UI (승리/패배 화면)
    /// </summary>
    public class GameResultUI : MonoBehaviour
    {
        // ========== 인스펙터 노출 변수 ==========
        [Header("Panels")]
        [SerializeField] private GameObject _resultPanel;
        [SerializeField] private GameObject _clearContent;
        [SerializeField] private GameObject _failedContent;

        [Header("Buttons")]
        [SerializeField] private Button _restartButton;
        [SerializeField] private Button _menuButton;

        [Header("Texts (Optional)")]
        [SerializeField] private Text _titleText;
        [SerializeField] private Text _messageText;

        [Header("Settings")]
        [SerializeField] private string _clearTitle = "CLEAR!";
        [SerializeField] private string _clearMessage = "레벨을 클리어했습니다!";
        [SerializeField] private string _failedTitle = "FAILED";
        [SerializeField] private string _failedMessage = "화살표를 모두 사용했습니다.";

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

            // GameManager 이벤트 구독
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnLevelCleared += OnLevelCleared;
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
                GameManager.Instance.OnLevelCleared -= OnLevelCleared;
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
        }

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// 클리어 화면 표시
        /// </summary>
        public void ShowClear()
        {
            Show(true);
        }

        /// <summary>
        /// 실패 화면 표시
        /// </summary>
        public void ShowFailed()
        {
            Show(false);
        }

        /// <summary>
        /// 패널 숨기기
        /// </summary>
        public void Hide()
        {
            if (_resultPanel != null)
            {
                _resultPanel.SetActive(false);
            }
        }

        // ========== 내부 유틸리티 ==========

        /// <summary>
        /// 결과 화면 표시
        /// </summary>
        private void Show(bool isClear)
        {
            if (_resultPanel != null)
            {
                _resultPanel.SetActive(true);
            }

            // 클리어/실패 콘텐츠 전환
            if (_clearContent != null)
            {
                _clearContent.SetActive(isClear);
            }

            if (_failedContent != null)
            {
                _failedContent.SetActive(!isClear);
            }

            // 텍스트 업데이트
            if (_titleText != null)
            {
                _titleText.text = isClear ? _clearTitle : _failedTitle;
            }

            if (_messageText != null)
            {
                _messageText.text = isClear ? _clearMessage : _failedMessage;
            }
        }

        // ========== 이벤트 핸들러 ==========

        private void OnLevelCleared()
        {
            ShowClear();
        }

        private void OnLevelFailed()
        {
            ShowFailed();
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
            // TODO: 메인 메뉴 씬으로 이동
            Debug.Log("Menu button clicked - Not implemented yet");
        }
    }
}