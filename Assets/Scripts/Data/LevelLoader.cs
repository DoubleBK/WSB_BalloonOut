using UnityEngine;

namespace BalloonOut.Data
{
    /// <summary>
    /// JSON 레벨 데이터 로더
    /// </summary>
    public static class LevelLoader
    {
        private const string LevelPath = "Levels/";

        /// <summary>
        /// 레벨 로드
        /// </summary>
        /// <param name="levelName">레벨 파일명 (확장자 없이)</param>
        /// <returns>LevelData 또는 null</returns>
        public static LevelData Load(string levelName)
        {
            var textAsset = Resources.Load<TextAsset>(LevelPath + levelName);
            if (textAsset == null)
            {
                Debug.LogError($"Level not found: {levelName}");
                return null;
            }

            try
            {
                var levelData = JsonUtility.FromJson<LevelData>(textAsset.text);
                Debug.Log($"Level loaded: {levelData.name}, GridSize: {levelData.gridSize}, Arrows: {levelData.arrows?.Count ?? 0}");
                return levelData;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to parse level: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// JSON 문자열에서 레벨 로드
        /// </summary>
        public static LevelData LoadFromJson(string json)
        {
            try
            {
                return JsonUtility.FromJson<LevelData>(json);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to parse level JSON: {e.Message}");
                return null;
            }
        }
    }
}