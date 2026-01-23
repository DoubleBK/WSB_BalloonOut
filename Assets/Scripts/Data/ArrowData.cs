using System;
using System.Collections.Generic;
using UnityEngine;
using BalloonOut.Core;

namespace BalloonOut.Data
{
    /// <summary>
    /// 화살표 데이터 (JSON 직렬화용)
    /// </summary>
    [Serializable]
    public class ArrowData
    {
        /// <summary>Head X 좌표</summary>
        public int x;

        /// <summary>Head Y 좌표</summary>
        public int y;

        /// <summary>색상 (R/G/Y/B/P)</summary>
        public string color;

        /// <summary>탈출 방향 (U/D/L/R)</summary>
        public string direction;

        /// <summary>길이</summary>
        public int length;

        /// <summary>셀 경로 (꺾이는 화살표용)</summary>
        public List<Vector2IntSerializable> path;

        /// <summary>탈출 순서</summary>
        public int order;

        /// <summary>Filler 여부</summary>
        public bool isFiller;

        /// <summary>
        /// Head 위치 반환
        /// </summary>
        public Vector2Int HeadPosition => new Vector2Int(x, y);

        /// <summary>
        /// Direction enum 반환
        /// </summary>
        public Direction Direction => DirectionHelper.FromString(direction);

        /// <summary>
        /// GameColor enum 반환
        /// </summary>
        public GameColor Color => ColorHelper.FromString(color);

        /// <summary>
        /// 셀 경로 반환 (path가 없으면 직선으로 계산)
        /// </summary>
        public List<Vector2Int> GetCells()
        {
            var cells = new List<Vector2Int>();

            if (path != null && path.Count > 0)
            {
                // 꺾이는 화살표: path 사용
                foreach (var p in path)
                {
                    cells.Add(p.ToVector2Int());
                }
            }
            else
            {
                // 직선 화살표: Head에서 반대 방향으로 계산
                var dir = DirectionHelper.Vectors[Direction];
                for (int i = 0; i < length; i++)
                {
                    cells.Add(new Vector2Int(x - dir.x * i, y - dir.y * i));
                }
            }

            return cells;
        }
    }
}