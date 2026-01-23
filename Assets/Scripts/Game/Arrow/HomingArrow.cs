using System;
using UnityEngine;
using BalloonOut.Core;
using BalloonOut.UI;
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

        // ========== 이벤트 ==========
        public event Action<HomingArrow, GameColor> OnHitTarget;

        // ========== 프로퍼티 ==========
        public GameColor Color => _color;
        public bool IsHoming => _isHoming;

        // ========== 유니티 라이프사이클 ==========
        private void Awake()
        {
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
            }
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
            int arrowLength)
        {
            _startPosition = arrowHeadPos;
            _queueUI = queueUI;
            _color = color;
            _originalArrowLength = arrowLength;
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
            // QueueUI 참조가 있으면 매 프레임 위치 업데이트 (카메라 이동 대응)
            if (_queueUI != null)
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

            OnHitTarget?.Invoke(this, _color);
            Debug.Log($"[HomingArrow] Hit target, color: {_color}");

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
            Color unityColor = ColorHelper.GetColor(_color);

            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = unityColor;
            }

            if (_trailRenderer != null)
            {
                _trailRenderer.startColor = unityColor;
                _trailRenderer.endColor = new Color(unityColor.r, unityColor.g, unityColor.b, 0f);
            }
        }
    }
}
