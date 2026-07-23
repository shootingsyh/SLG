using System.Collections.Generic;
using SLG.Core;
using SLG.Units;

namespace SLG.Grid
{
    public sealed class GridPathfinder
    {
        private readonly GridSystem gridSystem;
        private readonly List<Tile> openSet = new List<Tile>();
        private readonly HashSet<Tile> closedSet = new HashSet<Tile>();
        private readonly Dictionary<Tile, Tile> cameFrom = new Dictionary<Tile, Tile>();
        private readonly Dictionary<Tile, int> gScore = new Dictionary<Tile, int>();

        public GridPathfinder(GridSystem gridSystem)
        {
            this.gridSystem = gridSystem;
        }

        public static int GetManhattanDistance(GridCoordinate a, GridCoordinate b)
        {
            return System.Math.Abs(a.X - b.X) + System.Math.Abs(a.Y - b.Y);
        }

        public bool TryFindPath(Tile start, Tile destination, Unit movingUnit, List<Tile> path)
        {
            path.Clear();

            if (start == null || destination == null || !destination.CanEnter(movingUnit))
            {
                return false;
            }

            ResetSearch();
            openSet.Add(start);
            gScore[start] = 0;

            while (openSet.Count > 0)
            {
                Tile current = GetLowestScoreTile(destination);
                if (current == destination)
                {
                    BuildPath(destination, path);
                    return true;
                }

                openSet.Remove(current);
                closedSet.Add(current);

                IReadOnlyList<Tile> neighbors = gridSystem.GetNeighbors(current);
                for (int i = 0; i < neighbors.Count; i++)
                {
                    Tile neighbor = neighbors[i];
                    if (closedSet.Contains(neighbor) || !neighbor.CanEnter(movingUnit))
                    {
                        continue;
                    }

                    int tentativeScore = gScore[current] + neighbor.MovementCost;
                    if (!gScore.TryGetValue(neighbor, out int existingScore) || tentativeScore < existingScore)
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeScore;

                        if (!openSet.Contains(neighbor))
                        {
                            openSet.Add(neighbor);
                        }
                    }
                }
            }

            return false;
        }

        private void ResetSearch()
        {
            openSet.Clear();
            closedSet.Clear();
            cameFrom.Clear();
            gScore.Clear();
        }

        private Tile GetLowestScoreTile(Tile destination)
        {
            Tile bestTile = openSet[0];
            int bestScore = GetEstimatedTotalCost(bestTile, destination);

            for (int i = 1; i < openSet.Count; i++)
            {
                Tile tile = openSet[i];
                int score = GetEstimatedTotalCost(tile, destination);
                if (score < bestScore)
                {
                    bestTile = tile;
                    bestScore = score;
                }
            }

            return bestTile;
        }

        private int GetEstimatedTotalCost(Tile tile, Tile destination)
        {
            return gScore[tile] + GetManhattanDistance(tile.Coordinate, destination.Coordinate);
        }

        private void BuildPath(Tile destination, List<Tile> path)
        {
            Tile current = destination;
            path.Add(current);

            while (cameFrom.TryGetValue(current, out Tile previous))
            {
                current = previous;
                path.Add(current);
            }

            path.Reverse();
        }
    }
}
