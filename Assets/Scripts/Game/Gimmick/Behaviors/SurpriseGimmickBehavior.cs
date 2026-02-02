using System;
using UnityEngine;
using BalloonOut.Core;
using BalloonOut.Data;
using BalloonOut.Game.Balloon;

namespace BalloonOut.Game.Gimmick.Behaviors
{
    /// <summary>
    /// Surprise 기믹 - 숨겨진 색상, Queue 맨 앞에서 공개
    /// </summary>
    [Serializable]
    public class SurpriseGimmickBehavior : IGimmickBehavior
    {
        // ========== 상수 ==========
        public const string GIMMICK_ID = "surprise";
        private const string PARAM_IS_REVEALED = "isRevealed";

        // ========== IGimmickBehavior 구현 ==========

        public string GimmickId => GIMMICK_ID;

        public void OnInitialize(BalloonInstance balloon, GimmickInstanceData data)
        {
            // 기본값: 공개되지 않음
            if (!data.Parameters.ContainsKey(PARAM_IS_REVEALED))
            {
                data.SetParam(PARAM_IS_REVEALED, false);
            }

            // 비주얼 설정 (회색 + "?")
            if (balloon.Visual != null && !data.GetParamBool(PARAM_IS_REVEALED))
            {
                balloon.Visual.SetGrayOverlay(true);
                balloon.Visual.SetOverlayText("?", GetDefinition());
            }
        }

        public void OnBecomeActive(BalloonInstance balloon, GimmickInstanceData data)
        {
            // 이미 공개되었으면 무시
            if (data.GetParamBool(PARAM_IS_REVEALED))
            {
                return;
            }

            // 색상 공개!
            RevealColor(balloon, data);
        }

        public bool OnHit(BalloonInstance balloon, GimmickInstanceData data, GameColor arrowColor, out GimmickHitResult result)
        {
            // Surprise 기믹은 공개되지 않은 상태에서 히트 불가
            if (!data.GetParamBool(PARAM_IS_REVEALED))
            {
                result = GimmickHitResult.Reject("Not revealed yet!");
                return false;
            }

            // 공개된 상태면 정상 처리
            result = GimmickHitResult.DefaultPop;
            return true;
        }

        public void OnPop(BalloonInstance balloon, GimmickInstanceData data)
        {
            // 특별한 처리 없음
        }

        public void OnRestore(BalloonInstance balloon, GimmickInstanceData data, GimmickInstanceData snapshot)
        {
            // Surprise는 한번 공개되면 Undo해도 공개 상태 유지
            // 따라서 snapshot의 isRevealed 값을 무시하고 현재 값 유지
            // (단, 현재 값이 false였다면 snapshot 값 사용)
            bool currentRevealed = data.GetParamBool(PARAM_IS_REVEALED);
            bool snapshotRevealed = snapshot.GetParamBool(PARAM_IS_REVEALED);

            // 한번 공개된 건 유지
            if (currentRevealed || snapshotRevealed)
            {
                data.SetParam(PARAM_IS_REVEALED, true);

                // 비주얼 업데이트
                if (balloon.Visual != null)
                {
                    balloon.Visual.SetGrayOverlay(false);
                    balloon.Visual.HideOverlayText();
                }
            }
        }

        public GimmickInstanceData CreateSnapshot(BalloonInstance balloon, GimmickInstanceData data)
        {
            return data.Clone();
        }

        public void OnRender(BalloonInstance balloon, GimmickInstanceData data, BalloonVisual visual)
        {
            if (visual == null) return;

            bool isRevealed = data.GetParamBool(PARAM_IS_REVEALED);

            if (isRevealed)
            {
                // 공개됨 - 원래 색상 표시
                visual.SetGrayOverlay(false);
                visual.HideOverlayText();
            }
            else
            {
                // 숨겨짐 - 회색 + "?"
                visual.SetGrayOverlay(true);
                visual.SetOverlayText("?", GetDefinition());
            }
        }

        // ========== 내부 메서드 ==========

        /// <summary>
        /// 색상 공개 처리
        /// </summary>
        private void RevealColor(BalloonInstance balloon, GimmickInstanceData data)
        {
            data.SetParam(PARAM_IS_REVEALED, true);

            if (balloon.Visual != null)
            {
                var def = GetDefinition();

                // 공개 애니메이션 재생
                balloon.Visual.PlayRevealAnimation(
                    def?.animationDuration ?? 0.3f,
                    () =>
                    {
                        // 파티클 이펙트
                        if (def?.particleEffectPrefab != null)
                        {
                            balloon.Visual.SpawnParticleEffect(def.particleEffectPrefab);
                        }
                    }
                );
            }

            Debug.Log($"[SurpriseGimmick] Revealed balloon color: {balloon.Color} at lane {balloon.LaneIndex}");
        }

        /// <summary>
        /// 기믹 정의 가져오기
        /// </summary>
        private GimmickDefinitionSO GetDefinition()
        {
            return GimmickRegistry.Instance?.GetDefinition(GIMMICK_ID);
        }
    }
}
