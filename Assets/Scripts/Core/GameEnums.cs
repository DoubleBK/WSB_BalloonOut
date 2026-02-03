using System.Collections.Generic;
using UnityEngine;

namespace BalloonOut.Core
{
    /// <summary>
    /// 화살표 방향 (4방향)
    /// </summary>
    public enum ArrowDirection
    {
        Up = 0,
        Down = 1,
        Left = 2,
        Right = 3
    }

    /// <summary>
    /// 화살표/풍선 색상 (13색)
    /// </summary>
    public enum GameColor
    {
        Red = 0,
        Blue = 1,
        Green = 2,
        Yellow = 3,
        Purple = 4,
        Orange = 5,
        Cyan = 6,
        Pink = 7,
        Brown = 8,
        Lime = 9,
        Navy = 10,
        Magenta = 11,
        Black = 12
    }

    /// <summary>
    /// 화살표 상태
    /// </summary>
    public enum ArrowState
    {
        Idle,       // 대기 상태
        Dragging,   // 드래그 중
        Moving,     // 이동 중
        Extracted,  // 탈출 완료
        Homing,     // 호밍 중
        Destroyed   // 파괴됨
    }

    /// <summary>
    /// 풍선 상태
    /// </summary>
    public enum BalloonState
    {
        Active,     // 활성 상태
        Targeted,   // 타겟팅됨
        Popping,    // 터지는 중
        Popped      // 터짐 완료
    }

    /// <summary>
    /// 게임 상태
    /// </summary>
    public enum GameState
    {
        Loading,    // 로딩 중
        Ready,      // 준비 완료
        Playing,    // 플레이 중
        Paused,     // 일시정지
        Clear,      // 클리어
        Failed      // 실패
    }

    /// <summary>
    /// 레벨 난이도
    /// </summary>
    public enum Difficulty
    {
        Normal = 0,
        Hard = 1,
        Nightmare = 2
    }

    /// <summary>
    /// 방향 관련 유틸리티
    /// </summary>
    public static class DirectionHelper
    {
        public static readonly Dictionary<ArrowDirection, Vector2Int> Vectors = new()
        {
            { ArrowDirection.Up, Vector2Int.up },
            { ArrowDirection.Down, Vector2Int.down },
            { ArrowDirection.Left, Vector2Int.left },
            { ArrowDirection.Right, Vector2Int.right }
        };

        public static readonly Dictionary<ArrowDirection, ArrowDirection> Opposite = new()
        {
            { ArrowDirection.Up, ArrowDirection.Down },
            { ArrowDirection.Down, ArrowDirection.Up },
            { ArrowDirection.Left, ArrowDirection.Right },
            { ArrowDirection.Right, ArrowDirection.Left }
        };

        public static readonly Dictionary<ArrowDirection, float> Rotation = new()
        {
            { ArrowDirection.Up, 0f },
            { ArrowDirection.Down, 180f },
            { ArrowDirection.Left, 90f },
            { ArrowDirection.Right, -90f }
        };

        public static ArrowDirection FromString(string dir)
        {
            return dir.ToUpper() switch
            {
                "U" or "UP" => ArrowDirection.Up,
                "D" or "DOWN" => ArrowDirection.Down,
                "L" or "LEFT" => ArrowDirection.Left,
                "R" or "RIGHT" => ArrowDirection.Right,
                _ => ArrowDirection.Right
            };
        }

        public static string ToString(ArrowDirection dir)
        {
            return dir switch
            {
                ArrowDirection.Up => "U",
                ArrowDirection.Down => "D",
                ArrowDirection.Left => "L",
                ArrowDirection.Right => "R",
                _ => "R"
            };
        }
    }

    /// <summary>
    /// 색상 관련 유틸리티
    /// </summary>
    public static class ColorHelper
    {
        /// <summary>
        /// NEON HDR 색상 강도 (1.0 = 일반, 2.0+ = 발광)
        /// </summary>
        public static float NeonIntensity = 2.0f;

        /// <summary>
        /// 기본 색상 (표준 게임용 색상)
        /// </summary>
        private static readonly Dictionary<GameColor, Color> BaseColors = new()
        {
            { GameColor.Red, new Color(0.95f, 0.2f, 0.2f) },      // 선명한 빨강
            { GameColor.Blue, new Color(0.2f, 0.4f, 0.95f) },     // 선명한 파랑
            { GameColor.Green, new Color(0.2f, 0.85f, 0.3f) },    // 선명한 초록
            { GameColor.Yellow, new Color(1f, 0.9f, 0.15f) },     // 선명한 노랑
            { GameColor.Purple, new Color(0.6f, 0.25f, 0.85f) },  // 선명한 보라
            { GameColor.Orange, new Color(1f, 0.55f, 0.1f) },     // 선명한 주황
            { GameColor.Cyan, new Color(0.1f, 0.9f, 0.9f) },      // 선명한 청록
            { GameColor.Pink, new Color(1f, 0.5f, 0.7f) },        // 선명한 분홍
            { GameColor.Brown, new Color(0.6f, 0.35f, 0.15f) },   // 갈색
            { GameColor.Lime, new Color(0.7f, 1f, 0.2f) },        // 라임 (연두)
            { GameColor.Navy, new Color(0.15f, 0.2f, 0.6f) },     // 네이비
            { GameColor.Magenta, new Color(0.95f, 0.1f, 0.9f) },  // 마젠타
            { GameColor.Black, new Color(0.2f, 0.2f, 0.2f) }      // 검정
        };

