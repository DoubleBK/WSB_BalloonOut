using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BalloonOut.Core;
using BalloonOut.Data;

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

        // ========== 내부 상태 변수 ==========
        private List<List<Image>> _balloonImages = new List<List<Image>>();
        private List<List<GameColor>> _lanes = new List<List<GameColor>>();

        // 각 레인의 풍선 기준 위치 (balloons[0]의 anchoredPosition)
        private List<Vector2> _laneBasePositions = new List<Vector2>();

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

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// Queue 초기화
        /// </summary>
        public void Initialize(List<LaneData> lanesData)
        {
            Clear();

            _lanes = new List<List<GameColor>>();
            foreach (var lane in lanesData)
            {
                _lanes.Add(lane.GetColors());
            }

            CreateUI();
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
            poppedLaneIndex = -1;

            for (int laneIdx = 0; laneIdx < _lanes.Count; laneIdx++)
            {
                var lane = _lanes[laneIdx];
                // balloons[0]이 활성 풍선 (가장 아래)
                if (lane.Count > 0 && lane[0] == color)
                {
                    // 첫 번째 풍선 팝
                    lane.RemoveAt(0);
                    poppedLaneIndex = laneIdx;

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

                    UpdateHeadHighlights();
                    return true;
                }
            }

            return false;
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
        /// </summary>
        public Vector3 GetBalloonWorldPosition(GameColor color)
        {
            for (int laneIdx = 0; laneIdx < _lanes.Count; laneIdx++)
            {
                var lane = _lanes[laneIdx];
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
            }

            // 레이아웃 강제 업데이트 (LayoutGroup이 위치를 계산하도록)
            Canvas.ForceUpdateCanvases();

            // 각 레인의 기준 위치 저장 및 LayoutGroup 제거
            _laneBasePositions.Clear();
            for (int laneIdx = 0; laneIdx < _balloonImages.Count; laneIdx++)
            {
                if (_balloonImages[laneIdx].Count == 0)
                {
                    _laneBasePositions.Add(Vector2.zero);
                    continue;
                }

                Transform laneObj = _balloonImages[laneIdx][0]?.transform.parent;
                if (laneObj == null)
                {
                    _laneBasePositions.Add(Vector2.zero);
                    continue;
                }

                // 첫 번째 풍선(balloons[0])의 anchoredPosition을 기준으로 저장
                var firstBalloon = _balloonImages[laneIdx][0];
                var rect = firstBalloon.GetComponent<RectTransform>();
                if (rect != null)
                {
                    _laneBasePositions.Add(rect.anchoredPosition);
                }
                else
                {
                    _laneBasePositions.Add(Vector2.zero);
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

                // 프리팹의 원본 크기 사용
                var prefabRect = balloonObj.GetComponent<RectTransform>();
                if (prefabRect != null)
                {
                    targetWidth = prefabRect.sizeDelta.x;
                    targetHeight = prefabRect.sizeDelta.y;

                    // 앵커를 부모 하단 중앙으로 명시적 설정
                    // (LayoutGroup 제거 후 RestoreBalloon 시점에 올바른 위치 보장)
                    prefabRect.anchorMin = new Vector2(0.5f, 0f);
                    prefabRect.anchorMax = new Vector2(0.5f, 0f);
                    prefabRect.pivot = new Vector2(0.5f, 0f);
                    prefabRect.anchoredPosition = Vector2.zero;  // 앵커 변경 후 위치 리셋
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

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;

                // 스케일 업 후 다운
                float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.5f;
                balloon.transform.localScale = startScale * scale;

                // 페이드 아웃
                var image = balloon.GetComponent<Image>();
                if (image != null)
                {
                    var color = image.color;
                    color.a = 1f - t;
                    image.color = color;
                }

                yield return null;
            }

            Destroy(balloon);
            // 슬라이드 애니메이션이 위치 이동을 처리하므로
            // LayoutRebuilder 즉시 갱신은 제거됨
        }

        // ========== 부스터 지원 메서드 ==========

        /// <summary>
        /// 활성 풍선 색상 목록 반환 (Hint용)
        /// 각 레인의 첫 번째 풍선(활성) 색상들
        /// </summary>
        public List<GameColor> GetActiveBalloonColors()
        {
            var colors = new List<GameColor>();

            foreach (var lane in _lanes)
            {
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

                // 초기 위치를 활성 풍선 위치 아래로 설정 (등장 전 숨김)
                var newRect = balloonObj.GetComponent<RectTransform>();
                if (newRect != null)
                {
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
        }

        /// <summary>
        /// 특정 색상의 활성 풍선이 있는 레인 인덱스 반환
        /// </summary>
        public int FindLaneWithActiveBalloon(GameColor color)
        {
            for (int laneIdx = 0; laneIdx < _lanes.Count; laneIdx++)
            {
                var lane = _lanes[laneIdx];
                if (lane.Count > 0 && lane[0] == color)
                {
                    return laneIdx;
                }
            }
            return -1;
        }

        /// <summary>
        /// UI 정리
        /// </summary>
        private void Clear()
        {
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

            // 컨테이너 하위 오브젝트 정리
            if (_lanesContainer != null)
            {
                foreach (Transform child in _lanesContainer)
                {
                    Destroy(child.gameObject);
                }
            }
        }
    }
}