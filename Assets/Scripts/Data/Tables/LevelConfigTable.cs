using System;
using System.Collections.Generic;

namespace BalloonOut.Data
{
    /// <summary>
    /// 레벨별 Generator Config 레코드
    /// LevelConfigTable.json에서 로드
    /// </summary>
    [Serializable]
    public class LevelConfigRecord
    {
        public int level;
        public int gridSize;
        public int laneCount;
        public int balloonsPerLane;
        public int missArrowCount;
        public int decoyArrowCount;
        public int minBlockLength;
        public int maxBlockLength;
        public bool bendingEnabled;
        public float bendingChance;
        public float targetDensity;
        public int difficultyScore;

        /// <summary>
        /// GeneratorConfig로 변환
        /// </summary>
        public LevelGenerator.GeneratorConfig ToGeneratorConfig()
        {
            return new LevelGenerator.GeneratorConfig
            {
                gridSize = gridSize,
                laneCount = laneCount,
                balloonsPerLane = balloonsPerLane,
                missArrowCount = missArrowCount,
                decoyArrowCount = decoyArrowCount,
                minBlockLength = minBlockLength,
                maxBlockLength = maxBlockLength,
                bendingEnabled = bendingEnabled,
                bendingChance = bendingChance,
                targetDensity = targetDensity,
                fillerEnabled = false,
                branchingMode = false,
                branchingChance = 0.4f
            };
        }
    }

    /// <summary>
    /// JSON 배열 래퍼 (JsonUtility는 최상위 배열을 직접 파싱 불가)
    /// </summary>
    [Serializable]
    public class LevelConfigTableWrapper
    {
        public List<LevelConfigRecord> records;
    }

    /// <summary>
    /// LevelConfigTable JSON 로드 헬퍼
    /// </summary>
    public static class LevelConfigTableLoader
    {
        /// <summary>
        /// JSON 텍스트에서 LevelConfigRecord 리스트 로드
        /// </summary>
        public static List<LevelConfigRecord> Load(string jsonText)
        {
            string wrapped = "{\"records\":" + jsonText + "}";
            var wrapper = UnityEngine.JsonUtility.FromJson<LevelConfigTableWrapper>(wrapped);
            return wrapper?.records ?? new List<LevelConfigRecord>();
        }
    }
}