using System;
using UnityEngine;
using BalloonOut.Core;
using BalloonOut.UI;
using BalloonOut.Game.Balloon;
using DG.Tweening;

namespace BalloonOut.Game.Arrow
{
    /// <summary>
    /// 호밍 화살표 - 탈출 후 풍선을 향해 날아가는 화살표
    /// </summary>
    public class HomingArrow : MonoBehaviour
    {
        // ========== 인스펙터 노출 변수 ==========
        [Header("설정값")]
        [SerializeField] private float _homingSpeed = 15f;
        [SerializeField] private float _curveStrength = 2f;
        [SerializeField] private float _arrivalThreshold = 0.2f;

        [Header("비주얼")]
        [SerializeField] private SpriteRenderer _spriteRenderer;
        [SerializeField] private TrailRenderer _trailRenderer;
        [SerializeField] private int _sortingOrder = 100;

        [Header("파티클")]
        [SerializeField] private ParticleSystem _launchParticlePrefab;
        [SerializeField] private ParticleSystem _hitParticlePrefab;

        [Header("애니메이션")]
        [SerializeField] private float _launchScaleDuration = 0.2f;
        [SerializeField] private float _launchScaleMultiplier = 0.5f;

        [Header("Arrow 길이 반영")]
        [SerializeField] private float _trailTimePerCell = 0.08f;
        [SerializeField] private float _baseTrailTime = 0.15f;

        [Header("무지개 모드")]
        [SerializeField] private Gradient _rainbowGradient;
        [SerializeField] private float _rainbowCycleSpeed = 2f;

        // ========== 내부 상태 변수 ==========
        private Vector3 _originalScale;
        private int _originalArrowLength = 3;

        private GameColor _color;
        private Vector2 _startPosition;
        private Vector2 _controlPoint;
        private float _progress;
        private bool _isHoming;

        // 위치 기반 호밍용
        private Vector3 _targetPosition;
        private QueueUI _queueUI;

        // Triple Arrow용: 특정 풍선 타겟팅
        private BalloonInstance _targetBalloon;
        private bool _isRainbowMode;

        // 원본 화살표 스냅샷 (Undo용)
        private ArrowSnapshot _sourceArrowSnapshot;

        // ========== 이벤트 ==========
        public event Action<HomingArrow, GameColor> OnHitTarget;
        public event Action<HomingArrow, BalloonInstance> OnHitBalloonTarget;

        // ========== 프로퍼티 ==========
        public GameColor Color => _color;
        public bool IsHoming => _isHoming;
        public ArrowSnapshot SourceArrowSnapshot => _sourceArrowSnapshot;
        public BalloonInstance TargetBalloon => _targetBalloon;
        public bool IsRainbowMode => _isRainbowMode;

        // ========== 유니티 라이프사이클 ==========
        private void Awake()
        {
            // Inspector에서 할당되지 않았으면 자동으로 찾기
            if (_spriteRenderer == null)
                _spriteRenderer = GetComponent<SpriteRenderer>();

            if (_trailRenderer == null)
                _trailRenderer = GetComponent<TrailRenderer>();

            _originalScale = transform.localScale;
            ApplySortingOrder();
        }

        private void ApplySortingOrder()
        {
            if (_spriteRenderer != null)
            {
                _spriteRenderer.sortingOrder = _sortingOrder;
            }

            if (_trailRenderer != null)
            {
                _trailRenderer.sortingOrder = _sortingOrder - 1;
            }
        }

        private void Update()
        {
            if (_isHoming)
            {
                UpdateHoming();

                // 무지개 모드: 매 프레임 비주얼 업데이트
                if (_isRainbowMode)
                {
                    UpdateRainbowVisual();
                }
            }
        }

        /// <summary>
        /// 무지개 모드 비주얼 업데이트 (색상 애니메이션)
        /// </summary>
        private void UpdateRainbowVisual()
        {
            if (_spriteRenderer == null) return;

            // 시간에 따른 무지개색 변화
            float t = (Time.time * _rainbowCycleSpeed) % 1f;
            Color rainbowColor;

            if (_rainbowGradient != null && _rainbowGradient.colorKeys.Length > 0)
            {
                rainbowColor = _rainbowGradient.Evaluate(t);
            }
            else
            {
                // 기본 무지개색 생성 (HSV 기반)
                rainbowColor = UnityEngine.Color.HSVToRGB(t, 0.8f, 1f);
            }

            _spriteRenderer.color = rainbowColor;
        }

        // ========== 공개 인터페이스 ==========

