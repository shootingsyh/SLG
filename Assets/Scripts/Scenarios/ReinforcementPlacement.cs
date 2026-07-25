using SLG.Core;
using SLG.Grid;
using SLG.Units;

namespace SLG.Scenarios
{
    public static class ReinforcementPlacement
    {
        public static bool TryFindSpawnTile(GridSystem grid, Unit unit, GridCoordinate intended, int radius, out Tile tile)
        {
            tile = null;
            if (grid == null || unit == null || radius < 0)
            {
                return false;
            }

            if (IsValid(grid, unit, intended, out tile))
            {
                return true;
            }

            for (int distance = 1; distance <= radius; distance++)
            {
                for (int yOffset = -distance; yOffset <= distance; yOffset++)
                {
                    int xSpan = distance - System.Math.Abs(yOffset);
                    if (TryCandidate(grid, unit, intended.X - xSpan, intended.Y + yOffset, out tile))
                    {
                        return true;
                    }

                    if (xSpan != 0 && TryCandidate(grid, unit, intended.X + xSpan, intended.Y + yOffset, out tile))
                    {
                        return true;
                    }
                }
            }

            tile = null;
            return false;
        }

        private static bool TryCandidate(GridSystem grid, Unit unit, int x, int y, out Tile tile)
        {
            return IsValid(grid, unit, new GridCoordinate(x, y), out tile);
        }

        private static bool IsValid(GridSystem grid, Unit unit, GridCoordinate coordinate, out Tile tile)
        {
            if (!grid.TryGetTile(coordinate, out tile))
            {
                return false;
            }

            return tile.OccupyingUnit == null && tile.CanEnter(unit);
        }
    }
}
