using System.Collections.Generic;
using UnityEngine;

namespace BalloonOut.Core
{
    /// <summary>
    /// 게임 색상 관련 유틸리티
    /// </summary>
    public static class ColorHelper
    {
        /// <summary>
        /// GameColor에 해당하는 Unity Color
        /// </summary>
        public static readonly Dictionary<GameColor, Color> Colors = new()
        {
            { GameColor.R, new Color(1f, 0.275f, 0.341f) },      // #ff4757
            { GameColor.G, new Color(0.18f, 0.835f, 0.451f) },   // #2ed573
            { GameColor.Y, new Color(1f, 0.647f, 0.008f) },      // #ffa502
            { GameColor.B, new Color(0.325f, 0.322f, 0.929f) },  // #5352ed
            { GameColor.P, new Color(0.557f, 0.267f, 0.678f) }   // #8e44ad
        };

        /// <summary>
        /// 문자열을 GameColor로 변환
        /// </summary>
        public static GameColor FromString(string color)
        {
            return color.ToUpper() switch
            {
                "R" => GameColor.R,
                "G" => GameColor.G,
                "Y" => GameColor.Y,
                "B" => GameColor.B,
                "P" => GameColor.P,
                _ => GameColor.R
            };
        }

        /// <summary>
        /// GameColor를 문자열로 변환
        /// </summary>
        public static string ToString(GameColor color)
        {
            return color switch
            {
                GameColor.R => "R",
                GameColor.G => "G",
                GameColor.Y => "Y",
                GameColor.B => "B",
                GameColor.P => "P",
                _ => "R"
            };
        }

        /// <summary>
        /// GameColor에 해당하는 Unity Color 반환
        /// </summary>
        public static Color GetColor(GameColor gameColor)
        {
            return Colors.TryGetValue(gameColor, out var color) ? color : Color.white;
        }
    }
}