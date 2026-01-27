using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using NGFE.Data;
using BalloonOut.Core;

namespace BalloonOut.UI
{
    /// <summary>
    /// 게임 화면 하단 부스터 UI
    /// Undo, Hint 등 부스터 버튼 관리
    /// </summary>
    public class BoosterBottomBarUI : MonoBehaviour
    {
        // ========== 버튼 참조 ==========
        [Header("Booster Buttons")]
        [SerializeField] private Button _undoButton;
        [SerializeField] private Button _hintButton;
        [SerializeField] private Button _tripleArrowButton;
        [SerializeField] private Button _dartArrowButton;

        // ========== 수량 배지 ==========
        [Header("Quantity Badges")]
        [SerializeField] private TextMeshProUGUI _undoQuantityText;
        [SerializeField] private TextMeshProUGUI _hintQuantityText;
        [SerializeField] private TextMeshProUGUI _tripleArrowQuantityText;
        [SerializeField] private TextMeshProUGUI _dartArrowQuantityText;

        // ========== 잠금 아이콘 ==========
        [Header("Lock Icons")]
        [SerializeField] private GameObject _undoLockIcon;
        [SerializeField] private GameObject _hintLockIcon;
        [SerializeField] private GameObject _tripleArrowLockIcon;
        [SerializeField] private GameObject _dartArrowLockIcon;

        // ========== 애니메이션 설정 ==========
        [Header("Animation")]
        [SerializeField] private float _clickScale = 0.9f;
        [SerializeField] private float _clickDuration = 0.1f;
        [SerializeField] private float _disabledAlpha = 0.5f;

        // ========== 내부 상태 ==========
        private CanvasGroup _undoCanvasGroup;
        private CanvasGroup _hintCanvasGroup;
        private CanvasGroup _tripleArrowCanvasGroup;
        private CanvasGroup _dartArrowCanvasGroup;

        // ========== 유니티 라이프사이클 ==========
        private void Start()
        {
            InitializeCanvasGroups();
            BindButtons();
            SubscribeEvents();
            UpdateAllButtonStates();
        }

        private void OnDestroy()
        {
            UnbindButtons();
            UnsubscribeEvents();
        }

        // ========== 초기화 ==========
        private void InitializeCanvasGroups()
        {
            _undoCanvasGroup = GetOrAddCanvasGroup(_undoButton);
            _hintCanvasGroup = GetOrAddCanvasGroup(_hintButton);
            _tripleArrowCanvasGroup = GetOrAddCanvasGroup(_tripleArrowButton);
            _dartArrowCanvasGroup = GetOrAddCanvasGroup(_dartArrowButton);
        }

        private CanvasGroup GetOrAddCanvasGroup(Button button)
        {
            if (button == null) return null;

            var canvasGroup = button.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = button.gameObject.AddComponent<CanvasGroup>();
            }
            return canvasGroup;
        }

        private void BindButtons()
        {
            _undoButton?.onClick.AddListener(OnUndoClicked);
            _hintButton?.onClick.AddListener(OnHintClicked);
            _tripleArrowButton?.onClick.AddListener(OnTripleArrowClicked);
            _dartArrowButton?.onClick.AddListener(OnDartArrowClicked);
        }

        private void UnbindButtons()
        {
            _undoButton?.onClick.RemoveListener(OnUndoClicked);
            _hintButton?.onClick.RemoveListener(OnHintClicked);
            _tripleArrowButton?.onClick.RemoveListener(OnTripleArrowClicked);
            _dartArrowButton?.onClick.RemoveListener(OnDartArrowClicked);
        }

        private void SubscribeEvents()
        {
            if (BoosterManager.Instance != null)
            {
                BoosterManager.Instance.OnBoosterQuantityChanged += OnQuantityChanged;
                BoosterManager.Instance.OnUndoAvailabilityChanged += OnUndoAvailabilityChanged;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged += OnGameStateChanged;
            }
        }

        private void UnsubscribeEvents()
        {
            if (BoosterManager.Instance != null)
            {
                BoosterManager.Instance.OnBoosterQuantityChanged -= OnQuantityChanged;
                BoosterManager.Instance.OnUndoAvailabilityChanged -= OnUndoAvailabilityChanged;
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnGameStateChanged -= OnGameStateChanged;
            }
        }

        // ========== 버튼 클릭 핸들러 ==========
        private void OnUndoClicked()
        {
            if (!CanUseBooster(ITEM_TYPE.UNDO)) return;

            PlayClickAnimation(_undoButton.transform, () =>
            {
                if (BoosterManager.Instance != null)
                {
                    BoosterManager.Instance.UseUndo();
                    UpdateUndoButtonState();
                }
            });
        }

        private void OnHintClicked()
        {
            if (!CanUseBooster(ITEM_TYPE.HINT)) return;

            PlayClickAnimation(_hintButton.transform, () =>
            {
                if (BoosterManager.Instance != null)
                {
                    BoosterManager.Instance.UseHint();
                    UpdateHintButtonState();
                }
            });
        }

        private void OnTripleArrowClicked()
        {
            if (!CanUseBooster(ITEM_TYPE.TRIPLEARROW)) return;

            PlayClickAnimation(_tripleArrowButton.transform, () =>
            {
                // TODO: TripleArrow 구현 (2차 개발)
                Debug.Log("[BoosterBottomBarUI] TripleArrow not implemented yet");
            });
        }

