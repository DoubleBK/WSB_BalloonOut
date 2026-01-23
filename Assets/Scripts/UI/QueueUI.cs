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
                if (lane.Count > 0 && lane[lane.Count - 1] == color)
                {
                    // 마지막 풍선 팝
                    lane.RemoveAt(lane.Count - 1);

                    // UI 업데이트
                    if (_balloonImages.Count > laneIdx && _balloonImages[laneIdx].Count > 0)
                    {
                        var balloonList = _balloonImages[laneIdx];
                        var lastBalloon = balloonList[balloonList.Count - 1];
                        balloonList.RemoveAt(balloonList.Count - 1);

                        // 팝 애니메이션
                        AnimatePop(lastBalloon.gameObject);
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

        // ========== 내부 유틸리티 ==========

        /// <summary>
        /// UI 생성
        /// </summary>
        private void CreateUI()
        {
            _balloonImages.Clear();

            for (int laneIdx = 0; laneIdx < _lanes.Count; laneIdx++)
            {
                // Lane 컨테이너 생성
                GameObject laneObj;
                if (_lanePrefab != null)
                {
                    laneObj = Instantiate(_lanePrefab, _lanesContainer);
                }
                else
                {
                    laneObj = new GameObject($"Lane_{laneIdx}");
                    laneObj.transform.SetParent(_lanesContainer);
                    var layout = laneObj.AddComponent<HorizontalLayoutGroup>();
                    layout.spacing = _balloonSpacing;
                    layout.childAlignment = TextAnchor.MiddleCenter;
                    layout.childForceExpandWidth = false;
                    layout.childForceExpandHeight = false;
                }

                var lane = _lanes[laneIdx];
                var balloonList = new List<Image>();

                // 풍선 생성 (뒤에서부터 표시, 마지막이 Head)
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

            if (_balloonPrefab != null)
            {
                balloonObj = Instantiate(_balloonPrefab, parent);
            }
            else
            {
                balloonObj = new GameObject("Balloon");
                balloonObj.transform.SetParent(parent);

                var image = balloonObj.AddComponent<Image>();
                var rect = balloonObj.GetComponent<RectTransform>();
                rect.sizeDelta = new Vector2(_balloonSize, _balloonSize);
            }

            // 색상 설정
            var img = balloonObj.GetComponent<Image>();
            if (img != null)
            {
                img.color = ColorHelper.GetColor(color);
            }

            return balloonObj;
        }

        /// <summary>
        /// Head 풍선 강조
        /// </summary>
        private void UpdateHeadHighlights()
        {
            for (int laneIdx = 0; laneIdx < _balloonImages.Count; laneIdx++)
            {
                var lane = _balloonImages[laneIdx];
                for (int i = 0; i < lane.Count; i++)
                {
                    bool isHead = (i == lane.Count - 1);
                    var balloon = lane[i];

                    // Head 강조 (스케일)
                    float scale = isHead ? 1.2f : 1f;
                    balloon.transform.localScale = Vector3.one * scale;
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