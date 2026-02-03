using System;
using System.Collections.Generic;
using UnityEngine;

namespace BalloonOut.Data
{
    /// <summary>
    /// 스테이지 데이터 (ScriptableObject)
    /// </summary>
    [CreateAssetMenu(fileName = "stage_000000", menuName = "BalloonOut/Stage Data")]
    public class StageData : ScriptableObject
    {
        [Header("기본 정보")]
        public string stageName;
        public int gridSize = 6;
        public int gridWidth;   // 직사각형 그리드용 (0이면 gridSize 사용)
        public int gridHeight;  // 직사각형 그리드용 (0이면 gridSize 사용)

        [Header("풍선 Queue")]
        public List<LaneData> lanes = new List<LaneData>();

        [Header("화살표")]
        public List<ArrowData> arrows = new List<ArrowData>();

        [Header("통계")]
        public LevelStats stats;

        /// <summary>
        /// LevelData로 변환 (직사각형 지원, 하위 호환성 유지)
        /// </summary>
        public LevelData ToLevelData()
        {
            return new LevelData
            {
                name = stageName,
                gridSize = gridSize,
                // gridWidth/gridHeight가 0이면 gridSize 사용 (하위 호환성)
                gridWidth = gridWidth > 0 ? gridWidth : gridSize,
                gridHeight = gridHeight > 0 ? gridHeight : gridSize,
                lanes = new List<LaneData>(lanes),
                arrows = new List<ArrowData>(arrows),
                stats = stats
            };
        }

        /// <summary>
        /// LevelData에서 복사 (직사각형 지원)
        /// </summary>
        public void CopyFrom(LevelData levelData)
        {
            stageName = levelData.name;
            gridSize = levelData.gridSize;
            gridWidth = levelData.gridWidth;
            gridHeight = levelData.gridHeight;
            lanes = levelData.lanes != null ? new List<LaneData>(levelData.lanes) : new List<LaneData>();
            arrows = levelData.arrows != null ? new List<ArrowData>(levelData.arrows) : new List<ArrowData>();
            stats = levelData.stats;
        }
    }
}