using System;
using UnityEngine;
using BalloonOut.Game.Gimmick;

namespace BalloonOut.Data
{
    /// <summary>
    /// 기믹 자동 생성 설정
    /// 각 기믹별로 활성화 여부, 적용 확률, 파라미터 설정 가능
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

        /// <summary>
        /// 적용 확률 (0~1)
        /// </summary>
        [Range(0f, 1f)]
        public float chance;

        // ========== 기믹별 파라미터 ==========
        // Unity 직렬화를 위해 기본 타입 사용

        /// <summary>
        /// 정수 파라미터 1 (Number 기믹: minHits)
        /// </summary>
        public int intParam1;

        /// <summary>
        /// 정수 파라미터 2 (Number 기믹: maxHits)
        /// </summary>
        public int intParam2;

        /// <summary>
        /// 실수 파라미터 1 (향후 사용)
        /// </summary>
        public float floatParam1;

        /// <summary>
        /// 문자열 파라미터 1 (향후 사용)
        /// </summary>
        public string stringParam1;

        // ========== 생성자 ==========

        public GimmickGeneratorConfig()
        {
            gimmickId = "";
            enabled = false;
            chance = 0f;
            intParam1 = 0;
            intParam2 = 0;
            floatParam1 = 0f;
            stringParam1 = "";
        }

        public GimmickGeneratorConfig(string id)
        {
            gimmickId = id;
            enabled = false;
            chance = 0f;
            intParam1 = 0;
            intParam2 = 0;
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
                    chance = 0.2f;
                    break;

                case "number":
                    chance = 0.15f;
                    intParam1 = 2;  // minHits
                    intParam2 = 4;  // maxHits
                    break;
            }
        }

        /// <summary>
        /// 런타임에 GimmickInstanceData로 변환
        /// 확률 체크는 포함하지 않음 (호출자가 처리)
        /// </summary>
        public GimmickInstanceData ToInstanceData()
        {
            var data = new GimmickInstanceData(gimmickId);

            switch (gimmickId)
            {
                case "number":
                    // minHits ~ maxHits 사이의 랜덤 값
                    int minHits = Mathf.Max(1, intParam1);
                    int maxHits = Mathf.Max(minHits, intParam2);
                    int hits = UnityEngine.Random.Range(minHits, maxHits + 1);
                    data.SetParam("requiredHits", hits);
                    data.SetParam("currentHits", hits);
                    break;

                case "surprise":
                    // Surprise는 기본적으로 공개되지 않은 상태
                    data.SetParam("isRevealed", false);
                    break;
            }

            return data;
        }

        /// <summary>
        /// 확률 체크 후 GimmickInstanceData 생성
        /// 확률에 걸리지 않으면 null 반환
        /// </summary>
        public GimmickInstanceData TryCreateInstanceData()
        {
            if (!enabled || chance <= 0f)
                return null;

            if (UnityEngine.Random.value > chance)
                return null;

            return ToInstanceData();
        }

        /// <summary>
        /// 복사본 생성
        /// </summary>
        public GimmickGeneratorConfig Clone()
        {
            return new GimmickGeneratorConfig
            {
                gimmickId = this.gimmickId,
                enabled = this.enabled,
                chance = this.chance,
                intParam1 = this.intParam1,
                intParam2 = this.intParam2,
                floatParam1 = this.floatParam1,
                stringParam1 = this.stringParam1
            };
        }

        /// <summary>
        /// 기믹 표시 이름 반환
        /// </summary>
        public string GetDisplayName()
        {
            var def = GimmickRegistry.Instance?.GetDefinition(gimmickId);
            return def?.displayName ?? gimmickId;
        }
    }
}