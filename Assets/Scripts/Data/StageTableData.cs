using System;
using System.Collections.Generic;
using UnityEngine;

namespace BalloonOut.Data
{
    /// <summary>
    /// 난이도 열거형
    /// </summary>
    public enum StageDifficulty
    {
        Normal,
        Hard,
        Nightmare
    }

    /// <summary>
    /// StageTable 항목
    /// </summary>
    [Serializable]
    public class StageTableEntry
    {
        /// <summary>게임 레벨 번호 (1, 2, 3, ...)</summary>
        public int LevelIdx;

        /// <summary>.asset 파일 번호</summary>
        public int StageIdx;

        /// <summary>난이도 ("Normal", "Hard", "Nightmare")</summary>
        public string Difficulty;

        /// <summary>
        /// 난이도 enum 반환
        /// </summary>
        public StageDifficulty GetDifficulty()
        {
            return Difficulty switch
            {
                "Hard" => StageDifficulty.Hard,
                "Nightmare" => StageDifficulty.Nightmare,
                _ => StageDifficulty.Normal
            };
        }
    }

    /// <summary>
    /// StageTable 전체 데이터 (JsonUtility용 래퍼)
    /// </summary>
    [Serializable]
    public class StageTableData
    {
        public List<StageTableEntry> stages = new List<StageTableEntry>();

        /// <summary>
        /// JSON 배열 문자열에서 StageTableData 파싱
        /// JsonUtility는 최상위 배열을 직접 파싱할 수 없으므로 래핑 처리
        /// </summary>
        public static StageTableData FromJsonArray(string jsonArray)
        {
            // JsonUtility는 최상위 배열을 파싱할 수 없으므로 래핑
            string wrappedJson = "{\"stages\":" + jsonArray + "}";
            return JsonUtility.FromJson<StageTableData>(wrappedJson);
        }
    }
}