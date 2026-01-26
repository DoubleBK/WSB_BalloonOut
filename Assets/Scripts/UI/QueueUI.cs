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
        [SerializeField] private float _balloonSpacing = 10f;
        [SerializeField] private float _laneSpacing = 40f;

        // ========== 내부 상태 변수 ==========
        private List<List<Image>> _balloonImages = new List<List<Image>>();
        private List<List<GameColor>> _lanes = new List<List<GameColor>>();

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
            for (int laneIdx = 0; laneIdx < _lanes.Count; laneIdx++)
            {
                var lane = _lanes[laneIdx];
                // balloons[0]이 활성 풍선 (가장 아래)
                if (lane.Count > 0 && lane[0] == color)
                {
                    // 첫 번째 풍선 팝
                    lane.RemoveAt(0);

                    // UI 업데이트
                    if (_balloonImages.Count > laneIdx && _balloonImages[laneIdx].Count > 0)
                    {
                        var balloonList = _balloonImages[laneIdx];
                        var firstBalloon = balloonList[0];
                        balloonList.RemoveAt(0);

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
                        layout.reverseArrangement = true;  // balloons[0]이 아래에 표시되도록
                    }
                    else
                    {
                        existingVertical.reverseArrangement = true;
                    }
                }
                else
                {
                    laneObj = new GameObject($"Lane_{laneIdx}");
                    laneObj.transform.SetParent(_lanesContainer, false);
                    laneObj.AddComponent<RectTransform>();
                    laneObj.transform.localScale = Vector3.one;
                    var layout = laneObj.AddComponent<VerticalLayoutGroup>();
                    layout.spacing = _balloonSpacing;
                    layout.childAlignment = TextAnchor.LowerCenter;
                    layout.childForceExpandWidth = false;
                    layout.childForceExpandHeight = false;
                    layout.reverseArrangement = true;  // balloons[0]이 아래에 표시되도록
                }

                var lane = _lanes[laneIdx];
                var balloonList = new List<Image>();

                // 풍선 생성 (balloons[0]이 활성 풍선, reverseArrangement로 인해 아래에 표시)
                for (int i = 0; i < lane.Count; i++)
                {
                    var color = lane[i];
                    var balloonObj = CreateBalloon(laneObj.transform, color);
                    var image = balloonObj.GetComponent<Image>();
                    balloonList.Add(image);
                }

                _balloonImages.Add(balloonList);
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
                balloonObj = Instantiate(_balloonPrefab, parent);

                // 프리팹의 원본 크기 사용
                var prefabRect = balloonObj.GetComponent<RectTransform>();
                if (prefabRect != null)
                {
                    targetWidth = prefabRect.sizeDelta.x;
                    targetHeight = prefabRect.sizeDelta.y;
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