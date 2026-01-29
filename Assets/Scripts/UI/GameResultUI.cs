using UnityEngine;
using UnityEngine.UI;
using TMPro;
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
        [SerializeField] private Button _nextLevelButton;
        [SerializeField] private Button _menuButton;
        [SerializeField] private Button _playOnButton;

        [Header("Texts (Optional)")]
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _messageText;
        [SerializeField] private TMP_Text _levelInfoText;

        [Header("Settings")]
        [SerializeField] private string _clearTitle = "CLEAR!";
        [SerializeField] private string _clearMessage = "레벨을 클리어했습니다!";
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

            if (_nextLevelButton != null)
            {
                _nextLevelButton.onClick.AddListener(OnNextLevelClicked);
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

            if (_nextLevelButton != null)
            {
                _nextLevelButton.onClick.RemoveListener(OnNextLevelClicked);
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

            // 레벨 정보 업데이트
            UpdateLevelInfo();

            // 다음 레벨 버튼 표시 (클리어 시에만, 다음 레벨이 있을 때만)
            if (_nextLevelButton != null)
            {
                bool hasNextLevel = isClear && HasNextLevel();
                _nextLevelButton.gameObject.SetActive(hasNextLevel);
            }

            // PlayOn 버튼 표시 (실패 시에만)
            if (_playOnButton != null)
            {
                _playOnButton.gameObject.SetActive(!isClear);
            }
        }

        /// <summary>
        /// 레벨 정보 업데이트
        /// </summary>
        private void UpdateLevelInfo()
        {
            if (_levelInfoText == null) return;
            if (GameManager.Instance == null) return;

            int currentLevel = GameManager.Instance.CurrentLevelIdx;
            int totalLevels = GameManager.Instance.TotalLevelCount;
            var stageEntry = GameManager.Instance.CurrentStageEntry;

            string difficulty = stageEntry != null ? stageEntry.Difficulty : "Normal";
            _levelInfoText.text = $"Level {currentLevel} / {totalLevels}\n{difficulty}";
        }

        /// <summary>
        /// 다음 레벨 존재 여부 확인
        /// </summary>
        private bool HasNextLevel()
        {
            if (GameManager.Instance == null) return false;

            int nextLevelIdx = GameManager.Instance.CurrentLevelIdx + 1;
            return BalloonOut.Data.StageLoader.GetEntryByLevelIdx(nextLevelIdx) != null;
        }

        // ========== 이벤트 핸들러 ==========

        private void OnLevelCleared()
        {
            // 클리어 시에는 Confetti 연출 후 로비로 이동하므로 결과 패널 표시하지 않음
            // Confetti 연출은 GameManager.PlayClearSequence()에서 처리
            Debug.Log("[GameResultUI] Level cleared - Confetti sequence will handle transition");
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

        private void OnNextLevelClicked()
        {
            if (GameManager.Instance != null)
            {
                bool hasNext = GameManager.Instance.NextLevel();
                if (!hasNext)
                {
                    Debug.Log("[GameResultUI] No more levels available!");
                    // 모든 레벨 클리어 시 처리 (필요 시 추가)
                }
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