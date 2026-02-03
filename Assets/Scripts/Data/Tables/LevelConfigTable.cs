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
        public int colorCount = 6;  // 사용할 색상 수 (4~12, 기본 6)

        // === 기믹 설정 ===
        public int surpriseCount = 0;               // Surprise 풍선 개수 (0이면 비활성)
        public List<int> numberHitCounts = null;    // Number 풍선별 hit count (null/빈 배열이면 비활성)
        public List<int> connectedGroupSizes = null; // Connected 그룹별 풍선 수 (null/빈 배열이면 비활성)

        /// <summary>
        /// GeneratorConfig로 변환 (기믹 설정 포함)
        /// </summary>
        public LevelGenerator.GeneratorConfig ToGeneratorConfig()
        {
            var config = new LevelGenerator.GeneratorConfig
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
                branchingChance = 0.4f,
                colorCount = colorCount > 0 ? colorCount : 6  // 기본값 6
            };

            // 기믹 설정 변환
            config.InitializeDefaultGimmicks();

            // Surprise 기믹
            var surpriseConfig = config.balloonGimmicks.Find(g => g.gimmickId == "surprise");
            if (surpriseConfig != null)
            {
                surpriseConfig.enabled = surpriseCount > 0;
                surpriseConfig.count = surpriseCount;
            }

            // Number 기믹
            var numberConfig = config.balloonGimmicks.Find(g => g.gimmickId == "number");
            if (numberConfig != null)
            {
                numberConfig.enabled = numberHitCounts != null && numberHitCounts.Count > 0;
                numberConfig.hitCounts = numberHitCounts ?? new List<int>();
            }

            // Connected 기믹
            var connectedConfig = config.balloonGimmicks.Find(g => g.gimmickId == "connected");
            if (connectedConfig != null)
            {
                connectedConfig.enabled = connectedGroupSizes != null && connectedGroupSizes.Count > 0;
                connectedConfig.groupSizes = connectedGroupSizes ?? new List<int>();
            }

            return config;
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