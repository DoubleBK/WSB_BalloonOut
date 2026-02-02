using System;
using System.Collections.Generic;
using System.Linq;
using BalloonOut.Core;

namespace BalloonOut.Data
{
    /// <summary>
    /// 풍선 데이터 (직렬화 가능)
    /// 색상과 기믹 정보를 포함
    /// </summary>
    [Serializable]
    public class BalloonData
    {
        /// <summary>
        /// 풍선 색상 코드 (예: "R", "G", "B")
        /// </summary>
        public string color;

        /// <summary>
        /// 적용된 기믹들
        /// </summary>
        public List<GimmickInstanceData> gimmicks;

        public BalloonData()
        {
            color = "";
            gimmicks = new List<GimmickInstanceData>();
        }

        public BalloonData(string colorCode)
        {
            color = colorCode;
            gimmicks = new List<GimmickInstanceData>();
        }

        /// <summary>
        /// GameColor enum으로 변환
        /// </summary>
        public GameColor GetColor()
        {
            return ColorHelper.FromString(color);
        }

        /// <summary>
        /// 특정 기믹이 있는지 확인
        /// </summary>
        public bool HasGimmick(string gimmickId)
        {
            return gimmicks != null && gimmicks.Any(g => g.gimmickId == gimmickId);
        }

        /// <summary>
        /// 특정 기믹 데이터 가져오기
        /// </summary>
        public GimmickInstanceData GetGimmick(string gimmickId)
        {
            return gimmicks?.FirstOrDefault(g => g.gimmickId == gimmickId);
        }

        /// <summary>
        /// 기믹 추가
        /// </summary>
        public void AddGimmick(GimmickInstanceData gimmick)
        {
            if (gimmicks == null)
            {
                gimmicks = new List<GimmickInstanceData>();
            }
            gimmicks.Add(gimmick);
        }

        /// <summary>
        /// 기믹 제거
        /// </summary>
        public bool RemoveGimmick(string gimmickId)
        {
            if (gimmicks == null) return false;
            return gimmicks.RemoveAll(g => g.gimmickId == gimmickId) > 0;
        }

        /// <summary>
        /// 복제
        /// </summary>
        public BalloonData Clone()
        {
            var clone = new BalloonData(color);
            if (gimmicks != null)
            {
                clone.gimmicks = gimmicks.Select(g => g.Clone()).ToList();
            }
            return clone;
        }

        /// <summary>
        /// 색상 문자열에서 암시적 변환 (하위 호환성)
        /// </summary>
        public static implicit operator BalloonData(string colorCode)
        {
            return new BalloonData(colorCode);
        }
    }
}
