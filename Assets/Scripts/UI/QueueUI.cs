using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BalloonOut.Core;
using BalloonOut.Data;
using BalloonOut.Game.Balloon;
using BalloonOut.Game.Gimmick;

namespace BalloonOut.UI
{
    /// <summary>
    /// 풍선 Queue UI
    /// </summary>
    public class QueueUI : MonoBehaviour
    {
        // ========== 인스펙터 노출 변수 ==========
        [Header("References")]
        [SerializeField] private Transform _lanesContainer;
        [SerializeField] private GameObject _lanePrefab;
        [SerializeField] private GameObject _balloonPrefab;

        [Header("Settings")]
        [SerializeField] private float _balloonSize = 60f;
        [SerializeField] private float _balloonSpacing = 10f;  // 음수 값 사용 시 풍선 겹침
        [SerializeField] private float _laneSpacing = 40f;

        [Header("Overlap Settings")]
        [SerializeField] private bool _enableOverlap = true;  // 풍선 겹침 활성화
        [SerializeField] private float _overlapAmount = 20f;   // 겹치는 정도 (양수 = 더 많이 겹침)

        [Header("Animation")]
        [SerializeField] private float _slideAnimDuration = 0.25f;

        [Header("Conveyor Belt")]
        [SerializeField] private GameObject _conveyorTilePrefab;  // Animator 포함 타일 프리팹
        [SerializeField] private int _conveyorTileCount = 8;       // 벨트당 타일 수
        [SerializeField] private float _conveyorTileScale = 2.4f;  // 타일 스케일 배율
        [SerializeField] private float _conveyorAnimSpeed = 1f;    // 벨트 애니메이션 재생 속도

        [Header("Pop Effect")]
        [SerializeField] private ParticleSystem _popEffectPrefab;  // 풍선 팝 파티클 프리팹
        [SerializeField] private float _popEffectDuration = 0.5f;  // 파티클 지속 시간
        [SerializeField] private bool _autoCreatePopEffect = true; // 프리팹 없을 시 자동 생성

        // ========== 내부 상태 변수 ==========
        private List<List<Image>> _balloonImages = new List<List<Image>>();
        private List<List<GameColor>> _lanes = new List<List<GameColor>>();

        // 기믹 지원용 BalloonInstance 리스트
        private List<List<BalloonInstance>> _balloonInstances = new List<List<BalloonInstance>>();

        // 각 레인의 풍선 기준 위치 (balloons[0]의 anchoredPosition)
        private List<Vector2> _laneBasePositions = new List<Vector2>();

        // 각 레인의 앵커/피벗 정보 (LayoutGroup이 설정한 값 보존)
        private struct LaneAnchorInfo
        {
            public Vector2 anchorMin;
            public Vector2 anchorMax;
            public Vector2 pivot;
        }
        private List<LaneAnchorInfo> _laneAnchorInfos = new List<LaneAnchorInfo>();

        // 컨베이어 벨트 루트 오브젝트 (레인별)
        private List<GameObject> _conveyorBelts = new List<GameObject>();


        // ========== 싱글톤 ==========
        public static QueueUI Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            // Connected 기믹 그룹 POP 이벤트 구독
            if (ConnectedBalloonManager.Instance != null)
            {
                ConnectedBalloonManager.Instance.OnGroupPop += HandleConnectedGroupPop;
            }
        }

        private void OnDisable()
        {
            // Connected 기믹 그룹 POP 이벤트 구독 해제
            if (ConnectedBalloonManager.Instance != null)
            {
                ConnectedBalloonManager.Instance.OnGroupPop -= HandleConnectedGroupPop;
            }
        }

