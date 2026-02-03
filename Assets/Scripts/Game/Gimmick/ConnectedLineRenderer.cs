using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using BalloonOut.Game.Balloon;

namespace BalloonOut.Game.Gimmick
{
    /// <summary>
    /// Connected 풍선들 사이의 연결선 렌더러
    /// UI Canvas 환경에서 동작 (UI.Image 기반)
    /// </summary>
    public class ConnectedLineRenderer : MonoBehaviour
    {
        // ========== 설정 ==========
        [Header("Line Settings")]
        [SerializeField] private float _lineWidth = 6f;
        [SerializeField] private Color _normalColor = new Color(0.6f, 0.4f, 0.2f, 1f);  // 실타래 색상 (더 진하게)
        [SerializeField] private Color _markedColor = new Color(0.3f, 0.3f, 0.3f, 1f);  // Marked 상태 색상

        // ========== 상태 ==========
        private string _groupId;
        private List<BalloonInstance> _balloons = new List<BalloonInstance>();
        private List<Image> _lineSegments = new List<Image>();
        private Canvas _canvas;
        private RectTransform _rectTransform;

        // ========== 성능 최적화 (Dirty Flag) ==========
        private bool _isDirty = true;
        private Vector3[] _cachedPositions;

        // ========== 싱글톤 (관리용) ==========
        private static Dictionary<string, ConnectedLineRenderer> _renderers = new Dictionary<string, ConnectedLineRenderer>();

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// 그룹의 연결선 렌더러 가져오기 또는 생성
        /// </summary>
        public static ConnectedLineRenderer GetOrCreate(string groupId, Transform parent)
        {
            if (_renderers.TryGetValue(groupId, out var existing) && existing != null)
            {
                return existing;
            }

            var go = new GameObject($"ConnectedLine_{groupId}");
            go.transform.SetParent(parent, false);

            // RectTransform을 먼저 추가 (AddComponent<ConnectedLineRenderer>보다 먼저)
            var rect = go.AddComponent<RectTransform>();

            var renderer = go.AddComponent<ConnectedLineRenderer>();
            renderer._groupId = groupId;
            renderer._rectTransform = rect;

            // RectTransform 설정 (전체 영역 커버)
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            // Canvas 참조 찾기
            renderer._canvas = parent.GetComponentInParent<Canvas>();

            // 렌더링 순서: 풍선들 위에 표시 (맨 뒤 sibling)
            go.transform.SetAsLastSibling();

            _renderers[groupId] = renderer;

            Debug.Log($"[ConnectedLineRenderer] Created renderer for group: {groupId}, Canvas: {renderer._canvas?.name ?? "null"}");

            return renderer;
        }

        /// <summary>
        /// 모든 렌더러 정리
        /// </summary>
        public static void ClearAll()
        {
            foreach (var kvp in _renderers)
            {
                if (kvp.Value != null)
                {
                    Destroy(kvp.Value.gameObject);
                }
            }
            _renderers.Clear();
        }

        /// <summary>
        /// 풍선을 해당 그룹의 렌더러에서 제거
        /// </summary>
        public static void RemoveBalloonFromGroup(string groupId, BalloonInstance balloon)
        {
            if (string.IsNullOrEmpty(groupId) || balloon == null) return;

            if (_renderers.TryGetValue(groupId, out var renderer) && renderer != null)
            {
                renderer.RemoveBalloon(balloon);
            }
        }

        /// <summary>
        /// 풍선 추가
        /// </summary>
        public void AddBalloon(BalloonInstance balloon)
        {
            if (balloon != null && !_balloons.Contains(balloon))
            {
                _balloons.Add(balloon);
                _isDirty = true;
                // 캐시 배열 크기 조정
                System.Array.Resize(ref _cachedPositions, _balloons.Count);
                Debug.Log($"[ConnectedLineRenderer] Added balloon to group {_groupId}. Total: {_balloons.Count}, HasVisual: {balloon.Visual != null}");
            }
        }

        /// <summary>
        /// 풍선 제거
        /// </summary>
        public void RemoveBalloon(BalloonInstance balloon)
        {
            if (_balloons.Remove(balloon))
            {
                _isDirty = true;
                // 캐시 배열 크기 조정
                System.Array.Resize(ref _cachedPositions, _balloons.Count);
                UpdateLines();

                // 풍선이 1개 이하면 렌더러 제거
                if (_balloons.Count <= 1)
                {
                    DestroySelf();
                }
            }
        }

        /// <summary>
        /// 연결선 업데이트
        /// </summary>
        public void UpdateLines()
        {
            // 최소 2개 풍선이 있어야 연결선 표시
            if (_balloons.Count < 2)
            {
                ClearLineSegments();
                return;
            }

            // Canvas 참조가 없으면 다시 찾기
            if (_canvas == null)
            {
                _canvas = GetComponentInParent<Canvas>();
            }
            if (_rectTransform == null)
            {
                _rectTransform = GetComponent<RectTransform>();
            }

            // 필요한 선분 수 = 풍선 수 - 1 (체인 연결)
            int requiredSegments = _balloons.Count - 1;

            // 선분 수 조정
            while (_lineSegments.Count < requiredSegments)
            {
                CreateLineSegment();
            }
            while (_lineSegments.Count > requiredSegments)
            {
                DestroyLineSegment(_lineSegments.Count - 1);
            }

            // 각 선분 위치 및 크기 업데이트
            for (int i = 0; i < requiredSegments; i++)
            {
                var balloon1 = _balloons[i];
                var balloon2 = _balloons[i + 1];

                if (balloon1.Visual == null || balloon2.Visual == null)
                {
                    _lineSegments[i].gameObject.SetActive(false);
                    continue;
                }

                UpdateLineSegment(_lineSegments[i], balloon1, balloon2);
            }
        }

