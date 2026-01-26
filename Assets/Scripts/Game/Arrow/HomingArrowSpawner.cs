using System;
using System.Collections;
using UnityEngine;
using DG.Tweening;
using BalloonOut.Core;
using BalloonOut.Game.Grid;
using BalloonOut.UI;

namespace BalloonOut.Game.Arrow
{
    /// <summary>
    /// HomingArrow 생성 및 전환 연출 담당
    /// </summary>
    public class HomingArrowSpawner : MonoBehaviour
    {
        // ========== 인스펙터 노출 변수 ==========
        [Header("프리팹")]
        [SerializeField] private GameObject _homingArrowPrefab;

        [Header("전환 연출")]
        [SerializeField] private float _transitionDelay = 0.15f;
        [SerializeField] private float _fadeOutDuration = 0.25f;
        [SerializeField] private float _spawnDelay = 0.05f;
        [SerializeField] private float _scaleUpDuration = 0.15f;

        // ========== 참조 ==========
        private QueueUI _queueUI;

        // ========== 이벤트 ==========
        public event Action<HomingArrow, GameColor> OnHomingHitTarget;

        // ========== 싱글톤 ==========
        public static HomingArrowSpawner Instance { get; private set; }

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
        /// Arrow 탈출 시작 시 호출 - 딜레이 후 HomingArrow로 전환
        /// </summary>
        public void HandleArrowExtractionStarted(ArrowController arrow, Vector2 headPosition, ArrowDirection exitDir)
        {
            Debug.Log($"[HomingArrowSpawner] HandleArrowExtractionStarted: color={arrow.Color}, headPos={headPosition}, dir={exitDir}");
            StartCoroutine(DelayedArrowTransition(arrow, exitDir));
        }

        // ========== 내부 유틸리티 ==========

        /// <summary>
        /// 딜레이 후 Arrow → HomingArrow 전환
        /// </summary>
        private IEnumerator DelayedArrowTransition(ArrowController arrow, ArrowDirection exitDir)
        {
            // Arrow 정보 미리 저장 (Arrow가 파괴되기 전에!)
            GameColor color = arrow.Color;
            int arrowLength = arrow.TotalLength;
            Vector2 exitDirection = GetDirectionVector(exitDir);
            Vector2 initialHeadPos = arrow.GetHeadWorldPosition();

            Debug.Log($"[HomingArrowSpawner] DelayedArrowTransition started: color={color}, initialPos={initialHeadPos}");

            // 딜레이 대기
            yield return new WaitForSeconds(_transitionDelay);

            // Arrow가 아직 존재하면 현재 위치 사용, 아니면 저장된 위치 사용
            Vector2 spawnPosition;
            if (arrow != null && arrow.gameObject != null)
            {
                spawnPosition = arrow.GetHeadWorldPosition();

                Debug.Log($"[HomingArrowSpawner] Arrow still exists, using current pos: {spawnPosition}");

                // Arrow 페이드 아웃 시작
                arrow.StartFadeOutTransition(_fadeOutDuration, () =>
                {
                    if (arrow != null && arrow.gameObject != null)
                    {
                        Destroy(arrow.gameObject);
                    }
                });
            }
            else
            {
                // Arrow가 이미 파괴됨 - 저장된 위치 사용
                spawnPosition = initialHeadPos;
                Debug.Log($"[HomingArrowSpawner] Arrow already destroyed, using saved pos: {spawnPosition}");
            }

            // HomingArrow 생성 (Arrow 존재 여부와 무관하게 항상 생성)
            if (_queueUI != null && _homingArrowPrefab != null)
            {
                StartCoroutine(SpawnHomingArrowAtPosition(spawnPosition, exitDirection, color, arrowLength));
            }
            else
            {
                Debug.LogWarning($"[HomingArrowSpawner] Cannot spawn HomingArrow: _queueUI={_queueUI != null}, _homingArrowPrefab={_homingArrowPrefab != null}");
            }
        }

        /// <summary>
        /// Arrow 위치에서 HomingArrow 생성
        /// </summary>
        private IEnumerator SpawnHomingArrowAtPosition(
            Vector2 position,
            Vector2 exitDirection,
            GameColor color,
            int arrowLength)
        {
            yield return new WaitForSeconds(_spawnDelay);

            Vector3 targetPos = _queueUI.GetBalloonWorldPosition(color);
            if (targetPos == Vector3.zero)
            {
                Debug.LogWarning($"[HomingArrowSpawner] No balloon found for color: {color}");
                yield break;
            }

            var homingObj = Instantiate(_homingArrowPrefab, position, Quaternion.identity);
            var homingArrow = homingObj.GetComponent<HomingArrow>();

            if (homingArrow != null)
            {
                // 스케일 업 애니메이션
                homingArrow.transform.localScale = Vector3.zero;
                homingArrow.transform.DOScale(1f, _scaleUpDuration).SetEase(Ease.OutBack);

                // Arrow 위치에서 호밍 시작
                homingArrow.StartHomingFromArrowPosition(
                    position,
                    exitDirection,
                    _queueUI,
                    color,
                    arrowLength
                );
                homingArrow.OnHitTarget += HandleHomingHitTarget;

                Debug.Log($"[HomingArrowSpawner] HomingArrow spawned at: {position}, color: {color}");
            }
            else
            {
                Debug.LogError("[HomingArrowSpawner] HomingArrow component not found on prefab!");
            }
        }

        private void HandleHomingHitTarget(HomingArrow homing, GameColor color)
        {
            OnHomingHitTarget?.Invoke(homing, color);
        }

        private Vector2 GetDirectionVector(ArrowDirection dir)
        {
            return dir switch
            {
                ArrowDirection.Up => Vector2.up,
                ArrowDirection.Down => Vector2.down,
                ArrowDirection.Left => Vector2.left,
                ArrowDirection.Right => Vector2.right,
                _ => Vector2.up
            };
        }
    }
}