        /// <summary>
        /// Arrow 위치에서 바로 시작하는 호밍 (전환 연출용)
        /// </summary>
        public void StartHomingFromArrowPosition(
            Vector2 arrowHeadPos,
            Vector2 exitDirection,
            QueueUI queueUI,
            GameColor color,
            int arrowLength,
            ArrowSnapshot sourceArrowSnapshot = null)
        {
            _startPosition = arrowHeadPos;
            _queueUI = queueUI;
            _color = color;
            _originalArrowLength = arrowLength;
            _sourceArrowSnapshot = sourceArrowSnapshot;
            _progress = 0f;
            _isHoming = true;

            transform.position = arrowHeadPos;

            // Arrow 길이를 Trail 길이에 반영
            SetTrailLength(arrowLength);

            // 풍선 위치로 타겟 설정
            _targetPosition = _queueUI.GetBalloonWorldPosition(color);

            // 컨트롤 포인트: 탈출 방향으로 오프셋하여 부드러운 곡선 생성
            Vector2 exitOffset = exitDirection.normalized * _curveStrength * 1.5f;
            Vector2 midPoint = (arrowHeadPos + (Vector2)_targetPosition) * 0.5f;
            _controlPoint = midPoint + exitOffset;

            // 초기 회전: 탈출 방향
            float initialAngle = Mathf.Atan2(exitDirection.y, exitDirection.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0, 0, initialAngle);

            UpdateVisual();
            PlayLaunchAnimation();
            SpawnLaunchParticle();

            Debug.Log($"[HomingArrow] StartHomingFromArrowPosition: from {arrowHeadPos} (exit: {exitDirection}), length: {arrowLength}, color: {color}");
        }

        /// <summary>
        /// 고정 위치 타겟 호밍 시작
        /// </summary>
        public void StartHomingToPosition(Vector2 startPos, Vector3 targetPos, GameColor color)
        {
            _startPosition = startPos;
            _targetPosition = targetPos;
            _queueUI = null;
            _color = color;
            _progress = 0f;
            _isHoming = true;

            transform.position = startPos;

            // 컨트롤 포인트 계산
            Vector2 midPoint = (_startPosition + (Vector2)targetPos) * 0.5f;
            Vector2 perpendicular = Vector2.Perpendicular(((Vector2)targetPos - _startPosition).normalized);
            _controlPoint = midPoint + perpendicular * _curveStrength;

            // 초기 회전
            Vector2 initialDirection = ((Vector2)targetPos - _startPosition).normalized;
            float initialAngle = Mathf.Atan2(initialDirection.y, initialDirection.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0, 0, initialAngle);

            UpdateVisual();
            PlayLaunchAnimation();
            SpawnLaunchParticle();
        }

        /// <summary>
        /// Triple Arrow용: 특정 BalloonInstance 타겟 호밍 시작 (무지개 모드)
        /// </summary>
        public void StartHomingToBalloon(Vector2 startPos, BalloonInstance targetBalloon, bool isRainbow = true)
        {
            if (targetBalloon == null || targetBalloon.Visual == null)
            {
                Debug.LogWarning("[HomingArrow] StartHomingToBalloon: Invalid target balloon or visual");
                Destroy(gameObject);
                return;
            }

            _startPosition = startPos;
            _targetBalloon = targetBalloon;
            _color = targetBalloon.Color;
            _isRainbowMode = isRainbow;
            _queueUI = null;
            _progress = 0f;
            _isHoming = true;

            transform.position = startPos;

            // 초기 타겟 위치
            _targetPosition = targetBalloon.Visual.transform.position;

            // 컨트롤 포인트: 위쪽으로 곡선
            Vector2 midPoint = (_startPosition + (Vector2)_targetPosition) * 0.5f;
            Vector2 toTarget = ((Vector2)_targetPosition - _startPosition).normalized;
            Vector2 perpendicular = new Vector2(-toTarget.y, toTarget.x);
            _controlPoint = midPoint + perpendicular * _curveStrength;

            // 초기 회전
            float initialAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0, 0, initialAngle);

            SetTrailLength(_originalArrowLength);
            UpdateVisual();
            PlayLaunchAnimation();
            SpawnLaunchParticle();

            Debug.Log($"[HomingArrow] StartHomingToBalloon: target lane {targetBalloon.LaneIndex}, color {_color}, rainbow={isRainbow}");
        }

        /// <summary>
        /// Trail 길이를 Arrow 길이에 따라 설정
        /// </summary>
        private void SetTrailLength(int arrowLength)
        {
            if (_trailRenderer != null)
            {
                _trailRenderer.time = _baseTrailTime + (arrowLength - 1) * _trailTimePerCell;
            }
        }

