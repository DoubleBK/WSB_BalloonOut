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
    /// Lane 데이터 (풍선 배열)
    /// </summary>
    [Serializable]
    public class LaneData
    {
        /// <summary>
        /// 풍선 색상 배열 (레거시 형식)
        /// </summary>
        public List<string> balloons;

        /// <summary>
        /// 풍선 데이터 배열 (기믹 지원 형식)
        /// </summary>
        public List<BalloonData> balloonData;

        public LaneData()
        {
            balloons = new List<string>();
            balloonData = new List<BalloonData>();
        }

        public LaneData(List<string> balloons)
        {
            this.balloons = balloons;
            this.balloonData = new List<BalloonData>();
        }

        /// <summary>
        /// BalloonData 리스트 반환 (하위 호환성 지원)
        /// balloonData가 있으면 사용, 없으면 balloons에서 변환
        /// </summary>
        public List<BalloonData> GetBalloonDataList()
        {
            // balloonData가 있으면 우선 사용
            if (balloonData != null && balloonData.Count > 0)
            {
                return balloonData;
            }

            // 레거시 형식에서 변환
            var result = new List<BalloonData>();
            if (balloons != null)
            {
                foreach (var colorCode in balloons)
                {
                    result.Add(new BalloonData(colorCode));
                }
            }
            return result;
        }

        /// <summary>
        /// GameColor 리스트로 변환 (레거시 호환)
        /// </summary>
        public List<GameColor> GetColors()
        {
            var balloonList = GetBalloonDataList();
            var colors = new List<GameColor>();
            foreach (var b in balloonList)
            {
                colors.Add(b.GetColor());
            }
            return colors;
        }

        /// <summary>
        /// 풍선 개수
        /// </summary>
        public int Count
        {
            get
            {
                if (balloonData != null && balloonData.Count > 0)
                {
                    return balloonData.Count;
                }
                return balloons?.Count ?? 0;
            }
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