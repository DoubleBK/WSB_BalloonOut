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

        /// <summary>그리드 크기 (정사각형 호환용)</summary>
        public int gridSize;

        /// <summary>그리드 가로 크기 (직사각형용)</summary>
        public int gridWidth;

        /// <summary>그리드 세로 크기 (직사각형용)</summary>
        public int gridHeight;

        /// <summary>풍선 Queue (Lane별)</summary>
        public List<LaneData> lanes;

        /// <summary>
        /// 그리드 가로 크기 반환 (호환성 처리)
        /// gridWidth가 설정되어 있으면 사용, 없으면 gridSize 사용
        /// </summary>
        public int GetGridWidth() => gridWidth > 0 ? gridWidth : gridSize;

        /// <summary>
        /// 그리드 세로 크기 반환 (호환성 처리)
        /// gridHeight가 설정되어 있으면 사용, 없으면 gridSize 사용
        /// </summary>
        public int GetGridHeight() => gridHeight > 0 ? gridHeight : gridSize;

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