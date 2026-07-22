using System;

namespace SLG.Core
{
    [Serializable]
    public readonly struct GridCoordinate : IEquatable<GridCoordinate>
    {
        public GridCoordinate(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }
        public int Y { get; }

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