        // ========== 내부 유틸리티 ==========
        private void UpdateHoming()
        {
            // BalloonInstance 타겟이 있으면 해당 위치 추적
            if (_targetBalloon != null && _targetBalloon.Visual != null)
            {
                _targetPosition = _targetBalloon.Visual.transform.position;
            }
            // QueueUI 참조가 있으면 매 프레임 위치 업데이트 (카메라 이동 대응)
            else if (_queueUI != null)
            {
                Vector3 newTargetPos = _queueUI.GetBalloonWorldPosition(_color);
                if (newTargetPos != Vector3.zero)
                {
                    _targetPosition = newTargetPos;
                }
            }

            Vector2 targetPos = _targetPosition;

            // 진행도 업데이트
            _progress += _homingSpeed * Time.deltaTime / GetPathLength();
            _progress = Mathf.Clamp01(_progress);

            // Bezier 커브 위치 계산
            Vector2 newPos = CalculateBezierPoint(_progress, _startPosition, _controlPoint, targetPos);
            transform.position = newPos;

            // 방향 업데이트
            if (_progress < 1f)
            {
                Vector2 nextPos = CalculateBezierPoint(_progress + 0.05f, _startPosition, _controlPoint, targetPos);
                Vector2 direction = (nextPos - newPos).normalized;
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
                transform.rotation = Quaternion.Euler(0, 0, angle);
            }

            // 도착 체크
            float distance = Vector2.Distance(newPos, targetPos);
            if (distance < _arrivalThreshold || _progress >= 1f)
            {
                HitTarget();
            }
        }

        private void HitTarget()
        {
            _isHoming = false;

            SpawnHitParticle();

            // BalloonInstance 타겟이 있으면 해당 이벤트 발생
            if (_targetBalloon != null)
            {
                OnHitBalloonTarget?.Invoke(this, _targetBalloon);
                Debug.Log($"[HomingArrow] Hit balloon target, lane: {_targetBalloon.LaneIndex}, color: {_color}");
            }
            else
            {
                OnHitTarget?.Invoke(this, _color);
                Debug.Log($"[HomingArrow] Hit target, color: {_color}");
            }

            Destroy(gameObject, 0.1f);
        }

        // ========== 애니메이션 ==========
        private void PlayLaunchAnimation()
        {
            transform.localScale = _originalScale * _launchScaleMultiplier;
            transform.DOScale(_originalScale, _launchScaleDuration).SetEase(Ease.OutBack);
        }

        // ========== 파티클 ==========
        private void SpawnLaunchParticle()
        {
            if (_launchParticlePrefab == null)
                return;

            var particle = Instantiate(_launchParticlePrefab, transform.position, Quaternion.identity);
            var main = particle.main;
            main.startColor = ColorHelper.GetColor(_color);
            particle.Play();
            Destroy(particle.gameObject, main.duration + main.startLifetime.constantMax);
        }

        private void SpawnHitParticle()
        {
            if (_hitParticlePrefab == null)
                return;

            var particle = Instantiate(_hitParticlePrefab, transform.position, Quaternion.identity);
            var main = particle.main;
            main.startColor = ColorHelper.GetColor(_color);
            particle.Play();
            Destroy(particle.gameObject, main.duration + main.startLifetime.constantMax);
        }

        private float GetPathLength()
        {
            return Vector2.Distance(_startPosition, _targetPosition) * 1.2f;
        }

        private Vector2 CalculateBezierPoint(float t, Vector2 p0, Vector2 p1, Vector2 p2)
        {
            // Quadratic Bezier curve: B(t) = (1-t)²P0 + 2(1-t)tP1 + t²P2
            float u = 1f - t;
            return u * u * p0 + 2f * u * t * p1 + t * t * p2;
        }

        private void UpdateVisual()
        {
            Color unityColor;

            if (_isRainbowMode)
            {
                // 무지개 모드: 그라디언트 사용 또는 기본 무지개색
                if (_rainbowGradient != null && _rainbowGradient.colorKeys.Length > 0)
                {
                    unityColor = _rainbowGradient.Evaluate(Time.time * _rainbowCycleSpeed % 1f);
                }
                else
                {
                    // 기본 무지개색 (마젠타)
                    unityColor = new Color(1f, 0.4f, 0.8f, 1f);
                }
            }
            else
            {
                unityColor = ColorHelper.GetColor(_color);
            }

            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = unityColor;
            }

            if (_trailRenderer != null)
            {
                if (_isRainbowMode)
                {
                    // 무지개 Trail: 그라디언트 사용
                    SetRainbowTrail();
                }
                else
                {
                    _trailRenderer.startColor = unityColor;
                    _trailRenderer.endColor = new Color(unityColor.r, unityColor.g, unityColor.b, 0f);
                }
            }
        }

        /// <summary>
        /// Trail에 무지개 그라디언트 적용
        /// </summary>
        private void SetRainbowTrail()
        {
            if (_trailRenderer == null) return;

            // 무지개 색상 그라디언트
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(UnityEngine.Color.red, 0f),
                    new GradientColorKey(UnityEngine.Color.yellow, 0.25f),
                    new GradientColorKey(UnityEngine.Color.green, 0.5f),
                    new GradientColorKey(UnityEngine.Color.cyan, 0.75f),
                    new GradientColorKey(new UnityEngine.Color(1f, 0.4f, 0.8f), 1f)  // 마젠타
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.8f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            _trailRenderer.colorGradient = gradient;
        }
    }
}
