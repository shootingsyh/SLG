using System;
using UnityEngine;

namespace SLG.Core
{
    [Serializable]
    public struct GridCoordinate : IEquatable<GridCoordinate>
    {
        [SerializeField] private int x;
        [SerializeField] private int y;

        public GridCoordinate(int x, int y)
        {
            this.x = x;
            this.y = y;
        }

        public int X => x;
        public int Y => y;

        public bool Equals(GridCoordinate other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is GridCoordinate other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y);
        }

        public override string ToString()
        {
            return $"({X},{Y})";
        }
    }
}
