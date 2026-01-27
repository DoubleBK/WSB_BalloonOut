using System;
using System.Collections.Generic;
using UnityEngine;
using BalloonOut.Core;
using BalloonOut.Game.Grid;

namespace BalloonOut.Game.Arrow
{
    /// <summary>
    /// 화살표 탭/드래그 입력 처리
    /// ArrowController에서 분리된 입력 전담 컴포넌트
    /// </summary>
    public class ArrowInput : MonoBehaviour
    {
        // ========== 탭/드래그 판정 설정 ==========
        [Header("Tap Settings")]
        [SerializeField] private float _tapMaxDistance = 20f;    // 탭 최대 이동 거리 (픽셀)
        [SerializeField] private float _tapMaxDuration = 0.5f;   // 탭 최대 지속 시간 (초)
        [SerializeField] private float _touchAreaExpand = 0.2f;  // 터치 영역 확장 비율 (20%)

        // ========== 내부 상태 ==========
        private bool _isPotentialTap = false;
        private Vector2 _tapStartScreenPos;
        private float _tapStartTime;
        private bool _isEnabled = true;

        // 전역 입력 처리용 정적 변수 (같은 프레임에서 중복 터치 방지)
        private static int _lastInputFrame = -1;
        private static ArrowInput _lastTouchedInput = null;

        // ========== 참조 ==========
        private ArrowController _controller;
        private int _arrowId;

        // ========== 이벤트 ==========
        public event Action OnTapDetected;

        // ========== 프로퍼티 ==========
        public bool IsEnabled
        {
            get => _isEnabled;
            set => _isEnabled = value;
        }

        // ========== 초기화 ==========
        /// <summary>
        /// 입력 처리기 초기화
        /// </summary>
        public void Initialize(ArrowController controller, int arrowId)
        {
            _controller = controller;
            _arrowId = arrowId;
            _isPotentialTap = false;
        }

        // ========== 유니티 라이프사이클 ==========
        private void OnDisable()
        {
            _isPotentialTap = false;

            // 파괴된 입력이 _lastTouchedInput면 클리어
            if (_lastTouchedInput == this)
            {
                _lastTouchedInput = null;
            }
        }

        private void Update()
        {
            if (!_isEnabled || _controller == null) return;

            ProcessInput();
        }

        // ========== 입력 처리 ==========
        private void ProcessInput()
        {
            // 1. 입력 감지
            bool inputDown = false;
            bool inputUp = false;
            Vector2 inputPos = Vector2.zero;

#if UNITY_EDITOR || UNITY_STANDALONE
            if (Input.GetMouseButtonDown(0))
            {
                inputDown = true;
                inputPos = Input.mousePosition;
            }
            if (Input.GetMouseButtonUp(0))
            {
                inputUp = true;
                inputPos = Input.mousePosition;
            }
#else
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    inputDown = true;
                    inputPos = touch.position;
                }
                else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    inputUp = true;
                    inputPos = touch.position;
                }
            }
#endif

            // 2. 클릭 시작 처리 (탭 후보 등록)
            if (inputDown)
            {
                HandleInputDown(inputPos);
            }

            // 3. 클릭 종료 처리 (탭 판정)
            if (inputUp && _isPotentialTap)
            {
                HandleInputUp(inputPos);
            }

            // 4. 드래그 감지 시 탭 취소
            CheckDragCancel();
        }

        private void HandleInputDown(Vector2 inputPos)
        {
            // 상태 체크 (Idle 상태에서만 탭 가능)
            if (!_controller.CanLaunch) return;

            // 화면 좌표를 월드 좌표로 변환
            Vector3 worldPos3D = Camera.main.ScreenToWorldPoint(inputPos);
            Vector2 touchWorldPos = new Vector2(worldPos3D.x, worldPos3D.y);

            // 화살표 위에서 시작했는지 확인
            if (IsTouchOnArrowCells(touchWorldPos))
            {
                // 같은 프레임에서 다른 화살표가 이미 터치됐으면 무시
                if (_lastInputFrame == Time.frameCount && _lastTouchedInput != null)
                    return;

                // 탭 후보로 등록
                _isPotentialTap = true;
                _tapStartScreenPos = inputPos;
                _tapStartTime = Time.time;
                _lastInputFrame = Time.frameCount;
                _lastTouchedInput = this;

                Debug.Log($"[ArrowInput] Arrow {_arrowId} tap started at {inputPos}");
            }
        }

        private void HandleInputUp(Vector2 inputPos)
        {
            _isPotentialTap = false;

            // 상태 재확인 (드래그 중 상태가 변경됐을 수 있음)
            if (!_controller.CanLaunch)
            {
                Debug.Log($"[ArrowInput] Arrow {_arrowId} tap cancelled - state changed");
                return;
            }

            float distance = Vector2.Distance(inputPos, _tapStartScreenPos);
            float duration = Time.time - _tapStartTime;

            Debug.Log($"[ArrowInput] Arrow {_arrowId} input ended: distance={distance:F1}px, duration={duration:F2}s");

            // 탭 판정: 거리와 시간 모두 임계값 이하
            if (distance < _tapMaxDistance && duration < _tapMaxDuration)
            {
                Debug.Log($"[ArrowInput] Arrow {_arrowId} TAP detected!");
                OnTapDetected?.Invoke();
            }
            else
            {
                Debug.Log($"[ArrowInput] Arrow {_arrowId} was DRAG or HOLD, not TAP");
            }
        }

        private void CheckDragCancel()
        {
            if (_isPotentialTap && CameraController.Instance != null && CameraController.Instance.HasDragged)
            {
                Debug.Log($"[ArrowInput] Arrow {_arrowId} tap cancelled - drag detected");
                _isPotentialTap = false;
            }
        }

        /// <summary>
        /// 터치 위치가 화살표 셀 내에 있는지 확인
        /// </summary>
        private bool IsTouchOnArrowCells(Vector2 worldPos)
        {
            if (GridSystem.Instance == null)
            {
                Debug.LogWarning($"[ArrowInput] GridSystem.Instance is null, allowing touch");
                return true;
            }

            var cellPositions = _controller.GetAllWorldPositions();
            if (cellPositions == null || cellPositions.Count == 0)
            {
                Debug.LogWarning($"[ArrowInput] No cell positions for Arrow {_arrowId}, allowing touch");
                return true;
            }

            float cellSize = GridSystem.Instance.CellSize;
            // 터치 영역 확장 (기본 20%)
            float halfCell = cellSize * (0.5f + _touchAreaExpand);

            foreach (var cellPos in cellPositions)
            {
                float dx = Mathf.Abs(worldPos.x - cellPos.x);
                float dy = Mathf.Abs(worldPos.y - cellPos.y);

                if (dx <= halfCell && dy <= halfCell)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 탭 상태 강제 리셋 (외부에서 호출)
        /// </summary>
        public void ResetTapState()
        {
            _isPotentialTap = false;
        }
    }
}