        /// <summary>
        /// 게임용 색상 (HDR 밝기 2배 적용 - 세련된 느낌)
        /// </summary>
        public static readonly Dictionary<GameColor, Color> Colors = new()
        {
            { GameColor.Red, new Color(0.95f * 2f, 0.2f * 2f, 0.2f * 2f) },
            { GameColor.Blue, new Color(0.2f * 2f, 0.4f * 2f, 0.95f * 2f) },
            { GameColor.Green, new Color(0.2f * 2f, 0.85f * 2f, 0.3f * 2f) },
            { GameColor.Yellow, new Color(1f * 2f, 0.9f * 2f, 0.15f * 2f) },
            { GameColor.Purple, new Color(0.6f * 2f, 0.25f * 2f, 0.85f * 2f) },
            { GameColor.Orange, new Color(1f * 2f, 0.55f * 2f, 0.1f * 2f) },
            { GameColor.Cyan, new Color(0.1f * 2f, 0.9f * 2f, 0.9f * 2f) },
            { GameColor.Pink, new Color(1f * 2f, 0.5f * 2f, 0.7f * 2f) },
            { GameColor.Brown, new Color(0.6f * 1.5f, 0.35f * 1.5f, 0.15f * 1.5f) },  // 갈색은 약하게
            { GameColor.Lime, new Color(0.7f * 2f, 1f * 2f, 0.2f * 2f) },
            { GameColor.Navy, new Color(0.15f * 2f, 0.2f * 2f, 0.6f * 2f) },
            { GameColor.Magenta, new Color(0.95f * 2f, 0.1f * 2f, 0.9f * 2f) },
            { GameColor.Black, new Color(0.2f, 0.2f, 0.2f) }
        };

        public static GameColor FromString(string color)
        {
            return color.ToUpper() switch
            {
                "R" or "RED" => GameColor.Red,
                "B" or "BLUE" => GameColor.Blue,
                "G" or "GREEN" => GameColor.Green,
                "Y" or "YELLOW" => GameColor.Yellow,
                "P" or "PURPLE" => GameColor.Purple,
                "O" or "ORANGE" => GameColor.Orange,
                "C" or "CYAN" => GameColor.Cyan,
                "K" or "PINK" => GameColor.Pink,
                "W" or "BROWN" => GameColor.Brown,
                "L" or "LIME" => GameColor.Lime,
                "N" or "NAVY" => GameColor.Navy,
                "M" or "MAGENTA" => GameColor.Magenta,
                "X" or "BLACK" => GameColor.Black,
                _ => GameColor.Red
            };
        }

        public static Color GetColor(GameColor gameColor)
        {
            return Colors.TryGetValue(gameColor, out var color) ? color : Color.white;
        }

        /// <summary>
        /// 기본 색상 반환 (HDR 미적용)
        /// </summary>
        public static Color GetBaseColor(GameColor gameColor)
        {
            return BaseColors.TryGetValue(gameColor, out var color) ? color : Color.white;
        }

        /// <summary>
        /// HDR 강도를 적용한 색상 반환
        /// </summary>
        public static Color GetColorWithIntensity(GameColor gameColor, float intensity)
        {
            if (BaseColors.TryGetValue(gameColor, out var baseColor))
            {
                return new Color(
                    baseColor.r * intensity,
                    baseColor.g * intensity,
                    baseColor.b * intensity,
                    baseColor.a
                );
            }
            return Color.white;
        }

        public static string ToString(GameColor gameColor)
        {
            return gameColor switch
            {
                GameColor.Red => "R",
                GameColor.Blue => "B",
                GameColor.Green => "G",
                GameColor.Yellow => "Y",
                GameColor.Purple => "P",
                GameColor.Orange => "O",
                GameColor.Cyan => "C",
                GameColor.Pink => "K",
                GameColor.Brown => "W",
                GameColor.Lime => "L",
                GameColor.Navy => "N",
                GameColor.Magenta => "M",
                GameColor.Black => "X",
                _ => "R"
            };
        }
    }
}