using System.Diagnostics;
using Debug = UnityEngine.Debug;

namespace BalloonOut.Core
{
    /// <summary>
    /// 조건부 로깅 유틸리티
    /// 릴리스 빌드에서 로그를 자동으로 제거하여 성능 향상
    /// </summary>
    public static class GameLogger
    {
        /// <summary>
        /// 일반 로그 (에디터 및 개발 빌드에서만 출력)
        /// </summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Log(string message)
        {
            Debug.Log(message);
        }

        /// <summary>
        /// 포맷 로그
        /// </summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void Log(string format, params object[] args)
        {
            Debug.Log(string.Format(format, args));
        }

        /// <summary>
        /// 경고 로그 (항상 출력)
        /// </summary>
        public static void LogWarning(string message)
        {
            Debug.LogWarning(message);
        }

        /// <summary>
        /// 에러 로그 (항상 출력)
        /// </summary>
        public static void LogError(string message)
        {
            Debug.LogError(message);
        }
    }
}
