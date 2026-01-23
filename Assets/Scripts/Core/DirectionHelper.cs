using System.Collections.Generic;
using UnityEngine;

namespace BalloonOut.Core
{
    /// <summary>
    /// 방향 관련 유틸리티
    /// </summary>
    public static class DirectionHelper
    {
        /// <summary>
        /// 방향별 벡터
        /// Unity 좌표계 기준 (Y 위가 양수)
        /// </summary>
        public static readonly Dictionary<Direction, Vector2Int> Vectors = new()
        {
            { Direction.U, new Vector2Int(0, 1) },
            { Direction.D, new Vector2Int(0, -1) },
            { Direction.L, new Vector2Int(-1, 0) },
            { Direction.R, new Vector2Int(1, 0) }
        };

        /// <summary>
        /// 반대 방향
        /// </summary>
        public static readonly Dictionary<Direction, Direction> Opposite = new()
        {
            { Direction.U, Direction.D },
            { Direction.D, Direction.U },
            { Direction.L, Direction.R },
            { Direction.R, Direction.L }
        };

        /// <summary>
        /// 방향별 회전 각도 (Z축 기준, 도 단위)
        /// </summary>
        public static readonly Dictionary<Direction, float> Rotation = new()
        {
            { Direction.U, 0f },
            { Direction.D, 180f },
            { Direction.L, 90f },
            { Direction.R, -90f }
        };

        /// <summary>
        /// 문자열을 Direction으로 변환
        /// </summary>
        public static Direction FromString(string dir)
        {
            return dir.ToUpper() switch
            {
                "U" => Direction.U,
                "D" => Direction.D,
                "L" => Direction.L,
                "R" => Direction.R,
                _ => Direction.R
            };
        }

        /// <summary>
        /// Direction을 문자열로 변환
        /// </summary>
        public static string ToString(Direction dir)
        {
            return dir switch
            {
                Direction.U => "U",
                Direction.D => "D",
                Direction.L => "L",
                Direction.R => "R",
                _ => "R"
            };
        }
    }
}