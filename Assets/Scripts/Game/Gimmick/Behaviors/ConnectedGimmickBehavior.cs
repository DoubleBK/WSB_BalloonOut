using System;
using UnityEngine;
using BalloonOut.Core;
using BalloonOut.Data;
using BalloonOut.Game.Balloon;

namespace BalloonOut.Game.Gimmick.Behaviors
{
    /// <summary>
    /// Connected 기믹 - 연결된 풍선 그룹
    /// 모든 연결된 풍선이 Hit되어야 동시에 POP
    /// </summary>
    [Serializable]
    public class ConnectedGimmickBehavior : IGimmickBehavior
    {
        // ========== 상수 ==========
        public const string GIMMICK_ID = "connected";
        private const string PARAM_GROUP_ID = "groupId";
        private const string PARAM_IS_MARKED = "isMarked";
        private const string PARAM_GROUP_SIZE = "groupSize";

        // ========== IGimmickBehavior 구현 ==========

        public string GimmickId => GIMMICK_ID;

        public void OnInitialize(BalloonInstance balloon, GimmickInstanceData data)
        {
            // 기본값 설정
            if (!data.Parameters.ContainsKey(PARAM_IS_MARKED))
            {
                data.SetParam(PARAM_IS_MARKED, false);
            }

            // ConnectedBalloonManager에 등록
            string groupId = data.GetParam(PARAM_GROUP_ID, "");
            if (!string.IsNullOrEmpty(groupId))
            {
                ConnectedBalloonManager.Instance?.RegisterBalloon(groupId, balloon);
            }

            // 비주얼 설정 (연결 표시)
            if (balloon.Visual != null)
            {
                UpdateVisual(balloon, data);
            }
        }

        public void OnBecomeActive(BalloonInstance balloon, GimmickInstanceData data)
        {
            // 활성 위치에 도달해도 특별한 처리 없음
            // 비주얼만 업데이트
            if (balloon.Visual != null)
            {
                UpdateVisual(balloon, data);
            }
        }

        public bool OnHit(BalloonInstance balloon, GimmickInstanceData data, GameColor arrowColor, out GimmickHitResult result)
        {
            // 이미 Marked면 Hit 불가 (이론상 Target 제외로 여기 도달 안 함)
            if (data.GetParamBool(PARAM_IS_MARKED))
            {
                result = GimmickHitResult.Reject("Already marked");
                return false;
            }

            // Marked로 전환
            data.SetParam(PARAM_IS_MARKED, true);

            Debug.Log($"[ConnectedGimmick] Balloon marked at lane {balloon.LaneIndex}");

            // 비주얼 업데이트 (검은색으로)
            if (balloon.Visual != null)
            {
                UpdateVisual(balloon, data);
                balloon.Visual.PlayHitAnimation();
            }

            // 그룹 전체 체크
            string groupId = data.GetParam(PARAM_GROUP_ID, "");
            if (!string.IsNullOrEmpty(groupId) && ConnectedBalloonManager.Instance != null)
            {
                if (ConnectedBalloonManager.Instance.IsGroupFullyMarked(groupId))
                {
                    // 모두 Marked → 전체 POP!
                    Debug.Log($"[ConnectedGimmick] Group {groupId} fully marked! Popping all!");

                    // 그룹 전체 POP 처리는 ConnectedBalloonManager가 담당
                    // 현재 풍선은 정상 POP
                    result = GimmickHitResult.DefaultPop;

                    // 다른 연결된 풍선들도 POP 트리거
                    ConnectedBalloonManager.Instance.TriggerGroupPop(groupId, balloon);

                    return true;
                }
            }

            // 아직 다 안 맞음 → POP 방지
            result = GimmickHitResult.PreventPop("Group not complete");
            return false;
        }

        public void OnPop(BalloonInstance balloon, GimmickInstanceData data)
        {
            // 파티클 이펙트
            var def = GetDefinition();
            if (def?.particleEffectPrefab != null && balloon.Visual != null)
            {
                balloon.Visual.SpawnParticleEffect(def.particleEffectPrefab);
            }

            // ConnectedBalloonManager에서 제거
            string groupId = data.GetParam(PARAM_GROUP_ID, "");
            if (!string.IsNullOrEmpty(groupId))
            {
                ConnectedBalloonManager.Instance?.UnregisterBalloon(groupId, balloon);
            }
        }

        public void OnRestore(BalloonInstance balloon, GimmickInstanceData data, GimmickInstanceData snapshot)
        {
            // 스냅샷에서 isMarked 복원
            bool snapshotMarked = snapshot.GetParamBool(PARAM_IS_MARKED);
            data.SetParam(PARAM_IS_MARKED, snapshotMarked);

            Debug.Log($"[ConnectedGimmick] Restored isMarked to {snapshotMarked}");

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
        /// 비주얼 업데이트
        /// </summary>
        private void UpdateVisual(BalloonInstance balloon, GimmickInstanceData data)
        {
            if (balloon.Visual == null) return;

            bool isMarked = data.GetParamBool(PARAM_IS_MARKED);
            var def = GetDefinition();

            if (isMarked)
            {
                // Marked 상태 - 검은색/회색 + 체크마크
                balloon.Visual.SetMarkedState(true, def);
            }
            else
            {
                // Normal 상태 - 원래 색상
                balloon.Visual.SetMarkedState(false, def);
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
        /// Connected 기믹 인스턴스 데이터 생성 (편의 메서드)
        /// </summary>
        public static GimmickInstanceData CreateData(string groupId, int groupSize)
        {
            var data = new GimmickInstanceData(GIMMICK_ID);
            data.SetParam(PARAM_GROUP_ID, groupId);
            data.SetParam(PARAM_IS_MARKED, false);
            data.SetParam(PARAM_GROUP_SIZE, groupSize);
            return data;
        }

        /// <summary>
        /// 풍선이 Marked 상태인지 확인
        /// </summary>
        public static bool IsMarked(GimmickInstanceData data)
        {
            return data?.GetParamBool(PARAM_IS_MARKED) ?? false;
        }

        /// <summary>
        /// 풍선의 그룹 ID 가져오기
        /// </summary>
        public static string GetGroupId(GimmickInstanceData data)
        {
            return data?.GetParam(PARAM_GROUP_ID, "") ?? "";
        }
    }
}
