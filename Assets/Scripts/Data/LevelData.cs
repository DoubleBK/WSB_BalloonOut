using System;
using System.Collections.Generic;
using UnityEngine;
using BalloonOut.Core;

namespace BalloonOut.Data
{
    /// <summary>
    /// 레벨 데이터 (JSON 직렬화용)
    /// </summary>
    [Serializable]
    public class LevelData
    {
        /// <summary>레벨 이름</summary>
        public string name;

        /// <summary>그리드 크기</summary>
        public int gridSize;

        /// <summary>풍선 Queue (Lane별)</summary>
        public List<LaneData> lanes;

        /// <summary>화살표 배열</summary>
        public List<ArrowData> arrows;

        /// <summary>레벨 통계</summary>
        public LevelStats stats;
    }

    /// <summary>
    /// Lane 데이터 (풍선 색상 배열)
    /// </summary>
    [Serializable]
    public class LaneData
    {
        public List<string> balloons;

        public LaneData()
        {
            balloons = new List<string>();
        }

        public LaneData(List<string> balloons)
        {
            this.balloons = balloons;
        }

        /// <summary>
        /// GameColor 리스트로 변환
        /// </summary>
        public List<GameColor> GetColors()
        {
            var colors = new List<GameColor>();
            foreach (var b in balloons)
            {
                colors.Add(ColorHelper.FromString(b));
            }
            return colors;
        }
    }

    /// <summary>
    /// 레벨 통계
    /// </summary>
    [Serializable]
    public class LevelStats
    {
        public float density;
        public int mainArrows;
        public int fillers;
        public int totalArrows;
    }
}