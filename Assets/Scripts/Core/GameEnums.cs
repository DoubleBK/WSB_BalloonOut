namespace BalloonOut.Core
{
    /// <summary>
    /// 화살표 이동 방향
    /// </summary>
    public enum Direction
    {
        U,  // Up
        D,  // Down
        L,  // Left
        R   // Right
    }

    /// <summary>
    /// 게임 색상 (화살표, 풍선)
    /// </summary>
    public enum GameColor
    {
        R,  // Red
        G,  // Green
        Y,  // Yellow
        B,  // Blue
        P   // Purple
    }

    /// <summary>
    /// 게임 상태
    /// </summary>
    public enum GameState
    {
        Ready,
        Playing,
        Paused,
        Win,
        Lose
    }

    /// <summary>
    /// 화살표 이동 상태
    /// </summary>
    public enum ArrowState
    {
        Idle,
        Moving,
        Escaping,
        Escaped
    }
}