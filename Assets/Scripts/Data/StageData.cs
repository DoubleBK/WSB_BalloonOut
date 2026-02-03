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

        [Header("Generator 설정 (로드 시 복원용)")]
        public int genLaneCount;
        public int genBalloonsPerLane;
        public int genMissArrowCount;
        public int genDecoyArrowCount;
        public int genMinLength;
        public int genMaxLength;
        public float genTargetDensity;
        public bool genBendingEnabled;
        public float genBendingChance;
        public bool genBranchingMode;
        public float genBranchingChance;
        public int genColorCount;
        public bool genFillerEnabled;

        [Header("Gimmick 설정 (로드 시 복원용)")]
        public List<GimmickGeneratorConfig> genBalloonGimmicks = new List<GimmickGeneratorConfig>();
        public List<GimmickGeneratorConfig> genArrowGimmicks = new List<GimmickGeneratorConfig>();

        /// <summary>
        /// Generator 설정이 저장되어 있는지 확인
        /// </summary>
        public bool HasGeneratorConfig => genLaneCount > 0;

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