using System.Collections.Generic;
using SLG.Units;

namespace SLG.Grid
{
    public sealed class GridReachability
    {
        private readonly GridSystem gridSystem;
        private readonly List<Tile> frontier = new List<Tile>();
        private readonly List<Tile> neighbors = new List<Tile>(4);
        private readonly Dictionary<Tile, int> costSoFar = new Dictionary<Tile, int>();

        public GridReachability(GridSystem gridSystem)
        {
            this.gridSystem = gridSystem;
        }

        public void FindReachableTiles(Tile start, Unit movingUnit, int movementRange, List<Tile> results)
        {
            results.Clear();

            if (start == null || movingUnit == null || movementRange < 0)
            {
                return;
            }

            frontier.Clear();
            costSoFar.Clear();

            frontier.Add(start);
            costSoFar[start] = 0;

            while (frontier.Count > 0)
            {
                Tile current = RemoveLowestCostTile();
                gridSystem.FillNeighbors(current, neighbors);

                for (int i = 0; i < neighbors.Count; i++)
                {
                    Tile neighbor = neighbors[i];
                    if (!neighbor.CanEnter(movingUnit))
                    {
                        continue;
                    }

                    int newCost = costSoFar[current] + neighbor.MovementCost;
                    if (newCost > movementRange)
                    {
                        continue;
                    }

                    if (!costSoFar.TryGetValue(neighbor, out int existingCost) || newCost < existingCost)
                    {
                        costSoFar[neighbor] = newCost;
                        if (!frontier.Contains(neighbor))
                        {
                            frontier.Add(neighbor);
                        }
                    }
                }
            }

            foreach (Tile tile in costSoFar.Keys)
            {
                results.Add(tile);
            }
        }

        private Tile RemoveLowestCostTile()
        {
            int bestIndex = 0;
            int bestCost = costSoFar[frontier[0]];

            for (int i = 1; i < frontier.Count; i++)
            {
                int cost = costSoFar[frontier[i]];
                if (cost < bestCost)
                {
                    bestIndex = i;
                    bestCost = cost;
                }
            }

            Tile tile = frontier[bestIndex];
            frontier.RemoveAt(bestIndex);
            return tile;
        }
    }
}
