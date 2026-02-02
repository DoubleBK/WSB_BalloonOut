using System;
using UnityEngine;
using BalloonOut.Core;
using BalloonOut.Data;
using BalloonOut.Game.Balloon;

namespace BalloonOut.Game.Gimmick.Behaviors
{
    /// <summary>
    /// Number 기믹 - 여러 번 맞춰야 터지는 풍선
    /// </summary>
    [Serializable]
    public class NumberGimmickBehavior : IGimmickBehavior
    {
        // ========== 상수 ==========
        public const string GIMMICK_ID = "number";
        private const string PARAM_REQUIRED_HITS = "requiredHits";
        private const string PARAM_REMAINING_HITS = "remainingHits";

        // ========== IGimmickBehavior 구현 ==========

        public string GimmickId => GIMMICK_ID;

        public void OnInitialize(BalloonInstance balloon, GimmickInstanceData data)
        {
            // requiredHits가 없으면 기본값 2 설정
            if (!data.Parameters.ContainsKey(PARAM_REQUIRED_HITS))
            {
                data.SetParam(PARAM_REQUIRED_HITS, 2);
            }

            // remainingHits 초기화
            int required = data.GetParamInt(PARAM_REQUIRED_HITS, 2);
            data.SetParam(PARAM_REMAINING_HITS, required);

            // 비주얼 설정 (숫자 표시)
            if (balloon.Visual != null)
            {
                UpdateVisual(balloon, data);
            }
        }

        public void OnBecomeActive(BalloonInstance balloon, GimmickInstanceData data)
        {
            // 활성 위치에서 특별한 처리 없음
            // 비주얼만 업데이트
            if (balloon.Visual != null)
            {
                UpdateVisual(balloon, data);
            }
        }

        public bool OnHit(BalloonInstance balloon, GimmickInstanceData data, GameColor arrowColor, out GimmickHitResult result)
        {
            int remaining = data.GetParamInt(PARAM_REMAINING_HITS, 1);
            remaining--;
            data.SetParam(PARAM_REMAINING_HITS, remaining);

            Debug.Log($"[NumberGimmick] Hit! Remaining: {remaining} at lane {balloon.LaneIndex}");

            // 비주얼 업데이트
            if (balloon.Visual != null)
            {
                // 히트 애니메이션
                balloon.Visual.PlayHitAnimation();

                // 숫자 업데이트
                UpdateVisual(balloon, data);
            }

            if (remaining > 0)
            {
                // 아직 더 맞춰야 함
                result = GimmickHitResult.PreventPop($"{remaining} more hit(s)!");
                return false;
            }

            // 다 맞춤 - 팝!
            result = GimmickHitResult.DefaultPop;
            return true;
        }

        public void OnPop(BalloonInstance balloon, GimmickInstanceData data)
        {
            // 파티클 이펙트
            var def = GetDefinition();
            if (def?.particleEffectPrefab != null && balloon.Visual != null)
            {
                balloon.Visual.SpawnParticleEffect(def.particleEffectPrefab);
            }
        }

        public void OnRestore(BalloonInstance balloon, GimmickInstanceData data, GimmickInstanceData snapshot)
        {
            // 스냅샷에서 remainingHits 복원
            int snapshotRemaining = snapshot.GetParamInt(PARAM_REMAINING_HITS, 1);
            data.SetParam(PARAM_REMAINING_HITS, snapshotRemaining);

            Debug.Log($"[NumberGimmick] Restored remainingHits to {snapshotRemaining}");

            // 비주얼 업데이트
            if (balloon.Visual != null)
            {
                UpdateVisual(balloon, data);
            }
        }

        public GimmickInstanceData CreateSnapshot(BalloonInstance balloon, GimmickInstanceData data)
        {
            return data.Clone();
        }

        public void OnRender(BalloonInstance balloon, GimmickInstanceData data, BalloonVisual visual)
        {
            UpdateVisual(balloon, data);
        }

        // ========== 내부 메서드 ==========

        /// <summary>
        /// 비주얼 업데이트 (숫자 표시)
        /// </summary>
        private void UpdateVisual(BalloonInstance balloon, GimmickInstanceData data)
        {
            if (balloon.Visual == null) return;

            int remaining = data.GetParamInt(PARAM_REMAINING_HITS, 1);

            // 1 이하면 숫자 숨김 (일반 풍선처럼 보임)
            if (remaining <= 1)
            {
                balloon.Visual.HideOverlayText();
                return;
            }

            // 숫자 표시
            var def = GetDefinition();
            if (def != null)
            {
                balloon.Visual.SetOverlayText(remaining.ToString(), def);
            }
            else
            {
                balloon.Visual.SetOverlayText(remaining.ToString(), 36f, Color.white);
            }
        }

        /// <summary>
        /// 기믹 정의 가져오기
        /// </summary>
        private GimmickDefinitionSO GetDefinition()
        {
            return GimmickRegistry.Instance?.GetDefinition(GIMMICK_ID);
        }

        // ========== 유틸리티 ==========

        /// <summary>
        /// Number 기믹 인스턴스 데이터 생성 (편의 메서드)
        /// </summary>
        public static GimmickInstanceData CreateData(int requiredHits)
        {
            var data = new GimmickInstanceData(GIMMICK_ID);
            data.SetParam(PARAM_REQUIRED_HITS, requiredHits);
            data.SetParam(PARAM_REMAINING_HITS, requiredHits);
            return data;
        }
    }
}
