using System;
using System.Collections.Generic;
using UnityEngine;
using BalloonOut.Data;

namespace BalloonOut.Core
{
    /// <summary>
    /// 게임 상태 스냅샷 - Undo 기능용
    /// </summary>
    [Serializable]
    public class GameStateSnapshot
    {
        /// <summary>탈출한 화살표 정보</summary>
        public ArrowSnapshot EscapedArrow;

        /// <summary>터뜨린 풍선 정보 (매칭된 경우)</summary>
        public BalloonSnapshot PoppedBalloon;

        /// <summary>스냅샷 생성 시간</summary>
        public float Timestamp;

        public GameStateSnapshot()
        {
            Timestamp = Time.time;
        }

        /// <summary>
        /// 화살표 탈출 스냅샷 생성
        /// </summary>
        public static GameStateSnapshot CreateFromEscape(
            ArrowSnapshot arrow,
            BalloonSnapshot poppedBalloon = null)
        {
            return new GameStateSnapshot
            {
                EscapedArrow = arrow,
                PoppedBalloon = poppedBalloon,
                Timestamp = Time.time
            };
        }
    }

    /// <summary>
    /// 화살표 상태 스냅샷
    /// </summary>
    [Serializable]
    public class ArrowSnapshot
    {
        /// <summary>화살표 고유 ID</summary>
        public int ArrowId;

        /// <summary>색상</summary>
        public GameColor Color;

        /// <summary>차지하는 셀들 (Tail → Head 순서)</summary>
        public List<Vector2Int> OccupiedCells;

        /// <summary>Head 위치</summary>
        public Vector2Int HeadPosition;

        /// <summary>탈출 방향</summary>
        public ArrowDirection HeadDirection;

        /// <summary>셀 경로 (꺾이는 화살표용)</summary>
        public List<Vector2Int> Path;

        /// <summary>길이</summary>
        public int Length;

        /// <summary>Filler 여부</summary>
        public bool IsFiller;

        /// <summary>Decoy 여부</summary>
        public bool IsDecoy;

        /// <summary>
        /// ArrowController에서 스냅샷 생성
        /// </summary>
        public static ArrowSnapshot CreateFromController(Game.Arrow.ArrowController arrow)
        {
            if (arrow == null) return null;

            var occupiedPositions = arrow.GetOccupiedPositions();

            return new ArrowSnapshot
            {
                ArrowId = arrow.Id,
                Color = arrow.Color,
                OccupiedCells = new List<Vector2Int>(occupiedPositions),
                HeadPosition = arrow.HeadPosition,
                HeadDirection = arrow.HeadDirection,
                Length = arrow.TotalLength
            };
        }

        /// <summary>
        /// ArrowData로 변환 (화살표 재생성용)
        /// </summary>
        public ArrowData ToArrowData()
        {
            var arrowData = new ArrowData
            {
                x = HeadPosition.x,
                y = HeadPosition.y,
                color = ColorHelper.ToString(Color),
                direction = DirectionHelper.ToString(HeadDirection),
                length = Length,
                isFiller = IsFiller,
                isDecoy = IsDecoy
            };

            // 셀이 여러 개면 path 설정
            if (OccupiedCells != null && OccupiedCells.Count > 1)
            {
                arrowData.path = new List<Vector2IntSerializable>();
                foreach (var cell in OccupiedCells)
                {
                    arrowData.path.Add(new Vector2IntSerializable(cell.x, cell.y));
                }
            }

            return arrowData;
        }
    }

    /// <summary>
    /// 풍선 상태 스냅샷
    /// </summary>
    [Serializable]
    public class BalloonSnapshot
    {
        /// <summary>색상</summary>
        public GameColor Color;

        /// <summary>레인 인덱스</summary>
        public int LaneIndex;

        /// <summary>레인 내 위치 (0 = 가장 앞/활성)</summary>
        public int PositionInLane;

        public BalloonSnapshot() { }

        public BalloonSnapshot(GameColor color, int laneIndex, int positionInLane = 0)
        {
            Color = color;
            LaneIndex = laneIndex;
            PositionInLane = positionInLane;
        }
    }
}
