using System;
using UnityEngine;

namespace BalloonOut.Data
{
    /// <summary>
    /// JSON 직렬화 가능한 Vector2Int
    /// </summary>
    [Serializable]
    public class Vector2IntSerializable
    {
        public int x;
        public int y;

        public Vector2IntSerializable() { }

        public Vector2IntSerializable(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public Vector2IntSerializable(Vector2Int v)
        {
            this.x = v.x;
            this.y = v.y;
        }

        public Vector2Int ToVector2Int()
        {
            return new Vector2Int(x, y);
        }

        public static implicit operator Vector2Int(Vector2IntSerializable v)
        {
            return new Vector2Int(v.x, v.y);
        }

        public static implicit operator Vector2IntSerializable(Vector2Int v)
        {
            return new Vector2IntSerializable(v.x, v.y);
        }
    }
}