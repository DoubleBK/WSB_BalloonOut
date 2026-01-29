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