        private void OnDartArrowClicked()
        {
            if (!CanUseBooster(ITEM_TYPE.DARTARROW)) return;

            PlayClickAnimation(_dartArrowButton.transform, () =>
            {
                // TODO: DartArrow 구현 (2차 개발)
                Debug.Log("[BoosterBottomBarUI] DartArrow not implemented yet");
            });
        }

        // ========== 버튼 상태 업데이트 ==========
        public void UpdateAllButtonStates()
        {
            UpdateUndoButtonState();
            UpdateHintButtonState();
            UpdateTripleArrowButtonState();
            UpdateDartArrowButtonState();
        }

        private void UpdateUndoButtonState()
        {
            UpdateButtonState(
                ITEM_TYPE.UNDO,
                _undoButton,
                _undoQuantityText,
                _undoCanvasGroup,
                _undoLockIcon,
                BoosterManager.Instance?.CanUndo ?? false
            );
        }

        private void UpdateHintButtonState()
        {
            bool canUseHint = GameManager.Instance?.State == GameState.Playing;
            UpdateButtonState(
                ITEM_TYPE.HINT,
                _hintButton,
                _hintQuantityText,
                _hintCanvasGroup,
                _hintLockIcon,
                canUseHint
            );
        }

        private void UpdateTripleArrowButtonState()
        {
            UpdateButtonState(
                ITEM_TYPE.TRIPLEARROW,
                _tripleArrowButton,
                _tripleArrowQuantityText,
                _tripleArrowCanvasGroup,
                _tripleArrowLockIcon,
                false // 2차 개발 - 항상 비활성
            );
        }

        private void UpdateDartArrowButtonState()
        {
            UpdateButtonState(
                ITEM_TYPE.DARTARROW,
                _dartArrowButton,
                _dartArrowQuantityText,
                _dartArrowCanvasGroup,
                _dartArrowLockIcon,
                false // 2차 개발 - 항상 비활성
            );
        }

        private void UpdateButtonState(
            ITEM_TYPE itemType,
            Button button,
            TextMeshProUGUI quantityText,
            CanvasGroup canvasGroup,
            GameObject lockIcon,
            bool additionalCondition)
        {
            if (button == null) return;

            int quantity = BoosterManager.Instance?.GetBoosterQuantity(itemType) ?? 0;
            bool isUnlocked = BoosterManager.Instance?.IsBoosterUnlocked(itemType) ?? false;
            bool canUse = isUnlocked && quantity > 0 && additionalCondition;

            // 수량 텍스트 업데이트
            if (quantityText != null)
            {
                quantityText.text = quantity.ToString();
                quantityText.gameObject.SetActive(isUnlocked);
            }

            // 잠금 아이콘
            if (lockIcon != null)
            {
                lockIcon.SetActive(!isUnlocked);
            }

            // 버튼 인터랙션
            button.interactable = canUse;

            // 시각적 피드백
            if (canvasGroup != null)
            {
                canvasGroup.alpha = canUse ? 1f : _disabledAlpha;
            }
        }

        private bool CanUseBooster(ITEM_TYPE itemType)
        {
            if (BoosterManager.Instance == null) return false;

            bool hasQuantity = BoosterManager.Instance.GetBoosterQuantity(itemType) > 0;
            bool isUnlocked = BoosterManager.Instance.IsBoosterUnlocked(itemType);

            // Undo 추가 조건: 히스토리가 있어야 함
            if (itemType == ITEM_TYPE.UNDO)
            {
                return hasQuantity && isUnlocked && BoosterManager.Instance.CanUndo;
            }

            // Hint 추가 조건: 게임 진행 중이어야 함
            if (itemType == ITEM_TYPE.HINT)
            {
                return hasQuantity && isUnlocked && GameManager.Instance?.State == GameState.Playing;
            }

            return hasQuantity && isUnlocked;
        }

        // ========== 이벤트 핸들러 ==========
        private void OnQuantityChanged(ITEM_TYPE itemType, int newQuantity)
        {
            switch (itemType)
            {
                case ITEM_TYPE.UNDO:
                    UpdateUndoButtonState();
                    break;
                case ITEM_TYPE.HINT:
                    UpdateHintButtonState();
                    break;
                case ITEM_TYPE.TRIPLEARROW:
                    UpdateTripleArrowButtonState();
                    break;
                case ITEM_TYPE.DARTARROW:
                    UpdateDartArrowButtonState();
                    break;
            }
        }

        private void OnUndoAvailabilityChanged(bool canUndo)
        {
            UpdateUndoButtonState();
        }

        private void OnGameStateChanged(GameState newState)
        {
            UpdateAllButtonStates();
        }

        // ========== 애니메이션 ==========
        private void PlayClickAnimation(Transform target, TweenCallback onComplete)
        {
            if (target == null) return;

            target.DOScale(_clickScale, _clickDuration)
                .SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    target.DOScale(1f, _clickDuration)
                        .SetEase(Ease.OutQuad)
                        .OnComplete(onComplete);
                });
        }

        /// <summary>
        /// 수량 부족 시 버튼 흔들림 애니메이션
        /// </summary>
        public void PlayInsufficientAnimation(ITEM_TYPE itemType)
        {
            Transform target = itemType switch
            {
                ITEM_TYPE.UNDO => _undoButton?.transform,
                ITEM_TYPE.HINT => _hintButton?.transform,
                ITEM_TYPE.TRIPLEARROW => _tripleArrowButton?.transform,
                ITEM_TYPE.DARTARROW => _dartArrowButton?.transform,
                _ => null
            };

            if (target != null)
            {
                target.DOShakePosition(0.3f, 5f, 20, 90f, false, true);
            }
        }
    }
}
