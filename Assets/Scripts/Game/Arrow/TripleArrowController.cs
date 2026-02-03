using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using BalloonOut.Core;
using BalloonOut.UI;
using BalloonOut.Game.Balloon;
using BalloonOut.Game.Gimmick;
using DG.Tweening;

namespace BalloonOut.Game.Arrow
{
    /// <summary>
    /// Triple Arrow 부스터 컨트롤러
    /// 랜덤한 3개의 풍선을 순차적으로 타겟팅하여 발사
    /// </summary>
    public class TripleArrowController : MonoBehaviour
    {
        // ========== 인스펙터 노출 변수 ==========
        [Header("프리팹")]
        [SerializeField] private GameObject _homingArrowPrefab;

        [Header("발사 설정")]
        [SerializeField] private float _fireInterval = 0.2f;
        [SerializeField] private float _spawnScaleUpDuration = 0.15f;
        [SerializeField] private Transform _spawnPoint;  // 지정 안 되면 화면 하단 중앙 사용

        [Header("완료 대기")]
        [SerializeField] private float _completionDelay = 0.5f;

        // ========== 이벤트 ==========
        /// <summary>
        /// Triple Arrow 발사 완료 (모든 POP 처리 완료 후)
        /// </summary>
        public event Action OnTripleArrowComplete;

        /// <summary>
        /// 개별 풍선 Hit 시 발생
        /// </summary>
        public event Action<BalloonInstance, GimmickHitResult> OnBalloonHit;

        // ========== 내부 상태 ==========
        private QueueUI _queueUI;
        private int _pendingArrows;
        private bool _isExecuting;

        // ========== 싱글톤 ==========
        public static TripleArrowController Instance { get; private set; }

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
        /// 초기화
        /// </summary>
        public void Initialize(QueueUI queueUI)
        {
            _queueUI = queueUI;
        }

        /// <summary>
        /// 실행 중인지 여부
        /// </summary>
        public bool IsExecuting => _isExecuting;

        /// <summary>
        /// Triple Arrow 실행
        /// </summary>
        /// <param name="targets">타겟 풍선 목록</param>
        public void Execute(List<BalloonInstance> targets)
        {
            if (_isExecuting)
            {
                Debug.LogWarning("[TripleArrowController] Already executing");
                return;
            }

            if (targets == null || targets.Count == 0)
            {
                Debug.LogWarning("[TripleArrowController] No targets provided");
                OnTripleArrowComplete?.Invoke();
                return;
            }

            StartCoroutine(FireSequence(targets));
        }

        // ========== 내부 로직 ==========

        /// <summary>
        /// 순차 발사 코루틴
        /// </summary>
        private IEnumerator FireSequence(List<BalloonInstance> targets)
        {
            _isExecuting = true;
            _pendingArrows = targets.Count;

            Debug.Log($"[TripleArrowController] Starting fire sequence with {targets.Count} targets");

            Vector2 spawnPos = GetSpawnPosition();

            foreach (var target in targets)
            {
                // 타겟이 이미 사라졌으면 스킵
                if (target == null || target.Visual == null)
                {
                    _pendingArrows--;
                    continue;
                }

                SpawnHomingArrow(spawnPos, target);
                yield return new WaitForSeconds(_fireInterval);
            }

            // 모든 POP 완료 대기
            while (_pendingArrows > 0)
            {
                yield return null;
            }

            // 추가 완료 딜레이
            yield return new WaitForSeconds(_completionDelay);

            _isExecuting = false;
            Debug.Log("[TripleArrowController] Fire sequence complete");
            OnTripleArrowComplete?.Invoke();
        }

        /// <summary>
        /// 발사 위치 계산
        /// </summary>
        private Vector2 GetSpawnPosition()
        {
            if (_spawnPoint != null)
            {
                return _spawnPoint.position;
            }

            // 화면 하단 중앙
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 bottomCenter = cam.ViewportToWorldPoint(new Vector3(0.5f, 0.1f, cam.nearClipPlane));
                return new Vector2(bottomCenter.x, bottomCenter.y);
            }

            return Vector2.zero;
        }

        /// <summary>
        /// HomingArrow 생성 및 발사
        /// </summary>
        private void SpawnHomingArrow(Vector2 spawnPos, BalloonInstance target)
        {
            if (_homingArrowPrefab == null)
            {
                Debug.LogError("[TripleArrowController] HomingArrow prefab not assigned!");
                _pendingArrows--;
                return;
            }

            var homingObj = Instantiate(_homingArrowPrefab, spawnPos, Quaternion.identity);
            var homingArrow = homingObj.GetComponent<HomingArrow>();

            if (homingArrow != null)
            {
                // 스케일 업 애니메이션
                homingArrow.transform.localScale = Vector3.zero;
                homingArrow.transform.DOScale(1f, _spawnScaleUpDuration).SetEase(Ease.OutBack);

                // BalloonInstance 타겟 호밍 시작 (무지개 모드)
                homingArrow.StartHomingToBalloon(spawnPos, target, isRainbow: true);
                homingArrow.OnHitBalloonTarget += HandleBalloonHit;

                Debug.Log($"[TripleArrowController] Spawned HomingArrow targeting lane {target.LaneIndex}");
            }
            else
            {
                Debug.LogError("[TripleArrowController] HomingArrow component not found on prefab!");
                _pendingArrows--;
            }
        }

        /// <summary>
        /// 풍선 Hit 핸들러
        /// </summary>
        private void HandleBalloonHit(HomingArrow homingArrow, BalloonInstance balloon)
        {
            homingArrow.OnHitBalloonTarget -= HandleBalloonHit;
            _pendingArrows--;

            if (balloon == null)
            {
                Debug.LogWarning("[TripleArrowController] Hit balloon is null");
                return;
            }

            Debug.Log($"[TripleArrowController] Arrow hit balloon at lane {balloon.LaneIndex}");

            // 풍선 Hit 처리 (기믹 적용)
            ProcessBalloonHit(balloon);
        }

        /// <summary>
        /// 풍선 Hit 처리 (기믹 포함)
        /// </summary>
        private void ProcessBalloonHit(BalloonInstance balloon)
        {
            if (balloon == null) return;

            // Triple Arrow는 색상 무관하게 Hit
            // 풍선 자체 색상으로 처리 (기믹이 색상 체크 안 함)
            bool shouldPop = balloon.TryHit(balloon.Color, out GimmickHitResult hitResult);

            // 이벤트 발생
            OnBalloonHit?.Invoke(balloon, hitResult);

            if (shouldPop)
            {
                // 팝 처리
                if (_queueUI != null)
                {
                    _queueUI.PopBalloonInstance(balloon);
                }
            }
            else
            {
                // 기믹에 의해 팝 방지됨 (Number, Connected 등)
                Debug.Log($"[TripleArrowController] Balloon not popped due to gimmick: {hitResult.FeedbackMessage}");
            }
        }
    }
}
