using UnityEngine;

namespace BalloonOut.Core
{
    /// <summary>
    /// 카메라 컨트롤러 - Grid Size에 따른 자동 줌 및 드래그 이동
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        // ========== 싱글톤 ==========
        public static CameraController Instance { get; private set; }

        // ========== 인스펙터 노출 변수 ==========
        [Header("줌 설정")]
        [SerializeField] private float _minZoom = 3f;
        [SerializeField] private float _maxZoom = 25f;
        [SerializeField] private float _zoomSpeed = 0.5f;

        [Header("드래그 설정")]
        [SerializeField] private float _dragSpeed = 1f;
        [SerializeField] private float _dragThreshold = 10f;  // 드래그 인식 임계값 (픽셀)

        [Header("경계 설정")]
        [SerializeField] private float _boundaryPadding = 2f;
        [SerializeField] private float _verticalExtraPadding = 3f;  // 상하 추가 여유 (풍선 UI 영역 고려)
        [SerializeField] private float _bottomExtraPadding = 5f;  // 하단 추가 여유 (BottomUIBar 고려)
        [SerializeField] private float _minMoveRange = 2f;  // 경계가 카메라보다 작아도 허용되는 최소 이동 범위

        [Header("자동 크기 조절")]
        [SerializeField] private float _autoPadding = 1.5f;
        [SerializeField] private float _topMargin = 2f;  // 상단 풍선 영역 여유
        [SerializeField] private bool _useSmoothTransition = true;
        [SerializeField] private float _smoothSpeed = 5f;
        [SerializeField] private float _zoomOutStartPadding = 3f;  // 줌아웃 시작 시 추가 여유

        [Header("카메라 크기 제한")]
        [SerializeField, Tooltip("자동 조절 시 최소 카메라 크기")]
        private float _minCameraSize = 5f;

        // ========== 내부 상태 변수 ==========
        private Camera _camera;
        private Vector2 _dragStartPos;
        private Vector3 _cameraStartPos;
        private bool _isDragging;
        private bool _isPinching;
        private float _initialPinchDistance;
        private float _initialZoom;

        // 자동 크기 조절용
        private float _targetSize;
        private bool _isAutoTransitioning;

        // 드래그 판정용
        private bool _hasDragged;
        private float _totalDragDistance;

        // 카메라 드래그 경계 (월드 좌표)
        private Vector2 _worldBoundsMin;
        private Vector2 _worldBoundsMax;
        private bool _hasBounds;

        // 입력 활성화 여부
        private bool _isInputEnabled = true;

        // ========== 프로퍼티 ==========
        public bool IsDragging => _isDragging;
        public bool IsPinching => _isPinching;
        public bool IsInteracting => _isDragging || _isPinching;
        public bool HasDragged => _hasDragged;
        public Vector2 WorldBoundsMin => _worldBoundsMin;
        public Vector2 WorldBoundsMax => _worldBoundsMax;
        public bool HasBounds => _hasBounds;
        public bool IsInputEnabled => _isInputEnabled;

        // ========== 유니티 라이프사이클 ==========
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            _camera = GetComponent<Camera>();
            if (_camera == null)
            {
                _camera = Camera.main;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            // 자동 크기 조절 전환
            if (_isAutoTransitioning && _camera != null)
            {
                _camera.orthographicSize = Mathf.Lerp(
                    _camera.orthographicSize,
                    _targetSize,
                    Time.deltaTime * _smoothSpeed
                );

                if (Mathf.Abs(_camera.orthographicSize - _targetSize) < 0.01f)
                {
                    _camera.orthographicSize = _targetSize;
                    _isAutoTransitioning = false;
                }
            }

#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
            HandleMouseInput();
#else
            HandleTouchInput();
#endif
        }

