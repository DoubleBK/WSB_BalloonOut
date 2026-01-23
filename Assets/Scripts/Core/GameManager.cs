using System.Collections.Generic;
using UnityEngine;
using BalloonOut.Data;
using BalloonOut.Game.Grid;
using BalloonOut.Game.Arrow;
using BalloonOut.UI;

namespace BalloonOut.Core
{
    /// <summary>
    /// 게임 매니저
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        // ========== 싱글톤 ==========
        public static GameManager Instance { get; private set; }

        // ========== 이벤트 ==========
        public System.Action<GameState> OnGameStateChanged;
        public System.Action<GameColor, bool> OnArrowEscaped;  // color, wasMatch
        public System.Action OnLevelCleared;

        // ========== 인스펙터 노출 변수 ==========
        [Header("References")]
        [SerializeField] private GridSystem _gridSystem;
        [SerializeField] private SnakeMovement _snakeMovement;
        [SerializeField] private QueueUI _queueUI;
        [SerializeField] private Transform _arrowContainer;

        [Header("Prefabs")]
        [SerializeField] private GameObject _arrowPrefab;

        [Header("Test Level")]
        [SerializeField] private string _testLevelName = "Test_001";

        // ========== 내부 상태 변수 ==========
        private GameState _state = GameState.Ready;
        private LevelData _currentLevel;
        private List<ArrowController> _arrows = new List<ArrowController>();
        private bool _isProcessing = false;

        // ========== 프로퍼티 ==========
        public GameState State => _state;
        public LevelData CurrentLevel => _currentLevel;

        // ========== 유니티 라이프사이클 ==========
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            // 테스트 레벨 로드
            LoadLevel(_testLevelName);
        }

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// 레벨 로드
        /// </summary>
        public void LoadLevel(string levelName)
        {
            var levelData = LevelLoader.Load(levelName);
            if (levelData != null)
            {
                InitializeLevel(levelData);
            }
            else
            {
                Debug.LogError($"Failed to load level: {levelName}");
            }
        }

        /// <summary>
        /// 레벨 초기화
        /// </summary>
        public void InitializeLevel(LevelData levelData)
        {
            _currentLevel = levelData;

            // 기존 화살표 정리
            ClearArrows();

            // 그리드 초기화
            if (_gridSystem != null)
            {
                _gridSystem.Initialize(levelData.gridSize);
            }

            // Queue UI 초기화
            if (_queueUI != null && levelData.lanes != null)
            {
                _queueUI.Initialize(levelData.lanes);
            }

            // 화살표 스폰
            SpawnArrows(levelData.arrows);

            // 게임 상태 변경
            SetState(GameState.Playing);

            Debug.Log($"Level initialized: {levelData.name}");
        }

        /// <summary>
        /// 레벨 재시작
        /// </summary>
        public void RestartLevel()
        {
            if (_currentLevel != null)
            {
                InitializeLevel(_currentLevel);
            }
        }

        // ========== 내부 유틸리티 ==========

        /// <summary>
        /// 화살표 스폰
        /// </summary>
        private void SpawnArrows(List<ArrowData> arrowsData)
        {
            if (arrowsData == null) return;

            for (int i = 0; i < arrowsData.Count; i++)
            {
                var data = arrowsData[i];
                SpawnArrow(i, data);
            }
        }

        /// <summary>
        /// 단일 화살표 스폰
        /// </summary>
        private void SpawnArrow(int id, ArrowData data)
        {
            GameObject arrowObj;

            if (_arrowPrefab != null)
            {
                arrowObj = Instantiate(_arrowPrefab, _arrowContainer);
            }
            else
            {
                arrowObj = new GameObject($"Arrow_{id}");
                arrowObj.transform.SetParent(_arrowContainer);
                arrowObj.AddComponent<ArrowController>();
            }

            var controller = arrowObj.GetComponent<ArrowController>();
            if (controller == null)
            {
                controller = arrowObj.AddComponent<ArrowController>();
            }

            controller.Initialize(id, data);
            controller.OnTapped += OnArrowTapped;

            _arrows.Add(controller);
        }

        /// <summary>
        /// 화살표 정리
        /// </summary>
        private void ClearArrows()
        {
            foreach (var arrow in _arrows)
            {
                if (arrow != null)
                {
                    arrow.Cleanup();
                    Destroy(arrow.gameObject);
                }
            }
            _arrows.Clear();

            if (_gridSystem != null)
            {
                _gridSystem.ClearAllOccupied();
            }
        }

        /// <summary>
        /// 화살표 탭 이벤트
        /// </summary>
        private void OnArrowTapped(ArrowController arrow)
        {
            if (_state != GameState.Playing) return;
            if (_isProcessing) return;

            _isProcessing = true;

            // 이동 시작
            if (_snakeMovement != null)
            {
                _snakeMovement.StartMovement(arrow, (escaped) =>
                {
                    if (escaped)
                    {
                        OnArrowEscapedHandler(arrow);
                    }
                    else
                    {
                        // 막힘 - 토스트 메시지 등
                        Debug.Log("BLOCKED!");
                    }

                    _isProcessing = false;
                });
            }
            else
            {
                _isProcessing = false;
            }
        }

        /// <summary>
        /// 화살표 탈출 처리
        /// </summary>
        private void OnArrowEscapedHandler(ArrowController arrow)
        {
            // 풍선 팝 시도
            bool wasMatch = false;
            if (_queueUI != null)
            {
                wasMatch = _queueUI.TryPopBalloon(arrow.Color);
            }

            // 이벤트 발생
            OnArrowEscaped?.Invoke(arrow.Color, wasMatch);

            // 화살표 제거
            _arrows.Remove(arrow);
            Destroy(arrow.gameObject);

            // 승리 조건 확인
            CheckWinCondition();

            Debug.Log(wasMatch ? "POP!" : "FLY AWAY");
        }

        /// <summary>
        /// 승리 조건 확인
        /// </summary>
        private void CheckWinCondition()
        {
            if (_queueUI != null && _queueUI.IsAllCleared())
            {
                SetState(GameState.Win);
                OnLevelCleared?.Invoke();
                Debug.Log("LEVEL CLEARED!");
            }
        }

        /// <summary>
        /// 게임 상태 변경
        /// </summary>
        private void SetState(GameState newState)
        {
            if (_state == newState) return;

            _state = newState;
            OnGameStateChanged?.Invoke(_state);

            Debug.Log($"Game State: {_state}");
        }
    }
}