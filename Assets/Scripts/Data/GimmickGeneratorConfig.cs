using System;
using System.Collections.Generic;
using UnityEngine;
using BalloonOut.Game.Gimmick;

namespace BalloonOut.Data
{
    /// <summary>
    /// 기믹 자동 생성 설정 (개수 기반)
    /// 각 기믹별로 활성화 여부, 생성 개수, 파라미터 설정 가능
    /// </summary>
    [Serializable]
    public class GimmickGeneratorConfig
    {
        // ========== 기본 설정 ==========

        /// <summary>
        /// 기믹 ID (예: "surprise", "number")
        /// </summary>
        public string gimmickId;

        /// <summary>
        /// 활성화 여부
        /// </summary>
        public bool enabled;

        // ========== 개수 기반 설정 ==========

        /// <summary>
        /// Surprise 기믹: 생성할 개수
        /// </summary>
        public int count;

        /// <summary>
        /// Number 기믹: 각 풍선별 hit count
        /// 리스트 크기 = 생성할 Number 풍선 개수
        /// 예: [2, 3, 4] → 3개 Number 풍선, 각각 2, 3, 4 hits
        /// </summary>
        public List<int> hitCounts;

        /// <summary>
        /// Connected 기믹: 각 그룹별 풍선 수
        /// 리스트 크기 = 생성할 그룹 개수
        /// 예: [3, 2] → 2개 그룹 (3개짜리 1그룹, 2개짜리 1그룹)
        /// </summary>
        public List<int> groupSizes;

        // ========== 예비 파라미터 (향후 확장용) ==========

        public int intParam1;
        public float floatParam1;
        public string stringParam1;

        // ========== 생성자 ==========

        public GimmickGeneratorConfig()
        {
            gimmickId = "";
            enabled = false;
            count = 0;
            hitCounts = new List<int>();
            groupSizes = new List<int>();
            intParam1 = 0;
            floatParam1 = 0f;
            stringParam1 = "";
        }

        public GimmickGeneratorConfig(string id)
        {
            gimmickId = id;
            enabled = false;
            count = 0;
            hitCounts = new List<int>();
            groupSizes = new List<int>();
            intParam1 = 0;
            floatParam1 = 0f;
            stringParam1 = "";

            // 기믹별 기본값 설정
            ApplyDefaultsForGimmick(id);
        }

        // ========== 메서드 ==========

        /// <summary>
        /// 기믹별 기본값 적용
        /// </summary>
        private void ApplyDefaultsForGimmick(string id)
        {
            switch (id)
            {
                case "surprise":
                    count = 0;  // 기본: 비활성화
                    break;

                case "number":
                    hitCounts = new List<int>();  // 기본: 빈 리스트
                    break;

                case "connected":
                    groupSizes = new List<int>();  // 기본: 빈 리스트
                    break;
            }
        }

        /// <summary>
        /// Number 기믹의 생성 개수 (hitCounts 리스트 크기)
        /// </summary>
        public int GetNumberCount()
        {
            return hitCounts?.Count ?? 0;
        }

        /// <summary>
        /// Number 기믹의 추가 화살표 수 계산
        /// 각 hitCount - 1의 합계 (기본 1개 제외)
        /// </summary>
        public int GetExtraArrowCount()
        {
            if (gimmickId != "number" || hitCounts == null || hitCounts.Count == 0)
                return 0;

            int extra = 0;
            foreach (var hitCount in hitCounts)
            {
                extra += Mathf.Max(0, hitCount - 1);
            }
            return extra;
        }