        // ========== 마우스 입력 (에디터/PC) ==========
        private void HandleMouseInput()
        {
            if (!_isInputEnabled) return;

            // 부스터 실행 중 입력 차단
            if (GameManager.Instance?.IsUILockedForBooster ?? false) return;

            // 마우스 휠 줌
            float scroll = Input.mouseScrollDelta.y;
            if (scroll != 0f)
            {
                Zoom(-scroll * _zoomSpeed);
            }

            // 좌클릭(0), 우클릭(1), 중클릭(2) 모두 드래그 지원
            if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
            {
                StartDrag(Input.mousePosition);
            }
            else if (Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2))
            {
                UpdateDrag(Input.mousePosition);
            }
            else if (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1) || Input.GetMouseButtonUp(2))
            {
                EndDrag();
            }
        }

        // ========== 터치 입력 (모바일) ==========
        private void HandleTouchInput()
        {
            if (!_isInputEnabled) return;

            // 부스터 실행 중 입력 차단
            if (GameManager.Instance?.IsUILockedForBooster ?? false) return;

            // 마우스 휠 줌 (에뮬레이터/마우스 연결 태블릿 지원)
            float scroll = Input.mouseScrollDelta.y;
            if (scroll != 0f)
            {
                Zoom(-scroll * _zoomSpeed);
            }

            int touchCount = Input.touchCount;

            if (touchCount == 1)
            {
                // 핀치 중이었다면 종료
                if (_isPinching)
                {
                    EndPinch();
                }

                Touch touch = Input.GetTouch(0);

                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        StartDrag(touch.position);
                        break;
                    case TouchPhase.Moved:
                        UpdateDrag(touch.position);
                        break;
                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        EndDrag();
                        break;
                }
            }
            else if (touchCount == 2)
            {
                // 드래그 중이었다면 종료
                if (_isDragging)
                {
                    EndDrag();
                }

                Touch touch0 = Input.GetTouch(0);
                Touch touch1 = Input.GetTouch(1);

                if (touch0.phase == TouchPhase.Began || touch1.phase == TouchPhase.Began)
                {
                    StartPinch(touch0.position, touch1.position);
                }
                else if (touch0.phase == TouchPhase.Moved || touch1.phase == TouchPhase.Moved)
                {
                    UpdatePinch(touch0.position, touch1.position);
                }
                else if (touch0.phase == TouchPhase.Ended || touch1.phase == TouchPhase.Ended)
                {
                    EndPinch();
                }
            }
            else
            {
                EndDrag();
                EndPinch();
            }
        }

        // ========== 드래그 처리 ==========
        private void StartDrag(Vector2 screenPos)
        {
            _isDragging = true;
            _hasDragged = false;
            _totalDragDistance = 0f;
            _dragStartPos = screenPos;
            _cameraStartPos = transform.position;
        }

        private void UpdateDrag(Vector2 screenPos)
        {
            if (!_isDragging)
                return;

            Vector2 delta = screenPos - _dragStartPos;
            _totalDragDistance = delta.magnitude;

            // 드래그 임계값 초과 시에만 실제 카메라 이동
            if (_totalDragDistance > _dragThreshold)
            {
                _hasDragged = true;
                Vector2 worldDelta = delta * _dragSpeed * _camera.orthographicSize / 500f;
                Vector3 newPos = _cameraStartPos - new Vector3(worldDelta.x, worldDelta.y, 0);
                transform.position = ClampPosition(newPos);
            }
        }

        private void EndDrag()
        {
            _isDragging = false;
            // 드래그 종료 시 상태 리셋 (다음 클릭에서 화살표 터치 허용)
            _hasDragged = false;
            _totalDragDistance = 0f;
        }

        /// <summary>
        /// HasDragged 플래그 리셋
        /// </summary>
        public void ResetDragState()
        {
            _hasDragged = false;
            _totalDragDistance = 0f;
        }

        // ========== 핀치 줌 처리 ==========
        private void StartPinch(Vector2 pos0, Vector2 pos1)
        {
            _isPinching = true;
            _initialPinchDistance = Vector2.Distance(pos0, pos1);
            _initialZoom = _camera.orthographicSize;

            // 핀치 시작 시 드래그도 시작
            Vector2 center = (pos0 + pos1) * 0.5f;
            StartDrag(center);
        }

        private void UpdatePinch(Vector2 pos0, Vector2 pos1)
        {
            if (!_isPinching)
                return;

            // 수동 줌 시 자동 전환 중단
            _isAutoTransitioning = false;

            float currentDistance = Vector2.Distance(pos0, pos1);
            if (_initialPinchDistance > 0)
            {
                float ratio = _initialPinchDistance / currentDistance;
                float newZoom = _initialZoom * ratio;
                _camera.orthographicSize = Mathf.Clamp(newZoom, _minZoom, _maxZoom);
            }

            // 핀치 중 드래그도 업데이트
            Vector2 center = (pos0 + pos1) * 0.5f;
            UpdateDrag(center);
        }

        private void EndPinch()
        {
            _isPinching = false;
            EndDrag();
        }

        // ========== 줌 ==========
        private void Zoom(float delta)
        {
            // 수동 줌 시 자동 전환 중단
            _isAutoTransitioning = false;

            float newSize = _camera.orthographicSize + delta;
            _camera.orthographicSize = Mathf.Clamp(newSize, _minZoom, _maxZoom);
        }

        // ========== 유틸리티 ==========
        private Vector3 ClampPosition(Vector3 pos)
        {
            if (!_hasBounds || _camera == null)
                return pos;

            // 카메라 뷰포트 크기 계산
            float halfHeight = _camera.orthographicSize;
            float halfWidth = halfHeight * _camera.aspect;

            // 경계 내에서 카메라 중심이 이동할 수 있는 범위 계산
            float minX = _worldBoundsMin.x + halfWidth;
            float maxX = _worldBoundsMax.x - halfWidth;
            float minY = _worldBoundsMin.y + halfHeight;
            float maxY = _worldBoundsMax.y - halfHeight;

            // 경계가 카메라보다 작아도 최소 이동 범위 보장
            if (minX > maxX)
            {
                float centerX = (_worldBoundsMin.x + _worldBoundsMax.x) * 0.5f;
                pos.x = Mathf.Clamp(pos.x, centerX - _minMoveRange, centerX + _minMoveRange);
            }
            else
            {
                pos.x = Mathf.Clamp(pos.x, minX, maxX);
            }

            if (minY > maxY)
            {
                float centerY = (_worldBoundsMin.y + _worldBoundsMax.y) * 0.5f;
                pos.y = Mathf.Clamp(pos.y, centerY - _minMoveRange, centerY + _minMoveRange);
            }
            else
            {
                pos.y = Mathf.Clamp(pos.y, minY, maxY);
            }

            return pos;
        }

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// 카메라 초기화 (기본 위치/크기)
        /// </summary>
        public void ResetCamera()
        {
            transform.position = new Vector3(0, 0, transform.position.z);
            _camera.orthographicSize = 6f;
        }

        /// <summary>
        /// 카메라 입력(드래그, 줌) 활성화/비활성화
        /// </summary>
        /// <param name="enabled">true: 입력 허용, false: 입력 차단</param>
        public void SetInputEnabled(bool enabled)
        {
            _isInputEnabled = enabled;

            // 입력 비활성화 시 진행 중인 드래그/핀치 종료
            if (!enabled)
            {
                if (_isDragging)
                    EndDrag();
                if (_isPinching)
                    EndPinch();
            }

            Debug.Log($"[CameraController] Input {(enabled ? "enabled" : "disabled")}");
        }

        /// <summary>
        /// 그리드 크기에 맞춰 카메라 크기 자동 조절
        /// </summary>
        public void AdjustToGrid(int gridWidth, int gridHeight, float cellSize)
        {
            if (_camera == null)
                return;

            // 그리드 월드 크기 계산
            float gridWorldWidth = gridWidth * cellSize;
            float gridWorldHeight = gridHeight * cellSize;

            // 화면 비율 고려
            float aspectRatio = (float)Screen.width / Screen.height;

            // 세로 기준 필요 크기 (상단 마진 포함)
            float sizeForHeight = (gridWorldHeight / 2f) + _autoPadding + _topMargin;

            // 가로 기준 필요 크기
            float sizeForWidth = (gridWorldWidth / 2f) / aspectRatio + _autoPadding;

            // 둘 중 큰 값 선택 (최소 크기 보장)
            _targetSize = Mathf.Max(sizeForHeight, sizeForWidth, _minCameraSize);

            // 최대 줌 제한도 업데이트
            _maxZoom = Mathf.Max(_maxZoom, _targetSize + 5f);

            if (_useSmoothTransition)
            {
                // 줌아웃 상태에서 시작 → 적절한 크기로 줌인
                // 그리드 전체가 보이는 넉넉한 크기로 카메라를 먼저 설정
                float startSize = _targetSize + _zoomOutStartPadding;
                _camera.orthographicSize = startSize;
                _isAutoTransitioning = true;
            }
            else
            {
                _camera.orthographicSize = _targetSize;
            }

            // 카메라 위치도 중앙으로 리셋
            transform.position = new Vector3(0, 0, transform.position.z);

            // 드래그 경계 설정 (그리드 + 패딩)
            SetWorldBounds(gridWidth, gridHeight, cellSize);

            Debug.Log($"[CameraController] Adjusted to grid {gridWidth}x{gridHeight}, target size: {_targetSize:F1}");
        }

        /// <summary>
        /// 정사각형 그리드용 오버로드
        /// </summary>
        public void AdjustToGrid(int gridSize, float cellSize)
        {
            AdjustToGrid(gridSize, gridSize, cellSize);
        }

        /// <summary>
        /// 월드 경계 설정 (카메라 드래그 제한)
        /// </summary>
        public void SetWorldBounds(int gridWidth, int gridHeight, float cellSize)
        {
            // 그리드 중심이 (0,0)이므로 경계 계산
            float halfWidth = (gridWidth * cellSize) * 0.5f;
            float halfHeight = (gridHeight * cellSize) * 0.5f;

            // 경계에 패딩 추가 (상하 대칭 + 추가 여유)
            float verticalPadding = _boundaryPadding + _verticalExtraPadding + _bottomExtraPadding;
            _worldBoundsMin = new Vector2(-halfWidth - _boundaryPadding, -halfHeight - verticalPadding);
            _worldBoundsMax = new Vector2(halfWidth + _boundaryPadding, halfHeight + verticalPadding);
            _hasBounds = true;

            Debug.Log($"[CameraController] World bounds set: min={_worldBoundsMin}, max={_worldBoundsMax}");
        }

        /// <summary>
        /// 월드 좌표가 카메라 경계 밖인지 확인
        /// </summary>
        public bool IsOutOfWorldBounds(Vector2 worldPos)
        {
            if (!_hasBounds)
                return false;

            return worldPos.x < _worldBoundsMin.x || worldPos.x > _worldBoundsMax.x ||
                   worldPos.y < _worldBoundsMin.y || worldPos.y > _worldBoundsMax.y;
        }

        /// <summary>
        /// 월드 좌표가 현재 카메라 뷰포트 밖인지 확인
        /// </summary>
        public bool IsOutOfCameraView(Vector2 worldPos)
        {
            if (_camera == null)
                return false;

            Vector3 viewportPos = _camera.WorldToViewportPoint(worldPos);
            return viewportPos.x < 0f || viewportPos.x > 1f || viewportPos.y < 0f || viewportPos.y > 1f;
        }

        /// <summary>
        /// 현재 타겟 크기 반환
        /// </summary>
        public float GetTargetSize()
        {
            return _targetSize;
        }

        /// <summary>
        /// 현재 카메라 크기 반환
        /// </summary>
        public float GetCurrentSize()
        {
            return _camera != null ? _camera.orthographicSize : 0f;
        }
    }
}
