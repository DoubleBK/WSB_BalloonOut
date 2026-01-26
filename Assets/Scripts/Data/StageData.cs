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

        [Header("풍선 Queue")]
        public List<LaneData> lanes = new List<LaneData>();

        [Header("화살표")]
        public List<ArrowData> arrows = new List<ArrowData>();

        [Header("통계")]
        public LevelStats stats;

        /// <summary>
        /// LevelData로 변환 (기존 시스템 호환)
        /// </summary>
        public LevelData ToLevelData()
        {
            return new LevelData
            {
                name = stageName,
                gridSize = gridSize,
                lanes = new List<LaneData>(lanes),
                arrows = new List<ArrowData>(arrows),
                stats = stats
            };
        }

        /// <summary>
        /// LevelData에서 복사
        /// </summary>
        public void CopyFrom(LevelData levelData)
        {
            stageName = levelData.name;
            gridSize = levelData.gridSize;
            lanes = levelData.lanes != null ? new List<LaneData>(levelData.lanes) : new List<LaneData>();
            arrows = levelData.arrows != null ? new List<ArrowData>(levelData.arrows) : new List<ArrowData>();
            stats = levelData.stats;
        }
    }
}