        /// <summary>
        /// 지정된 인덱스의 hit count로 GimmickInstanceData 생성
        /// Number 기믹용
        /// </summary>
        public GimmickInstanceData ToInstanceData(int hitCountIndex = 0)
        {
            var data = new GimmickInstanceData(gimmickId);

            switch (gimmickId)
            {
                case "number":
                    if (hitCounts != null && hitCountIndex < hitCounts.Count)
                    {
                        int hits = Mathf.Max(1, hitCounts[hitCountIndex]);
                        data.SetParam("requiredHits", hits);
                        data.SetParam("currentHits", hits);
                    }
                    else
                    {
                        // 기본값
                        data.SetParam("requiredHits", 2);
                        data.SetParam("currentHits", 2);
                    }
                    break;

                case "surprise":
                    data.SetParam("isRevealed", false);
                    break;

                case "connected":
                    // Connected는 CreateConnectedInstance 사용 권장
                    data.SetParam("groupId", "");
                    data.SetParam("isMarked", false);
                    data.SetParam("groupSize", 0);
                    break;
            }

            return data;
        }

        /// <summary>
        /// Surprise 기믹용 간단 생성
        /// </summary>
        public GimmickInstanceData CreateSurpriseInstance()
        {
            var data = new GimmickInstanceData("surprise");
            data.SetParam("isRevealed", false);
            return data;
        }

        /// <summary>
        /// Number 기믹용 생성 (특정 hitCount)
        /// </summary>
        public GimmickInstanceData CreateNumberInstance(int hitCount)
        {
            var data = new GimmickInstanceData("number");
            int hits = Mathf.Max(1, hitCount);
            data.SetParam("requiredHits", hits);
            data.SetParam("currentHits", hits);
            return data;
        }

        /// <summary>
        /// 복사본 생성
        /// </summary>
        public GimmickGeneratorConfig Clone()
        {
            var clone = new GimmickGeneratorConfig
            {
                gimmickId = this.gimmickId,
                enabled = this.enabled,
                count = this.count,
                hitCounts = this.hitCounts != null ? new List<int>(this.hitCounts) : new List<int>(),
                groupSizes = this.groupSizes != null ? new List<int>(this.groupSizes) : new List<int>(),
                intParam1 = this.intParam1,
                floatParam1 = this.floatParam1,
                stringParam1 = this.stringParam1
            };
            return clone;
        }

        /// <summary>
        /// 기믹 표시 이름 반환
        /// </summary>
        public string GetDisplayName()
        {
            var def = GimmickRegistry.Instance?.GetDefinition(gimmickId);
            return def?.displayName ?? gimmickId;
        }

        /// <summary>
        /// 현재 설정으로 생성될 기믹 개수
        /// </summary>
        public int GetTotalCount()
        {
            if (!enabled) return 0;

            switch (gimmickId)
            {
                case "surprise":
                    return count;
                case "number":
                    return hitCounts?.Count ?? 0;
                case "connected":
                    return GetConnectedTotalBalloonCount();
                default:
                    return 0;
            }
        }

        // ========== Connected 기믹 전용 메서드 ==========

        /// <summary>
        /// Connected 기믹의 총 풍선 수 (모든 그룹의 풍선 수 합계)
        /// </summary>
        public int GetConnectedTotalBalloonCount()
        {
            if (gimmickId != "connected" || groupSizes == null || groupSizes.Count == 0)
                return 0;

            int total = 0;
            foreach (var size in groupSizes)
            {
                total += Mathf.Max(2, size);  // 최소 2개
            }
            return total;
        }

        /// <summary>
        /// Connected 기믹의 그룹 수
        /// </summary>
        public int GetConnectedGroupCount()
        {
            return groupSizes?.Count ?? 0;
        }

        /// <summary>
        /// Connected 기믹용 GimmickInstanceData 생성
        /// </summary>
        /// <param name="groupId">그룹 고유 ID</param>
        /// <param name="groupSize">그룹 내 총 풍선 수</param>
        public GimmickInstanceData CreateConnectedInstance(string groupId, int groupSize)
        {
            var data = new GimmickInstanceData("connected");
            data.SetParam("groupId", groupId);
            data.SetParam("isMarked", false);
            data.SetParam("groupSize", groupSize);
            return data;
        }
    }
}