        /// <summary>
        /// Marked 상태 업데이트
        /// </summary>
        public void OnBalloonMarked(BalloonInstance balloon)
        {
            // 연결선 색상 업데이트
            bool allMarked = true;
            foreach (var b in _balloons)
            {
                if (!b.IsMarked())
                {
                    allMarked = false;
                    break;
                }
            }

            Color lineColor = allMarked ? _markedColor : _normalColor;
            foreach (var segment in _lineSegments)
            {
                if (segment != null)
                {
                    segment.color = lineColor;
                }
            }
        }

        // ========== 내부 메서드 ==========

        private void CreateLineSegment()
        {
            var go = new GameObject("LineSegment");
            go.transform.SetParent(transform, false);

            var image = go.AddComponent<Image>();
            image.color = _normalColor;
            image.raycastTarget = false;

            // 기본 흰색 스프라이트 사용 (Unity UI 기본)
            // 스프라이트가 없으면 단색 사각형으로 렌더링됨
            image.type = Image.Type.Simple;

            var rect = go.GetComponent<RectTransform>();
            // 앵커를 중앙으로 설정 (부모가 stretch 앵커여도 정확한 위치 계산 가능)
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0, 0.5f);  // 왼쪽 중앙 피벗 (시작점 기준 회전)

            _lineSegments.Add(image);
        }

        private void DestroyLineSegment(int index)
        {
            if (index >= 0 && index < _lineSegments.Count)
            {
                if (_lineSegments[index] != null)
                {
                    Destroy(_lineSegments[index].gameObject);
                }
                _lineSegments.RemoveAt(index);
            }
        }

        private void ClearLineSegments()
        {
            foreach (var segment in _lineSegments)
            {
                if (segment != null)
                {
                    Destroy(segment.gameObject);
                }
            }
            _lineSegments.Clear();
        }

        private void UpdateLineSegment(Image segment, BalloonInstance balloon1, BalloonInstance balloon2)
        {
            if (segment == null) return;

            // 풍선 Visual의 RectTransform 가져오기
            var rect1 = balloon1.Visual.GetComponent<RectTransform>();
            var rect2 = balloon2.Visual.GetComponent<RectTransform>();

            if (rect1 == null || rect2 == null) return;

            // 풍선 월드 좌표
            Vector3 worldPos1 = rect1.position;
            Vector3 worldPos2 = rect2.position;

            // 선분의 RectTransform
            var lineRect = segment.GetComponent<RectTransform>();

            // 방법 1: RectTransformUtility를 사용하여 정확한 Canvas 좌표로 변환
            if (_canvas != null && _rectTransform != null)
            {
                Camera cam = _canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _canvas.worldCamera;

                // 월드 좌표 → 스크린 좌표 → 로컬 좌표 변환
                Vector2 screenPos1 = RectTransformUtility.WorldToScreenPoint(cam, worldPos1);
                Vector2 screenPos2 = RectTransformUtility.WorldToScreenPoint(cam, worldPos2);

                Vector2 localPos1, localPos2;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_rectTransform, screenPos1, cam, out localPos1);
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_rectTransform, screenPos2, cam, out localPos2);

                // 방향 및 거리 계산
                Vector2 direction = localPos2 - localPos1;
                float distance = direction.magnitude;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

                // RectTransform 설정
                lineRect.anchoredPosition = localPos1;
                lineRect.sizeDelta = new Vector2(distance, _lineWidth);
                lineRect.localRotation = Quaternion.Euler(0, 0, angle);
            }
            else
            {
                // 폴백: 직접 로컬 좌표 변환 (이전 방식)
                Vector2 localPos1 = transform.InverseTransformPoint(worldPos1);
                Vector2 localPos2 = transform.InverseTransformPoint(worldPos2);

                Vector2 direction = localPos2 - localPos1;
                float distance = direction.magnitude;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

                lineRect.anchoredPosition = localPos1;
                lineRect.sizeDelta = new Vector2(distance, _lineWidth);
                lineRect.localRotation = Quaternion.Euler(0, 0, angle);
            }

            segment.gameObject.SetActive(true);
        }

        private void DestroySelf()
        {
            if (_renderers.ContainsKey(_groupId))
            {
                _renderers.Remove(_groupId);
            }
            ClearLineSegments();
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_renderers.ContainsKey(_groupId) && _renderers[_groupId] == this)
            {
                _renderers.Remove(_groupId);
            }
        }

        private void LateUpdate()
        {
            if (_balloons.Count < 2) return;

            // 위치 변경 감지 (Dirty Flag 패턴)
            bool positionChanged = CheckPositionChanged();
            if (!positionChanged && !_isDirty) return;

            UpdateLines();
            _isDirty = false;
        }

        /// <summary>
        /// 풍선 위치 변경 감지 (성능 최적화)
        /// </summary>
        private bool CheckPositionChanged()
        {
            if (_cachedPositions == null || _cachedPositions.Length != _balloons.Count)
            {
                _cachedPositions = new Vector3[_balloons.Count];
                return true;  // 배열 초기화 시 변경됨으로 처리
            }

            for (int i = 0; i < _balloons.Count; i++)
            {
                if (_balloons[i].Visual == null) continue;
                Vector3 currentPos = _balloons[i].Visual.transform.position;
                if (_cachedPositions[i] != currentPos)
                {
                    _cachedPositions[i] = currentPos;
                    return true;
                }
            }
            return false;
        }
    }
}