        /// <summary>
        /// Connected 그룹 전체 POP 핸들러
        /// 직접 Hit된 풍선 외의 나머지 풍선들을 POP 처리
        /// </summary>
        private void HandleConnectedGroupPop(string groupId, List<BalloonInstance> balloons)
        {
            Debug.Log($"[QueueUI] Handling connected group pop: {groupId}, Count: {balloons?.Count ?? 0}");

            if (balloons == null) return;

            foreach (var balloon in balloons)
            {
                // 직접 Hit된 풍선은 TryPopBalloonWithGimmick에서 이미 처리됨
                // 여기서는 나머지 Marked 풍선들만 처리
                PopBalloonInstance(balloon);
            }

            // 그룹 POP 완료 알림
            ConnectedBalloonManager.Instance?.FinishGroupPop(groupId);

            // 그룹 POP 후 레벨 클리어 체크 요청
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RequestWinConditionCheck();
            }
        }

        /// <summary>
        /// 특정 BalloonInstance를 직접 POP 처리
        /// Connected 기믹 그룹 POP용
        /// </summary>
        public void PopBalloonInstance(BalloonInstance balloon)
        {
            if (balloon == null) return;

            int laneIdx = balloon.LaneIndex;
            if (laneIdx < 0 || laneIdx >= _balloonInstances.Count) return;

            var instanceLane = _balloonInstances[laneIdx];
            int balloonIndex = instanceLane.IndexOf(balloon);
            if (balloonIndex < 0) return;

            // Connected 렌더러에서 제거
            var connectedData = balloon.GetGimmickData("connected");
            if (connectedData != null)
            {
                string groupId = connectedData.GetParam("groupId", "");
                ConnectedLineRenderer.RemoveBalloonFromGroup(groupId, balloon);
            }

            // 기믹 알림
            balloon.NotifyPop();

            // 데이터에서 제거
            if (laneIdx < _lanes.Count && balloonIndex < _lanes[laneIdx].Count)
            {
                _lanes[laneIdx].RemoveAt(balloonIndex);
            }
            instanceLane.RemoveAt(balloonIndex);

            // UI 제거 및 애니메이션
            if (laneIdx < _balloonImages.Count && balloonIndex < _balloonImages[laneIdx].Count)
            {
                var balloonList = _balloonImages[laneIdx];
                var balloonImage = balloonList[balloonIndex];
                balloonList.RemoveAt(balloonIndex);

                if (balloonImage != null)
                {
                    // 팝 애니메이션
                    AnimatePop(balloonImage.gameObject);
                }

                // 남은 풍선들 위치 재배치 애니메이션 (어떤 풍선이든 제거 후)
                if (balloonList.Count > 0)
                {
                    AnimateRemainingBalloons(balloonList, balloonImage, laneIdx);
                }

                // 활성 풍선이 제거된 경우에만 다음 풍선에게 활성화 알림
                if (balloonIndex == 0 && instanceLane.Count > 0)
                {
                    instanceLane[0].PositionInLane = 0;
                    instanceLane[0].NotifyBecomeActive();
                }
            }

            // 나머지 인스턴스들의 PositionInLane 업데이트
            for (int i = 0; i < instanceLane.Count; i++)
            {
                instanceLane[i].PositionInLane = i;
            }

            UpdateHeadHighlights();
        }

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// Queue 초기화
        /// </summary>
        public void Initialize(List<LaneData> lanesData)
        {
            Clear();

            _lanes = new List<List<GameColor>>();
            _balloonInstances = new List<List<BalloonInstance>>();

            for (int laneIdx = 0; laneIdx < lanesData.Count; laneIdx++)
            {
                var lane = lanesData[laneIdx];
                var balloonDataList = lane.GetBalloonDataList();

                // 디버그: balloonData와 gimmicks 확인
                Debug.Log($"[QueueUI] Lane {laneIdx}: balloonData count={lane.balloonData?.Count ?? 0}, balloons count={lane.balloons?.Count ?? 0}");
                for (int b = 0; b < balloonDataList.Count; b++)
                {
                    var bd = balloonDataList[b];
                    int gimmickCount = bd.gimmicks?.Count ?? 0;
                    Debug.Log($"[QueueUI] Lane {laneIdx}, Balloon {b}: color={bd.color}, gimmicks={gimmickCount}");
                    if (gimmickCount > 0)
                    {
                        foreach (var g in bd.gimmicks)
                        {
                            Debug.Log($"[QueueUI]   -> Gimmick: {g.gimmickId}, params: {string.Join(", ", g.Parameters)}");
                        }
                    }
                }

                _lanes.Add(lane.GetColors());

                // BalloonInstance 생성
                var instanceList = new List<BalloonInstance>();
                for (int i = 0; i < balloonDataList.Count; i++)
                {
                    var instance = new BalloonInstance(laneIdx, i, balloonDataList[i]);
                    instanceList.Add(instance);
                }
                _balloonInstances.Add(instanceList);
            }

            CreateUI();

            // Connected 기믹 실타래 렌더러 설정
            SetupConnectedRenderers();

            // 첫 번째 풍선들(활성 위치)에 OnBecomeActive 호출
            NotifyActiveBalloons();
        }

        /// <summary>
        /// 활성 풍선들에게 OnBecomeActive 알림
        /// </summary>
        private void NotifyActiveBalloons()
        {
            for (int laneIdx = 0; laneIdx < _balloonInstances.Count; laneIdx++)
            {
                var lane = _balloonInstances[laneIdx];
                if (lane.Count > 0)
                {
                    lane[0].NotifyBecomeActive();
                }
            }
        }

        /// <summary>
        /// Connected 기믹 실타래 렌더러 설정
        /// 연결된 풍선들 사이에 시각적 연결선 표시
        /// </summary>
        private void SetupConnectedRenderers()
        {
            int totalBalloons = 0;
            int connectedBalloons = 0;

            foreach (var lane in _balloonInstances)
            {
                foreach (var balloon in lane)
                {
                    totalBalloons++;
                    var connectedData = balloon.GetGimmickData("connected");
                    if (connectedData != null)
                    {
                        connectedBalloons++;
                        string groupId = connectedData.GetParam("groupId", "");
                        Debug.Log($"[QueueUI] Found Connected balloon at lane {balloon.LaneIndex}, groupId: '{groupId}'");
                        if (!string.IsNullOrEmpty(groupId))
                        {
                            var renderer = ConnectedLineRenderer.GetOrCreate(groupId, _lanesContainer);
                            renderer.AddBalloon(balloon);
                        }
                    }
                }
            }

            Debug.Log($"[QueueUI] SetupConnectedRenderers: Total balloons: {totalBalloons}, Connected balloons: {connectedBalloons}");
        }

        /// <summary>
        /// 풍선 팝 시도
        /// </summary>
        /// <param name="color">터뜨릴 색상</param>
        /// <returns>성공 여부</returns>
        public bool TryPopBalloon(GameColor color)
        {
            return TryPopBalloon(color, out _);
        }

        /// <summary>
        /// 풍선 팝 시도 (레인 인덱스 반환)
        /// </summary>
        /// <param name="color">터뜨릴 색상</param>
        /// <param name="poppedLaneIndex">팝된 레인 인덱스 (-1 if not popped)</param>
        /// <returns>성공 여부</returns>
        public bool TryPopBalloon(GameColor color, out int poppedLaneIndex)
        {
            var result = TryPopBalloonWithGimmick(color, out poppedLaneIndex, out _);
            return result == PopResult.Popped;
        }

        /// <summary>
        /// 풍선 팝 시도 결과
        /// </summary>
        public enum PopResult
        {
            NoMatch,      // 색상이 맞지 않음
            Hit,          // 기믹에 의해 팝되지 않음 (Number 등)
            Popped        // 완전히 팝됨
        }

        /// <summary>
        /// 풍선 팝 시도 (기믹 지원)
        /// </summary>
        /// <param name="color">터뜨릴 색상</param>
        /// <param name="poppedLaneIndex">처리된 레인 인덱스</param>
        /// <param name="hitResult">기믹 처리 결과</param>
        /// <returns>팝 결과</returns>
        public PopResult TryPopBalloonWithGimmick(GameColor color, out int poppedLaneIndex, out GimmickHitResult hitResult)
        {
            poppedLaneIndex = -1;
            hitResult = GimmickHitResult.DefaultPop;

            for (int laneIdx = 0; laneIdx < _lanes.Count; laneIdx++)
            {
                var lane = _lanes[laneIdx];
                var instanceLane = _balloonInstances.Count > laneIdx ? _balloonInstances[laneIdx] : null;

                // Connected 기믹: Marked 풍선은 Target에서 제외
                if (instanceLane != null && instanceLane.Count > 0 && instanceLane[0].IsMarked())
                {
                    continue;  // 이 레인은 스킵
                }

                // balloons[0]이 활성 풍선 (가장 아래)
                if (lane.Count > 0 && lane[0] == color)
                {
                    poppedLaneIndex = laneIdx;

                    // 기믹이 있는 풍선인지 확인
                    if (instanceLane != null && instanceLane.Count > 0)
                    {
                        var balloon = instanceLane[0];

                        // 기믹 처리
                        bool shouldPop = balloon.TryHit(color, out hitResult);

                        if (!shouldPop)
                        {
                            // 기믹에 의해 팝 방지됨 (Number 기믹 등)
                            return PopResult.Hit;
                        }

                        // Connected 렌더러에서 제거
                        var connectedData = balloon.GetGimmickData("connected");
                        if (connectedData != null)
                        {
                            string groupId = connectedData.GetParam("groupId", "");
                            ConnectedLineRenderer.RemoveBalloonFromGroup(groupId, balloon);
                        }

                        // 팝 진행 - 기믹 알림
                        balloon.NotifyPop();
                        instanceLane.RemoveAt(0);
                    }

                    // 데이터 업데이트
                    lane.RemoveAt(0);

                    // UI 업데이트
                    if (_balloonImages.Count > laneIdx && _balloonImages[laneIdx].Count > 0)
                    {
                        var balloonList = _balloonImages[laneIdx];
                        var firstBalloon = balloonList[0];
                        balloonList.RemoveAt(0);

                        // 남은 풍선들의 슬라이드 애니메이션 시작 (laneIdx 전달)
                        AnimateRemainingBalloons(balloonList, firstBalloon, laneIdx);

                        // 팝 애니메이션
                        AnimatePop(firstBalloon.gameObject);
                    }

                    // 다음 풍선에게 활성화 알림
                    if (instanceLane != null && instanceLane.Count > 0)
                    {
                        instanceLane[0].PositionInLane = 0;
                        instanceLane[0].NotifyBecomeActive();
                    }

                    UpdateHeadHighlights();
                    return PopResult.Popped;
                }
            }

            return PopResult.NoMatch;
        }

        /// <summary>
        /// 모든 풍선이 팝되었는지 확인
        /// </summary>
        public bool IsAllCleared()
        {
            foreach (var lane in _lanes)
            {
                if (lane.Count > 0) return false;
            }
            return true;
        }

        /// <summary>
        /// 남은 풍선 수
        /// </summary>
        public int RemainingBalloons()
        {
            int count = 0;
            foreach (var lane in _lanes)
            {
                count += lane.Count;
            }
            return count;
        }

        /// <summary>
        /// 특정 색상의 풍선 월드 좌표 반환 (HomingArrow 타겟용)
        /// 해당 색상의 활성 풍선(첫 번째 풍선, 가장 아래) 위치를 반환
        /// Connected 기믹의 Marked 풍선은 제외
        /// </summary>
        public Vector3 GetBalloonWorldPosition(GameColor color)
        {
            for (int laneIdx = 0; laneIdx < _lanes.Count; laneIdx++)
            {
                var lane = _lanes[laneIdx];
                var instanceLane = _balloonInstances.Count > laneIdx ? _balloonInstances[laneIdx] : null;

                // Connected 기믹: Marked 풍선은 Target에서 제외
                if (instanceLane != null && instanceLane.Count > 0 && instanceLane[0].IsMarked())
                {
                    continue;  // 이 레인은 스킵
                }

                // balloons[0]이 활성 풍선 (가장 아래)
                if (lane.Count > 0 && lane[0] == color)
                {
                    // 해당 레인의 첫 번째 풍선 (활성)
                    if (_balloonImages.Count > laneIdx && _balloonImages[laneIdx].Count > 0)
                    {
                        var balloonList = _balloonImages[laneIdx];
                        var activeBalloon = balloonList[0];
                        if (activeBalloon != null)
                        {
                            return activeBalloon.transform.position;
                        }
                    }
                }
            }

            return Vector3.zero;
        }

        // ========== 내부 유틸리티 ==========

        /// <summary>
        /// UI 생성
        /// </summary>
        private void CreateUI()
        {
            _balloonImages.Clear();

            // _lanesContainer에 HorizontalLayoutGroup 설정 (Lane들을 가로로 배열)
            if (_lanesContainer != null)
            {
                // 기존 LayoutGroup 제거 (VerticalLayoutGroup이 있을 수 있음)
                var existingVerticalLayout = _lanesContainer.GetComponent<VerticalLayoutGroup>();
                if (existingVerticalLayout != null)
                {
                    DestroyImmediate(existingVerticalLayout);
                }

                var containerLayout = _lanesContainer.GetComponent<HorizontalLayoutGroup>();
                if (containerLayout == null)
                {
                    containerLayout = _lanesContainer.gameObject.AddComponent<HorizontalLayoutGroup>();
                }

                if (containerLayout != null)
                {
                    containerLayout.spacing = _laneSpacing;
                    containerLayout.childAlignment = TextAnchor.LowerCenter;
                    containerLayout.childForceExpandWidth = false;
                    containerLayout.childForceExpandHeight = false;
                }
            }

            for (int laneIdx = 0; laneIdx < _lanes.Count; laneIdx++)
            {
                // Lane 컨테이너 생성
                GameObject laneObj;
                if (_lanePrefab != null)
                {
                    laneObj = Instantiate(_lanePrefab, _lanesContainer);

                    // 프리팹의 기존 LayoutGroup 제거 후 VerticalLayoutGroup 추가
                    var existingLayout = laneObj.GetComponent<HorizontalLayoutGroup>();
                    if (existingLayout != null)
                    {
                        DestroyImmediate(existingLayout);
                    }

                    var existingVertical = laneObj.GetComponent<VerticalLayoutGroup>();
                    if (existingVertical == null)
                    {
                        var layout = laneObj.AddComponent<VerticalLayoutGroup>();
                        layout.spacing = _balloonSpacing;
                        layout.childAlignment = TextAnchor.LowerCenter;
                        layout.childForceExpandWidth = false;
                        layout.childForceExpandHeight = false;
                        layout.reverseArrangement = true;  // balloons[0]이 하단(활성 위치)에 배치
                    }
                    else
                    {
                        existingVertical.reverseArrangement = true;
                    }

                    // ContentSizeFitter 추가 (Lane 크기를 컨텐츠에 맞춤)
                    var fitter = laneObj.GetComponent<ContentSizeFitter>();
                    if (fitter == null)
                    {
                        fitter = laneObj.AddComponent<ContentSizeFitter>();
                    }
                    fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                    fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

                    // Pivot을 하단으로 설정 (Lane들의 바닥선 정렬을 위해)
                    var rect = laneObj.GetComponent<RectTransform>();
                    if (rect != null)
                    {
                        rect.pivot = new Vector2(0.5f, 0f);  // 하단 중앙
                    }
                }
                else
                {
                    laneObj = new GameObject($"Lane_{laneIdx}");
                    laneObj.transform.SetParent(_lanesContainer, false);
                    var rect = laneObj.AddComponent<RectTransform>();
                    rect.pivot = new Vector2(0.5f, 0f);  // 하단 중앙 (Lane들의 바닥선 정렬)
                    laneObj.transform.localScale = Vector3.one;

                    var layout = laneObj.AddComponent<VerticalLayoutGroup>();
                    layout.spacing = _balloonSpacing;
                    layout.childAlignment = TextAnchor.LowerCenter;
                    layout.childForceExpandWidth = false;
                    layout.childForceExpandHeight = false;
                    layout.reverseArrangement = true;  // balloons[0]이 하단(활성 위치)에 배치

                    // ContentSizeFitter 추가 (Lane 크기를 컨텐츠에 맞춤)
                    var fitter = laneObj.AddComponent<ContentSizeFitter>();
                    fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                    fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                }

                var lane = _lanes[laneIdx];
                var balloonList = new List<Image>();

                // 풍선 생성 (balloons[0]이 활성 풍선, 가장 아래에서 위로 쌓임)
                for (int i = 0; i < lane.Count; i++)
                {
                    var color = lane[i];
                    var balloonObj = CreateBalloon(laneObj.transform, color);
                    var image = balloonObj.GetComponent<Image>();
                    balloonList.Add(image);
                }

                _balloonImages.Add(balloonList);

                // BalloonInstance와 Visual 연결
                if (laneIdx < _balloonInstances.Count)
                {
                    var instanceLane = _balloonInstances[laneIdx];
                    for (int i = 0; i < balloonList.Count && i < instanceLane.Count; i++)
                    {
                        var visual = balloonList[i].GetComponent<BalloonVisual>();
                        if (visual != null)
                        {
                            instanceLane[i].Visual = visual;
                            instanceLane[i].RefreshVisual();
                        }
                    }
                }
            }

            // 레이아웃 강제 업데이트 (LayoutGroup이 위치를 계산하도록)
            Canvas.ForceUpdateCanvases();

            // 각 레인의 기준 위치 및 앵커 정보 저장, LayoutGroup 제거
            _laneBasePositions.Clear();
            _laneAnchorInfos.Clear();
            for (int laneIdx = 0; laneIdx < _balloonImages.Count; laneIdx++)
            {
                if (_balloonImages[laneIdx].Count == 0)
                {
                    _laneBasePositions.Add(Vector2.zero);
                    _laneAnchorInfos.Add(default);
                    continue;
                }

                Transform laneObj = _balloonImages[laneIdx][0]?.transform.parent;
                if (laneObj == null)
                {
                    _laneBasePositions.Add(Vector2.zero);
                    _laneAnchorInfos.Add(default);
                    continue;
                }

                // 첫 번째 풍선(balloons[0])의 anchoredPosition과 앵커 정보를 기준으로 저장
                var firstBalloon = _balloonImages[laneIdx][0];
                var rect = firstBalloon.GetComponent<RectTransform>();
                if (rect != null)
                {
                    _laneBasePositions.Add(rect.anchoredPosition);
                    _laneAnchorInfos.Add(new LaneAnchorInfo
                    {
                        anchorMin = rect.anchorMin,
                        anchorMax = rect.anchorMax,
                        pivot = rect.pivot
                    });
                }
                else
                {
                    _laneBasePositions.Add(Vector2.zero);
                    _laneAnchorInfos.Add(default);
                }

                // VerticalLayoutGroup 제거 (이제 수동 관리)
                var layout = laneObj.GetComponent<VerticalLayoutGroup>();
                if (layout != null)
                {
                    Destroy(layout);
                }

                // ContentSizeFitter도 제거
                var fitter = laneObj.GetComponent<ContentSizeFitter>();
                if (fitter != null)
                {
                    Destroy(fitter);
                }

                // 겹침 모드: 앞 풍선(index 0)이 뒤 풍선을 가리도록 렌더링 순서 조정
                // Unity UI에서 나중에 렌더링되는 것이 위에 표시되므로, index 0을 맨 마지막 sibling으로
                if (_enableOverlap && _balloonImages[laneIdx].Count > 1)
                {
                    for (int i = _balloonImages[laneIdx].Count - 1; i >= 0; i--)
                    {
                        var balloon = _balloonImages[laneIdx][i];
                        if (balloon != null)
                        {
                            balloon.transform.SetAsLastSibling();
                        }
                    }
                }
            }

            // 겹침 모드에서 위치 재계산 (LayoutGroup 기준 위치가 아닌 직접 계산)
            if (_enableOverlap)
            {
                ApplyOverlapPositions();
            }

            UpdateHeadHighlights();

            // 컨베이어 벨트 생성
            CreateConveyorBelts();
        }

        /// <summary>
        /// 풍선 오브젝트 생성
        /// </summary>
        private GameObject CreateBalloon(Transform parent, GameColor color)
        {
            GameObject balloonObj;
            float targetWidth;
            float targetHeight;

            if (_balloonPrefab != null)
            {
                balloonObj = Instantiate(_balloonPrefab);
                balloonObj.transform.SetParent(parent, false);  // worldPositionStays = false

                // _balloonSize 기준으로 크기 적용 (프리팹 비율 유지)
                var prefabRect = balloonObj.GetComponent<RectTransform>();
                if (prefabRect != null)
                {
                    float originalWidth = prefabRect.sizeDelta.x;
                    float originalHeight = prefabRect.sizeDelta.y;

                    if (originalWidth > 0 && originalHeight > 0)
                    {
                        // 비율 유지: _balloonSize를 높이 기준으로 적용
                        float ratio = originalWidth / originalHeight;
                        targetHeight = _balloonSize;
                        targetWidth = _balloonSize * ratio;
                    }
                    else
                    {
                        targetWidth = _balloonSize;
                        targetHeight = _balloonSize;
                    }

                    // RectTransform에 크기 반영
                    prefabRect.sizeDelta = new Vector2(targetWidth, targetHeight);
                }
                else
                {
                    targetWidth = _balloonSize;
                    targetHeight = _balloonSize;
                }
            }
            else
            {
                balloonObj = new GameObject("Balloon");
                balloonObj.transform.SetParent(parent, false);
                balloonObj.AddComponent<Image>();

                // _balloonSize 사용
                targetWidth = _balloonSize;
                targetHeight = _balloonSize;

                var rect = balloonObj.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.sizeDelta = new Vector2(targetWidth, targetHeight);
                }
            }

            // localScale 보정
            balloonObj.transform.localScale = Vector3.one;

            // LayoutElement로 크기 고정
            var layoutElement = balloonObj.GetComponent<LayoutElement>();
            if (layoutElement == null)
            {
                layoutElement = balloonObj.AddComponent<LayoutElement>();
            }
            layoutElement.minWidth = targetWidth;
            layoutElement.minHeight = targetHeight;
            layoutElement.preferredWidth = targetWidth;
            layoutElement.preferredHeight = targetHeight;

            // 색상 설정
            var img = balloonObj.GetComponent<Image>();
            if (img != null)
            {
                img.color = ColorHelper.GetColor(color);
            }

            // BalloonVisual 컴포넌트 추가 (기믹 렌더링용)
            var visual = balloonObj.GetComponent<BalloonVisual>();
            if (visual == null)
            {
                visual = balloonObj.AddComponent<BalloonVisual>();
            }
            visual.SetColor(color);

            return balloonObj;
        }

        /// <summary>
        /// 활성 풍선 강조 (첫 번째 풍선, 가장 아래)
        /// </summary>
        private void UpdateHeadHighlights()
        {
            for (int laneIdx = 0; laneIdx < _balloonImages.Count; laneIdx++)
            {
                var lane = _balloonImages[laneIdx];
                for (int i = 0; i < lane.Count; i++)
                {
                    var balloon = lane[i];
                    if (balloon == null) continue;

                    // 첫 번째(i==0)가 활성 풍선 - 약간 확대
                    if (i == 0)
                    {
                        balloon.transform.localScale = Vector3.one * 1.1f;
                    }
                    else
                    {
                        balloon.transform.localScale = Vector3.one;
                    }
                }
            }
        }

        /// <summary>
        /// 풍선 인덱스에 따른 anchoredPosition 계산
        /// index 0 = 가장 아래 (활성 풍선)
        /// </summary>
        private Vector2 CalculateBalloonPosition(int laneIdx, int balloonIndex)
        {
            if (laneIdx < 0 || laneIdx >= _laneBasePositions.Count)
                return Vector2.zero;

            Vector2 basePos = _laneBasePositions[laneIdx];

            // 겹침이 활성화된 경우, 풍선 간격을 줄여서 겹치게 함
            float effectiveSpacing = _balloonSpacing;
            if (_enableOverlap)
            {
                // 겹침 양만큼 간격 감소 (음수가 될 수 있음)
                effectiveSpacing = _balloonSize - _overlapAmount;
            }

            float yOffset = balloonIndex * effectiveSpacing;
            return new Vector2(basePos.x, basePos.y + yOffset);
        }

        /// <summary>
        /// 겹침 모드에서 모든 풍선 위치 적용
        /// </summary>
        private void ApplyOverlapPositions()
        {
            for (int laneIdx = 0; laneIdx < _balloonImages.Count; laneIdx++)
            {
                var balloonList = _balloonImages[laneIdx];
                for (int i = 0; i < balloonList.Count; i++)
                {
                    var balloon = balloonList[i];
                    if (balloon == null) continue;

                    var rect = balloon.GetComponent<RectTransform>();
                    if (rect != null)
                    {
                        rect.anchoredPosition = CalculateBalloonPosition(laneIdx, i);
                    }
                }
            }
        }

        /// <summary>
        /// 남은 풍선들의 슬라이드 다운 애니메이션
        /// 각 풍선을 새 인덱스 위치로 애니메이션
        /// </summary>
        private void AnimateRemainingBalloons(List<Image> balloons, Image poppedBalloon, int laneIdx)
        {
            if (balloons == null || balloons.Count == 0) return;

            // 각 풍선을 새 인덱스(i)에 해당하는 목표 위치로 애니메이션
            for (int i = 0; i < balloons.Count; i++)
            {
                var balloon = balloons[i];
                if (balloon == null) continue;

                var rect = balloon.GetComponent<RectTransform>();
                if (rect == null) continue;

                Vector2 startPos = rect.anchoredPosition;
                // 새 인덱스(i)에 해당하는 목표 위치 계산
                Vector2 endPos = CalculateBalloonPosition(laneIdx, i);

                // 슬라이드 코루틴 시작
                StartCoroutine(SlideBalloonCoroutine(rect, startPos, endPos));
            }

            // 컨베이어 벨트 애니메이션 재생
            PlayConveyorAnimation(laneIdx);
        }

        /// <summary>
        /// 개별 풍선 슬라이드 애니메이션 코루틴
        /// LayoutGroup이 제거되었으므로 ignoreLayout 불필요
        /// </summary>
        private System.Collections.IEnumerator SlideBalloonCoroutine(RectTransform rect, Vector2 from, Vector2 to)
        {
            if (rect == null) yield break;

            float elapsed = 0f;

            while (elapsed < _slideAnimDuration)
            {
                if (rect == null) yield break;

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / _slideAnimDuration);

                // Ease Out Cubic - 부드러운 감속
                float easedT = 1f - Mathf.Pow(1f - t, 3f);

                rect.anchoredPosition = Vector2.Lerp(from, to, easedT);
                yield return null;
            }

            if (rect != null)
            {
                rect.anchoredPosition = to;
            }
        }

        /// <summary>
        /// 팝 애니메이션
        /// </summary>
        private void AnimatePop(GameObject balloon)
        {
            // 간단한 팝 애니메이션 (스케일 업 후 사라짐)
            StartCoroutine(PopCoroutine(balloon));
        }

        private System.Collections.IEnumerator PopCoroutine(GameObject balloon)
        {
            float duration = 0.2f;
            float elapsed = 0f;
            var startScale = balloon.transform.localScale;

            // 파티클용 색상 미리 저장 (페이드 전)
            Color balloonColor = Color.white;
            var image = balloon.GetComponent<Image>();
            if (image != null)
            {
                balloonColor = image.color;
            }

            // 파티클 생성 위치 저장
            Vector3 popPosition = balloon.transform.position;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // 스케일 업 후 다운
                float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.5f;
                balloon.transform.localScale = startScale * scale;

                // 페이드 아웃
                if (image != null)
                {
                    var color = balloonColor;
                    color.a = 1f - t;
                    image.color = color;
                }

                yield return null;
            }

            // 파티클 이펙트 생성
            SpawnPopEffect(popPosition, balloonColor);

            Destroy(balloon);
            // 슬라이드 애니메이션이 위치 이동을 처리하므로
            // LayoutRebuilder 즉시 갱신은 제거됨
        }

        /// <summary>
        /// 풍선 팝 파티클 이펙트 생성
        /// </summary>
        private void SpawnPopEffect(Vector3 position, Color color)
        {
            ParticleSystem effect;

            if (_popEffectPrefab != null)
            {
                effect = Instantiate(_popEffectPrefab, position, Quaternion.identity);
            }
            else if (_autoCreatePopEffect)
            {
                effect = CreatePopParticle(position);
            }
            else
            {
                return;
            }

            // 풍선 색상에 맞춰 파티클 색상 설정 (알파값 보장 + 색상 변화 추가)
            var main = effect.main;

            // 원본 색상 (알파 1.0 보장)
            Color baseColor = new Color(color.r, color.g, color.b, 1f);

            // 밝은 버전 (하이라이트)
            Color brightColor = Color.Lerp(baseColor, Color.white, 0.3f);
            brightColor.a = 1f;

            // 두 색상 사이에서 랜덤하게 선택되도록 그라디언트 설정
            var colorGradient = new ParticleSystem.MinMaxGradient(baseColor, brightColor);
            main.startColor = colorGradient;

            effect.Play();

            // 재생 완료 후 자동 삭제
            Destroy(effect.gameObject, _popEffectDuration);
        }

        /// <summary>
        /// 풍선 팝 파티클 시스템 동적 생성
        /// </summary>
        private ParticleSystem CreatePopParticle(Vector3 position)
        {
            GameObject particleGO = new GameObject("BalloonPopEffect");
            particleGO.transform.position = position;

            ParticleSystem ps = particleGO.AddComponent<ParticleSystem>();

            // ParticleSystem은 추가 시 자동 재생됨 - 설정 전에 먼저 정지
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // Main Module
            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            main.gravityModifier = 0.5f;
            main.maxParticles = 30;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            // Emission (Burst)
            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[]
            {
                new ParticleSystem.Burst(0f, 15, 20)
            });

            // Shape (Sphere - 풍선 터지듯 사방으로)
            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.3f;

            // Color over Lifetime (페이드 아웃)
            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient fadeGradient = new Gradient();
            fadeGradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = fadeGradient;

            // Size over Lifetime (점점 작아짐)
            var sizeOverLifetime = ps.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 1f);
            sizeCurve.AddKey(1f, 0.3f);
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // Renderer
            var renderer = particleGO.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;

                // 파티클 Material 설정 (URP/Built-in 호환)
                Material particleMaterial = null;

                // URP용 셰이더 우선 시도
                string[] shaderNames = new string[]
                {
                    "Universal Render Pipeline/Particles/Unlit",
                    "Universal Render Pipeline/Particles/Simple Lit",
                    "Particles/Standard Unlit",
                    "Legacy Shaders/Particles/Alpha Blended"
                };

                foreach (var shaderName in shaderNames)
                {
                    var shader = Shader.Find(shaderName);
                    if (shader != null)
                    {
                        particleMaterial = new Material(shader);
                        break;
                    }
                }

                if (particleMaterial != null)
                {
                    renderer.material = particleMaterial;
                }
            }

            return ps;
        }

        // ========== 부스터 지원 메서드 ==========

        /// <summary>
        /// Triple Arrow용 유효 타겟 풍선 목록 반환
        /// 모든 레인의 모든 풍선 중 Marked 상태가 아닌 풍선을 랜덤하게 선택
        /// </summary>
        /// <param name="maxCount">최대 선택 개수 (기본 3)</param>
        /// <returns>랜덤하게 선택된 유효 타겟 풍선 목록</returns>
        public List<BalloonInstance> GetValidTargetsForTripleArrow(int maxCount = 3)
        {
            var validTargets = new List<BalloonInstance>();

            // 모든 레인의 모든 풍선 수집
            foreach (var lane in _balloonInstances)
            {
                foreach (var balloon in lane)
                {
                    // Marked 풍선 제외 (Connected 기믹)
                    if (balloon.IsMarked()) continue;

                    validTargets.Add(balloon);
                }
            }

            // 셔플
            ShuffleList(validTargets);

            // 최대 개수만큼 반환
            if (validTargets.Count > maxCount)
            {
                return validTargets.GetRange(0, maxCount);
            }

            return validTargets;
        }

        /// <summary>
        /// 리스트 셔플 (Fisher-Yates 알고리즘)
        /// </summary>
        private void ShuffleList<T>(List<T> list)
        {
            int n = list.Count;
            for (int i = n - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                T temp = list[i];
                list[i] = list[j];
                list[j] = temp;
            }
        }

        /// <summary>
        /// 활성 풍선 색상 목록 반환 (Hint용)
        /// 각 레인의 첫 번째 풍선(활성) 색상들
        /// Connected 기믹의 Marked 풍선은 제외
        /// </summary>
        public List<GameColor> GetActiveBalloonColors()
        {
            var colors = new List<GameColor>();

            for (int laneIdx = 0; laneIdx < _lanes.Count; laneIdx++)
            {
                var lane = _lanes[laneIdx];
                var instanceLane = _balloonInstances.Count > laneIdx ? _balloonInstances[laneIdx] : null;

                // Connected 기믹: Marked 풍선은 Target에서 제외
                if (instanceLane != null && instanceLane.Count > 0 && instanceLane[0].IsMarked())
                {
                    continue;  // 이 레인은 스킵
                }

                if (lane.Count > 0)
                {
                    colors.Add(lane[0]);
                }
            }

            return colors;
        }

        /// <summary>
        /// 풍선 복원 (Undo용)
        /// 지정된 레인의 맨 앞에 풍선 추가
        /// </summary>
        public void RestoreBalloon(GameColor color, int laneIndex)
        {
            if (laneIndex < 0 || laneIndex >= _lanes.Count)
            {
                Debug.LogWarning($"[QueueUI] Invalid lane index for restore: {laneIndex}");
                return;
            }

            // 데이터 복원 - 맨 앞에 삽입
            _lanes[laneIndex].Insert(0, color);

            // UI 복원
            if (_balloonImages.Count > laneIndex)
            {
                var balloonList = _balloonImages[laneIndex];

                // 레인 컨테이너 찾기
                Transform laneContainer = null;
                if (balloonList.Count > 0 && balloonList[0] != null)
                {
                    laneContainer = balloonList[0].transform.parent;
                }
                else if (_lanesContainer != null && _lanesContainer.childCount > laneIndex)
                {
                    laneContainer = _lanesContainer.GetChild(laneIndex);
                }

                if (laneContainer == null)
                {
                    Debug.LogWarning("[QueueUI] Could not find lane container for restore");
                    return;
                }

                // 새 풍선 생성
                var balloonObj = CreateBalloon(laneContainer, color);
                var image = balloonObj.GetComponent<Image>();

                // LayoutGroup이 설정한 앵커/피벗을 복원 (_laneBasePositions와 좌표계 일치 필요)
                var newRect = balloonObj.GetComponent<RectTransform>();
                if (newRect != null)
                {
                    if (laneIndex < _laneAnchorInfos.Count)
                    {
                        var anchorInfo = _laneAnchorInfos[laneIndex];
                        newRect.anchorMin = anchorInfo.anchorMin;
                        newRect.anchorMax = anchorInfo.anchorMax;
                        newRect.pivot = anchorInfo.pivot;
                    }
                    else if (balloonList.Count > 0 && balloonList[0] != null)
                    {
                        // 폴백: 기존 풍선에서 복사
                        var existingRect = balloonList[0].GetComponent<RectTransform>();
                        if (existingRect != null)
                        {
                            newRect.anchorMin = existingRect.anchorMin;
                            newRect.anchorMax = existingRect.anchorMax;
                            newRect.pivot = existingRect.pivot;
                        }
                    }

                    // 초기 위치를 활성 풍선 위치 아래로 설정 (등장 전 숨김)
                    Vector2 targetPos = CalculateBalloonPosition(laneIndex, 0);
                    newRect.anchoredPosition = targetPos - new Vector2(0, _balloonSize);
                }

                // 목록 맨 앞에 삽입
                balloonList.Insert(0, image);

                // 렌더링 순서 조정 (앞 풍선이 위에 표시)
                if (_enableOverlap)
                {
                    image.transform.SetAsLastSibling();
                }

                // 기존 풍선들 위치 조정 (위로 이동)
                AnimateBalloonsAfterRestore(balloonList, laneIndex);

                Debug.Log($"[QueueUI] Balloon restored: Color={color}, Lane={laneIndex}");
            }

            UpdateHeadHighlights();
        }

        /// <summary>
        /// 풍선 복원 (BalloonSnapshot 사용, 기믹 상태 포함)
        /// </summary>
        public void RestoreBalloon(BalloonSnapshot snapshot)
        {
            if (snapshot == null) return;

            int laneIndex = snapshot.LaneIndex;
            GameColor color = snapshot.Color;

            // 기존 복원 로직 수행
            RestoreBalloon(color, laneIndex);

            // BalloonInstance 복원
            if (laneIndex >= 0 && laneIndex < _balloonInstances.Count)
            {
                var instanceLane = _balloonInstances[laneIndex];

                // 새 BalloonData 생성 (기믹 스냅샷 포함)
                var balloonData = new BalloonData(ColorHelper.ToString(color));
                if (snapshot.GimmickSnapshots != null)
                {
                    foreach (var gimmickSnapshot in snapshot.GimmickSnapshots)
                    {
                        balloonData.AddGimmick(gimmickSnapshot.Clone());
                    }
                }

                // BalloonInstance 생성 및 복원
                var instance = new BalloonInstance(laneIndex, 0, balloonData);

                // 기믹 상태 복원 알림
                if (snapshot.GimmickSnapshots != null && snapshot.GimmickSnapshots.Count > 0)
                {
                    instance.NotifyRestore(snapshot.GimmickSnapshots);
                }

                // 맨 앞에 삽입
                instanceLane.Insert(0, instance);

                // 다른 인스턴스들의 위치 업데이트
                for (int i = 1; i < instanceLane.Count; i++)
                {
                    instanceLane[i].PositionInLane = i;
                }

                // Visual 연결
                if (_balloonImages.Count > laneIndex && _balloonImages[laneIndex].Count > 0)
                {
                    var image = _balloonImages[laneIndex][0];
                    var visual = image.GetComponent<BalloonVisual>();
                    if (visual == null)
                    {
                        visual = image.gameObject.AddComponent<BalloonVisual>();
                    }
                    instance.Visual = visual;
                    instance.RefreshVisual();
                }

                // 활성 풍선 알림
                instance.NotifyBecomeActive();

                Debug.Log($"[QueueUI] Balloon restored with gimmicks: Color={color}, Lane={laneIndex}, Gimmicks={snapshot.GimmickSnapshots?.Count ?? 0}");
            }
        }

        /// <summary>
        /// Partial hit 복원 (Number 기믹 등)
        /// </summary>
        public void RestorePartialHit(int laneIndex, List<GimmickInstanceData> gimmickSnapshots)
        {
            if (laneIndex < 0 || laneIndex >= _balloonInstances.Count)
                return;

            var instanceLane = _balloonInstances[laneIndex];
            if (instanceLane.Count == 0)
                return;

            var instance = instanceLane[0];
            instance.NotifyRestore(gimmickSnapshots);
        }

        /// <summary>
        /// 현재 활성 풍선의 스냅샷 생성
        /// </summary>
        public BalloonSnapshot CreateActiveBalloonSnapshot(int laneIndex)
        {
            if (laneIndex < 0 || laneIndex >= _balloonInstances.Count)
                return null;

            var instanceLane = _balloonInstances[laneIndex];
            if (instanceLane.Count == 0)
                return null;

            return BalloonSnapshot.CreateFromInstance(instanceLane[0]);
        }

        /// <summary>
        /// 복원 후 풍선들 위치 애니메이션
        /// </summary>
        private void AnimateBalloonsAfterRestore(List<Image> balloons, int laneIdx)
        {
            if (balloons == null || balloons.Count == 0) return;

            for (int i = 0; i < balloons.Count; i++)
            {
                var balloon = balloons[i];
                if (balloon == null) continue;

                var rect = balloon.GetComponent<RectTransform>();
                if (rect == null) continue;

                // 새로운 목표 위치 계산
                Vector2 targetPos = CalculateBalloonPosition(laneIdx, i);

                // 현재 위치에서 목표 위치로 애니메이션
                // (새 풍선은 RestoreBalloon에서 이미 아래쪽에 배치됨)
                Vector2 currentPos = rect.anchoredPosition;
                StartCoroutine(SlideBalloonCoroutine(rect, currentPos, targetPos));
            }

            // 컨베이어 벨트 애니메이션 역재생 (Undo)
            PlayConveyorAnimation(laneIdx, reverse: true);
        }

        /// <summary>
        /// 특정 색상의 활성 풍선이 있는 레인 인덱스 반환
        /// Connected 기믹의 Marked 풍선은 제외
        /// </summary>
        public int FindLaneWithActiveBalloon(GameColor color)
        {
            for (int laneIdx = 0; laneIdx < _lanes.Count; laneIdx++)
            {
                var lane = _lanes[laneIdx];
                var instanceLane = _balloonInstances.Count > laneIdx ? _balloonInstances[laneIdx] : null;

                // Connected 기믹: Marked 풍선은 Target에서 제외
                if (instanceLane != null && instanceLane.Count > 0 && instanceLane[0].IsMarked())
                {
                    continue;  // 이 레인은 스킵
                }

                if (lane.Count > 0 && lane[0] == color)
                {
                    return laneIdx;
                }
            }
            return -1;
        }

        // ========== 컨베이어 벨트 ==========

        /// <summary>
        /// 각 레인 뒤에 컨베이어 벨트 타일을 생성.
        /// 프리팹이 SpriteRenderer 기반이면 UI Image + 브릿지 스크립트로 변환.
        /// </summary>
        private void CreateConveyorBelts()
        {
            if (_conveyorTilePrefab == null) return;

            _conveyorBelts.Clear();

            // 프리팹이 SpriteRenderer 기반인지 확인
            bool isSpriteRendererBased = _conveyorTilePrefab.GetComponent<SpriteRenderer>() != null
                && _conveyorTilePrefab.GetComponent<RectTransform>() == null;

            // 타일 크기 결정 (스케일 적용 전 원본 크기)
            float rawTileWidth, rawTileHeight;
            if (isSpriteRendererBased)
            {
                var sr = _conveyorTilePrefab.GetComponent<SpriteRenderer>();
                if (sr != null && sr.sprite != null)
                {
                    rawTileWidth = sr.sprite.rect.width;
                    rawTileHeight = sr.sprite.rect.height;
                }
                else
                {
                    rawTileWidth = GetBalloonWidth();
                    rawTileHeight = _balloonSize;
                }
            }
            else
            {
                var tilePrefabRect = _conveyorTilePrefab.GetComponent<RectTransform>();
                rawTileWidth = tilePrefabRect != null ? tilePrefabRect.sizeDelta.x : GetBalloonWidth();
                rawTileHeight = tilePrefabRect != null ? tilePrefabRect.sizeDelta.y : _balloonSize;
            }

            // 스케일 적용된 실제 크기
            float tileWidth = rawTileWidth * _conveyorTileScale;
            float tileHeight = rawTileHeight * _conveyorTileScale;

            // Animator Controller 참조 (SpriteRenderer 기반 프리팹용)
            RuntimeAnimatorController animController = null;
            if (isSpriteRendererBased)
            {
                var prefabAnimator = _conveyorTilePrefab.GetComponent<Animator>();
                if (prefabAnimator != null)
                {
                    animController = prefabAnimator.runtimeAnimatorController;
                }
            }

            for (int laneIdx = 0; laneIdx < _balloonImages.Count; laneIdx++)
            {
                if (_balloonImages[laneIdx].Count == 0) continue;

                Transform laneTransform = _balloonImages[laneIdx][0]?.transform.parent;
                if (laneTransform == null) continue;

                // 벨트 루트 컨테이너 생성
                var beltRoot = new GameObject($"ConveyorBelt_{laneIdx}");
                beltRoot.transform.SetParent(laneTransform, false);
                beltRoot.transform.SetAsFirstSibling();  // 풍선 뒤에 렌더링

                var beltRect = beltRoot.AddComponent<RectTransform>();
                beltRect.anchorMin = new Vector2(0.5f, 0f);
                beltRect.anchorMax = new Vector2(0.5f, 0f);
                beltRect.pivot = new Vector2(0.5f, 0f);
                beltRect.anchoredPosition = Vector2.zero;
                beltRect.sizeDelta = new Vector2(tileWidth, tileHeight * _conveyorTileCount);

                // N개 타일 세로로 쌓기
                for (int t = 0; t < _conveyorTileCount; t++)
                {
                    GameObject tile;

                    if (isSpriteRendererBased)
                    {
                        // SpriteRenderer 프리팹 → UI 타일로 변환 (원본 크기 전달, 스케일은 localScale로)
                        tile = CreateUIConveyorTile(beltRoot.transform, animController, rawTileWidth, rawTileHeight);
                    }
                    else
                    {
                        // UI 기반 프리팹 → 그대로 사용
                        tile = Instantiate(_conveyorTilePrefab);
                        tile.transform.SetParent(beltRoot.transform, false);
                    }

                    tile.transform.localScale = Vector3.one * _conveyorTileScale;

                    var tileRect = tile.GetComponent<RectTransform>();
                    if (tileRect != null)
                    {
                        tileRect.anchorMin = new Vector2(0.5f, 0f);
                        tileRect.anchorMax = new Vector2(0.5f, 0f);
                        tileRect.pivot = new Vector2(0.5f, 0.5f);  // 중앙 피벗 (회전 기준점)
                        // 중앙 피벗이므로 tileHeight/2 만큼 위로 오프셋
                        tileRect.anchoredPosition = new Vector2(0, t * tileHeight + tileHeight * 0.5f);
                    }

                    // raycast 차단 방지
                    var uiImages = tile.GetComponentsInChildren<Image>();
                    foreach (var img in uiImages) img.raycastTarget = false;
                }

                _conveyorBelts.Add(beltRoot);
            }
        }

        /// <summary>
        /// SpriteRenderer 프리팹을 기반으로 UI 컨베이어 타일 생성.
        /// Animator → SpriteRenderer.sprite → Image.sprite 브릿지 방식.
        /// </summary>
        private GameObject CreateUIConveyorTile(Transform parent, RuntimeAnimatorController animController, float width, float height)
        {
            var tile = new GameObject("ConveyorTile");
            tile.transform.SetParent(parent, false);

            // RectTransform 설정
            var rect = tile.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, height);
            rect.pivot = new Vector2(0.5f, 0.5f);  // 중앙 피벗 (회전 기준점)

            // Z축 -90도 회전 (화살표가 아래 방향을 바라보도록)
            rect.localEulerAngles = new Vector3(0, 0, -90);

            // Image 컴포넌트 (Canvas에서 렌더링)
            var image = tile.AddComponent<Image>();
            image.raycastTarget = false;

            // 초기 스프라이트 설정
            var prefabSR = _conveyorTilePrefab.GetComponent<SpriteRenderer>();
            if (prefabSR != null && prefabSR.sprite != null)
            {
                image.sprite = prefabSR.sprite;
            }

            // SpriteRenderer 추가 (Animator가 이 컴포넌트의 sprite를 애니메이션)
            var sr = tile.AddComponent<SpriteRenderer>();
            sr.enabled = false;  // Canvas에서는 렌더링 불필요
            if (prefabSR != null)
            {
                sr.sprite = prefabSR.sprite;
            }

            // Animator 추가 (초기 정지 상태)
            if (animController != null)
            {
                var animator = tile.AddComponent<Animator>();
                animator.runtimeAnimatorController = animController;
                animator.speed = 0f;  // 풍선 POP/Undo 시에만 재생
            }

            // 브릿지: SpriteRenderer.sprite → Image.sprite 매 프레임 동기화
            tile.AddComponent<SpriteRendererToImage>();

            return tile;
        }

        /// <summary>
        /// 풍선 프리팹의 가로 크기
        /// </summary>
        private float GetBalloonWidth()
        {
            if (_balloonPrefab != null)
            {
                var prefabRect = _balloonPrefab.GetComponent<RectTransform>();
                if (prefabRect != null) return prefabRect.sizeDelta.x;
            }
            return _balloonSize;
        }

        /// <summary>
        /// 풍선 프리팹의 세로 크기
        /// </summary>
        private float GetBalloonHeight()
        {
            if (_balloonPrefab != null)
            {
                var prefabRect = _balloonPrefab.GetComponent<RectTransform>();
                if (prefabRect != null) return prefabRect.sizeDelta.y;
            }
            return _balloonSize;
        }



        /// <summary>
        /// 특정 레인의 컨베이어 벨트 애니메이션 재생.
        /// _slideAnimDuration 후 자동 정지.
        /// </summary>
        /// <param name="laneIdx">레인 인덱스</param>
        /// <param name="reverse">true면 역재생 (Undo 시)</param>
        private void PlayConveyorAnimation(int laneIdx, bool reverse = false)
        {
            if (laneIdx < 0 || laneIdx >= _conveyorBelts.Count) return;

            var beltRoot = _conveyorBelts[laneIdx];
            if (beltRoot == null) return;

            var animators = beltRoot.GetComponentsInChildren<Animator>();
            if (animators.Length == 0) return;

            if (reverse)
            {
                // PPtrCurve(스프라이트 교체) 애니메이션은 음수 speed로 역재생이 안 됨
                // normalizedTime을 직접 감소시켜 역재생 구현
                StartCoroutine(PlayConveyorReverseCoroutine(animators, _slideAnimDuration));
            }
            else
            {
                foreach (var anim in animators)
                {
                    anim.speed = _conveyorAnimSpeed;
                }
                StartCoroutine(StopConveyorAfterDelay(animators, _slideAnimDuration));
            }
        }

        private System.Collections.IEnumerator PlayConveyorReverseCoroutine(Animator[] animators, float duration)
        {
            // 현재 normalizedTime과 클립 길이 캡처
            float[] startNormTimes = new float[animators.Length];
            int[] stateHashes = new int[animators.Length];
            float clipLength = 0f;

            for (int i = 0; i < animators.Length; i++)
            {
                var info = animators[i].GetCurrentAnimatorStateInfo(0);
                startNormTimes[i] = info.normalizedTime % 1f;
                stateHashes[i] = info.fullPathHash;
                if (clipLength <= 0f) clipLength = info.length;
            }

            if (clipLength <= 0f) clipLength = 1f;

            // duration 동안 역방향으로 이동할 normalizedTime 양
            float reverseNormAmount = (_conveyorAnimSpeed * duration) / clipLength;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Ease Out Cubic (풍선 슬라이드와 동일)
                float easedT = 1f - Mathf.Pow(1f - t, 3f);

                for (int i = 0; i < animators.Length; i++)
                {
                    if (animators[i] == null) continue;

                    float newNormTime = startNormTimes[i] - reverseNormAmount * easedT;
                    // 루프 애니메이션이므로 음수면 래핑
                    while (newNormTime < 0f) newNormTime += 1f;

                    animators[i].Play(stateHashes[i], 0, newNormTime);
                    animators[i].speed = 0f;  // 수동 제어 유지
                }

                yield return null;
            }
        }

        private System.Collections.IEnumerator StopConveyorAfterDelay(Animator[] animators, float delay)
        {
            yield return new WaitForSeconds(delay);

            foreach (var anim in animators)
            {
                if (anim != null)
                {
                    anim.speed = 0f;
                }
            }
        }

        /// <summary>
        /// UI 정리
        /// </summary>
        private void Clear()
        {
            // Connected 실타래 렌더러 정리
            ConnectedLineRenderer.ClearAll();

            foreach (var lane in _balloonImages)
            {
                foreach (var balloon in lane)
                {
                    if (balloon != null)
                    {
                        Destroy(balloon.gameObject);
                    }
                }
            }
            _balloonImages.Clear();
            _lanes.Clear();
            _conveyorBelts.Clear();
            // 컨테이너 하위 오브젝트 즉시 정리
            // DestroyImmediate 사용: Destroy()는 프레임 끝까지 지연되어
            // CreateUI()에서 LayoutGroup이 old+new 자식을 모두 계산하는 문제 방지
            if (_lanesContainer != null)
            {
                for (int i = _lanesContainer.childCount - 1; i >= 0; i--)
                {
                    DestroyImmediate(_lanesContainer.GetChild(i).gameObject);
                }
            }
        }
    }
}