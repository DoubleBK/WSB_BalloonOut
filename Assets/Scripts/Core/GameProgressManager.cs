using UnityEngine;

namespace BalloonOut.Core
{
    /// <summary>
    /// 게임 진행 상황 저장/로드 (PlayerPrefs 기반)
    /// </summary>
    public static class GameProgressManager
    {
        // LobbyUI와 동일한 키 사용
        private const string KEY_CURRENT_LEVEL = "CurrentLevel";
        private const string KEY_MAX_CLEARED_LEVEL = "MaxClearedLevel";

        // LobbyUI의 Level Up 연출을 위한 이전 레벨 저장
        private const string KEY_PREVIOUS_LEVEL = "PreviousLevel";

        /// <summary>
        /// 현재 레벨 인덱스 저장
        /// </summary>
        public static void SaveCurrentLevel(int levelIdx)
        {
            PlayerPrefs.SetInt(KEY_CURRENT_LEVEL, levelIdx);
            PlayerPrefs.Save();
            Debug.Log($"[GameProgressManager] Current level saved: {levelIdx}");
        }

        /// <summary>
        /// 현재 레벨 인덱스 로드
        /// </summary>
        public static int LoadCurrentLevel(int defaultLevel = 1)
        {
            return PlayerPrefs.GetInt(KEY_CURRENT_LEVEL, defaultLevel);
        }

        /// <summary>
        /// 레벨 클리어 시 호출 - 다음 레벨로 진행
        /// </summary>
        public static void OnLevelCleared(int clearedLevelIdx)
        {
            // 이전 레벨 저장 (LobbyUI Level Up 연출용)
            PlayerPrefs.SetInt(KEY_PREVIOUS_LEVEL, clearedLevelIdx);

            int nextLevel = clearedLevelIdx + 1;
            SaveCurrentLevel(nextLevel);

            // 최대 클리어 레벨 업데이트
            int maxCleared = PlayerPrefs.GetInt(KEY_MAX_CLEARED_LEVEL, 0);
            if (clearedLevelIdx > maxCleared)
            {
                PlayerPrefs.SetInt(KEY_MAX_CLEARED_LEVEL, clearedLevelIdx);
                PlayerPrefs.Save();
            }

            Debug.Log($"[GameProgressManager] Level {clearedLevelIdx} cleared! Next: {nextLevel}");
        }

        /// <summary>
        /// 이전 레벨 조회 (Level Up 연출용)
        /// </summary>
        public static int GetPreviousLevel()
        {
            return PlayerPrefs.GetInt(KEY_PREVIOUS_LEVEL, -1);
        }

        /// <summary>
        /// 이전 레벨 초기화 (연출 완료 후)
        /// </summary>
        public static void ClearPreviousLevel()
        {
            PlayerPrefs.DeleteKey(KEY_PREVIOUS_LEVEL);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 최대 클리어 레벨 조회
        /// </summary>
        public static int GetMaxClearedLevel()
        {
            return PlayerPrefs.GetInt(KEY_MAX_CLEARED_LEVEL, 0);
        }

        /// <summary>
        /// 진행 상황 초기화 (테스트용)
        /// </summary>
        public static void ResetProgress()
        {
            PlayerPrefs.DeleteKey(KEY_CURRENT_LEVEL);
            PlayerPrefs.DeleteKey(KEY_MAX_CLEARED_LEVEL);
            PlayerPrefs.DeleteKey(KEY_PREVIOUS_LEVEL);
            PlayerPrefs.Save();
            Debug.Log("[GameProgressManager] Progress reset!");
        }
    }
}