using System.Collections.Generic;
using UnityEngine;

namespace BalloonOut.Data
{
    /// <summary>
    /// StageTable 기반 스테이지 로더
    /// </summary>
    public static class StageLoader
    {
        private const string STAGE_TABLE_PATH = "Tables/StageTable";
        private const string STAGES_FOLDER = "ScriptableObjects/Stages";

        private static StageTableData _cachedTable;
        private static Dictionary<int, StageTableEntry> _levelIndexMap;

        /// <summary>
        /// StageTable 로드 (캐싱)
        /// </summary>
        public static StageTableData LoadTable()
        {
            if (_cachedTable != null)
                return _cachedTable;

            var textAsset = Resources.Load<TextAsset>(STAGE_TABLE_PATH);
            if (textAsset == null)
            {
                Debug.LogError("[StageLoader] StageTable.json not found in Resources!");
                return null;
            }

            _cachedTable = StageTableData.FromJsonArray(textAsset.text);
            BuildIndexMap();

            Debug.Log($"[StageLoader] StageTable loaded: {_cachedTable.stages.Count} entries");
            return _cachedTable;
        }

        /// <summary>
        /// LevelIdx → StageTableEntry 맵 구축
        /// </summary>
        private static void BuildIndexMap()
        {
            _levelIndexMap = new Dictionary<int, StageTableEntry>();
            foreach (var entry in _cachedTable.stages)
            {
                if (!_levelIndexMap.ContainsKey(entry.LevelIdx))
                {
                    _levelIndexMap[entry.LevelIdx] = entry;
                }
            }
        }

        /// <summary>
        /// LevelIdx로 스테이지 로드
        /// </summary>
        public static LevelData LoadByLevelIdx(int levelIdx)
        {
            var table = LoadTable();
            if (table == null) return null;

            if (!_levelIndexMap.TryGetValue(levelIdx, out var entry))
            {
                Debug.LogError($"[StageLoader] LevelIdx {levelIdx} not found in StageTable");
                return null;
            }

            return LoadByStageIdx(entry.StageIdx);
        }

        /// <summary>
        /// StageIdx로 스테이지 로드
        /// </summary>
        public static LevelData LoadByStageIdx(int stageIdx)
        {
            string assetName = $"stage_{stageIdx:D6}";
            string assetPath = $"{STAGES_FOLDER}/{assetName}";

            var stageData = Resources.Load<StageData>(assetPath);
            if (stageData == null)
            {
                Debug.LogError($"[StageLoader] Stage asset not found: {assetPath}");
                return null;
            }

            Debug.Log($"[StageLoader] Stage loaded: {assetName}");
            return stageData.ToLevelData();
        }

        /// <summary>
        /// StageTableEntry 조회
        /// </summary>
        public static StageTableEntry GetEntryByLevelIdx(int levelIdx)
        {
            var table = LoadTable();
            if (table == null) return null;

            _levelIndexMap.TryGetValue(levelIdx, out var entry);
            return entry;
        }

        /// <summary>
        /// 난이도별 레벨 목록 조회
        /// </summary>
        public static List<StageTableEntry> GetLevelsByDifficulty(StageDifficulty difficulty)
        {
            var table = LoadTable();
            if (table == null) return new List<StageTableEntry>();

            var result = new List<StageTableEntry>();
            string diffStr = difficulty.ToString();

            foreach (var entry in table.stages)
            {
                if (entry.Difficulty == diffStr)
                {
                    result.Add(entry);
                }
            }

            return result;
        }

        /// <summary>
        /// 전체 레벨 개수
        /// </summary>
        public static int GetTotalLevelCount()
        {
            var table = LoadTable();
            return table?.stages.Count ?? 0;
        }

        /// <summary>
        /// 캐시 초기화
        /// </summary>
        public static void ClearCache()
        {
            _cachedTable = null;
            _levelIndexMap = null;
        }
    }
}