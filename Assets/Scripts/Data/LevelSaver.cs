using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;

namespace BalloonOut.Data
{
    /// <summary>
    /// 레벨 JSON 저장/불러오기 유틸리티
    /// </summary>
    public static class LevelSaver
    {
        // ========== 경로 설정 ==========
        private const string LEVELS_FOLDER = "Levels";
        private const string FILE_EXTENSION = ".json";

        /// <summary>
        /// 저장 경로 반환 (에디터: Resources, 빌드: persistentDataPath)
        /// </summary>
        public static string GetSavePath()
        {
#if UNITY_EDITOR
            return Path.Combine(Application.dataPath, "Resources", LEVELS_FOLDER);
#else
            return Path.Combine(Application.persistentDataPath, LEVELS_FOLDER);
#endif
        }

        // ========== 저장 ==========

        /// <summary>
        /// 레벨 데이터를 JSON 파일로 저장
        /// </summary>
        public static bool Save(LevelData levelData, string fileName = null)
        {
            if (levelData == null)
            {
                Debug.LogError("[LevelSaver] LevelData is null");
                return false;
            }

            string name = string.IsNullOrEmpty(fileName) ? levelData.name : fileName;
            if (string.IsNullOrEmpty(name))
            {
                Debug.LogError("[LevelSaver] Level name is empty");
                return false;
            }

            // 통계 업데이트
            UpdateStats(levelData);

            try
            {
                string savePath = GetSavePath();

                // 폴더 생성
                if (!Directory.Exists(savePath))
                {
                    Directory.CreateDirectory(savePath);
                }

                string filePath = Path.Combine(savePath, name + FILE_EXTENSION);
                string json = JsonUtility.ToJson(levelData, true);

                File.WriteAllText(filePath, json);
                Debug.Log($"[LevelSaver] Level saved: {filePath}");

#if UNITY_EDITOR
                UnityEditor.AssetDatabase.Refresh();
#endif

                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[LevelSaver] Save failed: {e.Message}");
                return false;
            }
        }

        // ========== 불러오기 ==========

        /// <summary>
        /// JSON 파일에서 레벨 데이터 로드
        /// </summary>
        public static LevelData Load(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                Debug.LogError("[LevelSaver] File name is empty");
                return null;
            }

            // Resources에서 먼저 시도
            var textAsset = Resources.Load<TextAsset>($"{LEVELS_FOLDER}/{fileName}");
            if (textAsset != null)
            {
                try
                {
                    var levelData = JsonUtility.FromJson<LevelData>(textAsset.text);
                    Debug.Log($"[LevelSaver] Level loaded from Resources: {fileName}");
                    return levelData;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[LevelSaver] Parse error (Resources): {e.Message}");
                }
            }

            // persistentDataPath에서 시도
            string filePath = Path.Combine(GetSavePath(), fileName + FILE_EXTENSION);
            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    var levelData = JsonUtility.FromJson<LevelData>(json);
                    Debug.Log($"[LevelSaver] Level loaded from file: {filePath}");
                    return levelData;
                }
                catch (Exception e)
                {
                    Debug.LogError($"[LevelSaver] Parse error (File): {e.Message}");
                }
            }

            Debug.LogWarning($"[LevelSaver] Level not found: {fileName}");
            return null;
        }

        // ========== 레벨 목록 ==========

        /// <summary>
        /// 저장된 레벨 목록 반환
        /// </summary>
        public static List<string> GetLevelList()
        {
            var levels = new List<string>();

            // Resources에서 레벨 목록 (에디터에서만 작동)
#if UNITY_EDITOR
            string resourcesPath = Path.Combine(Application.dataPath, "Resources", LEVELS_FOLDER);
            if (Directory.Exists(resourcesPath))
            {
                var files = Directory.GetFiles(resourcesPath, "*" + FILE_EXTENSION);
                foreach (var file in files)
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    if (!levels.Contains(name))
                    {
                        levels.Add(name);
                    }
                }
            }
#endif

            // persistentDataPath에서 레벨 목록
            string savePath = GetSavePath();
            if (Directory.Exists(savePath))
            {
                var files = Directory.GetFiles(savePath, "*" + FILE_EXTENSION);
                foreach (var file in files)
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    if (!levels.Contains(name))
                    {
                        levels.Add(name);
                    }
                }
            }

            levels.Sort();
            return levels;
        }

        /// <summary>
        /// 레벨 파일 삭제
        /// </summary>
        public static bool Delete(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return false;
            }

            bool deleted = false;

            // persistentDataPath에서 삭제
            string filePath = Path.Combine(GetSavePath(), fileName + FILE_EXTENSION);
            if (File.Exists(filePath))
            {
                try
                {
                    File.Delete(filePath);
                    deleted = true;
                    Debug.Log($"[LevelSaver] Level deleted: {filePath}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[LevelSaver] Delete failed: {e.Message}");
                }
            }

#if UNITY_EDITOR
            // Resources에서 삭제 (에디터에서만)
            string resourcesPath = Path.Combine(Application.dataPath, "Resources", LEVELS_FOLDER, fileName + FILE_EXTENSION);
            if (File.Exists(resourcesPath))
            {
                try
                {
                    File.Delete(resourcesPath);
                    string metaPath = resourcesPath + ".meta";
                    if (File.Exists(metaPath))
                    {
                        File.Delete(metaPath);
                    }
                    UnityEditor.AssetDatabase.Refresh();
                    deleted = true;
                    Debug.Log($"[LevelSaver] Level deleted from Resources: {resourcesPath}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[LevelSaver] Delete from Resources failed: {e.Message}");
                }
            }
#endif

            return deleted;
        }

        /// <summary>
        /// 레벨 이름 중복 확인
        /// </summary>
        public static bool Exists(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return false;
            }

            // Resources 확인
            var textAsset = Resources.Load<TextAsset>($"{LEVELS_FOLDER}/{fileName}");
            if (textAsset != null)
            {
                return true;
            }

            // 파일 확인
            string filePath = Path.Combine(GetSavePath(), fileName + FILE_EXTENSION);
            return File.Exists(filePath);
        }

        // ========== 유틸리티 ==========

        /// <summary>
        /// 새 레벨 데이터 생성
        /// </summary>
        public static LevelData CreateNew(string name, int gridSize = 6)
        {
            return new LevelData
            {
                name = name,
                gridSize = gridSize,
                lanes = new List<LaneData>(),
                arrows = new List<ArrowData>(),
                stats = new LevelStats()
            };
        }

        /// <summary>
        /// 레벨 통계 업데이트
        /// </summary>
        private static void UpdateStats(LevelData levelData)
        {
            if (levelData.stats == null)
            {
                levelData.stats = new LevelStats();
            }

            int mainArrows = 0;
            int fillers = 0;

            if (levelData.arrows != null)
            {
                foreach (var arrow in levelData.arrows)
                {
                    if (arrow.isFiller)
                    {
                        fillers++;
                    }
                    else
                    {
                        mainArrows++;
                    }
                }
            }

            levelData.stats.mainArrows = mainArrows;
            levelData.stats.fillers = fillers;
            levelData.stats.totalArrows = mainArrows + fillers;

            // 밀도 계산
            int totalCells = levelData.gridSize * levelData.gridSize;
            int occupiedCells = 0;

            if (levelData.arrows != null)
            {
                foreach (var arrow in levelData.arrows)
                {
                    occupiedCells += arrow.GetCells().Count;
                }
            }

            levelData.stats.density = totalCells > 0 ? (float)occupiedCells / totalCells : 0f;
        }
    }
}
