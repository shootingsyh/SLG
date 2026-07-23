using System.Collections.Generic;
using SLG.Core;
using SLG.Units;
using UnityEngine;

namespace SLG.Grid
{
    public sealed class GridSystem : MonoBehaviour
    {
        [Header("Grid")]
        [SerializeField] private int width = 8;
        [SerializeField] private int height = 8;
        [SerializeField] private float tileSize = 1f;
        [SerializeField] private Tile tilePrefab;

        [Header("Tile Colors")]
        [SerializeField] private Color tileColor = new Color(0.24f, 0.29f, 0.34f, 1f);
        [SerializeField] private Color hoverColor = new Color(0.43f, 0.65f, 0.9f, 1f);
        [SerializeField] private Color selectedColor = new Color(1f, 0.82f, 0.25f, 1f);
        [SerializeField] private Color movementRangeColor = new Color(0.22f, 0.55f, 0.32f, 1f);

        [Header("Selection")]
        [SerializeField] private UnitSelectionController unitSelectionController;

        private Tile[,] tiles;
        private Tile selectedTile;
        private readonly List<Tile> neighborBuffer = new List<Tile>(4);
        private GridPathfinder pathfinder;
        private GridReachability reachability;

        public int Width => width;
        public int Height => height;
        public float TileSize => tileSize;
        public GridPathfinder Pathfinder => pathfinder;
        public GridReachability Reachability => reachability;

        public Vector3 GetTileWorldPosition(GridCoordinate coordinate)
        {
            Vector3 originOffset = new Vector3((width - 1) * tileSize * 0.5f, 0f, (height - 1) * tileSize * 0.5f);
            return new Vector3(coordinate.X * tileSize, 0f, coordinate.Y * tileSize) - originOffset + transform.position;
        }

        private void Start()
        {
            GenerateGrid();
            pathfinder = new GridPathfinder(this);
            reachability = new GridReachability(this);
            unitSelectionController?.InitializeUnitsOnGrid();
        }

        public void HandleTileClicked(Tile tile)
        {
            if (unitSelectionController != null && unitSelectionController.HandleTileClicked(tile))
            {
                return;
            }

            unitSelectionController?.DeselectCurrentUnit();
            SelectTile(tile);
        }

        public IReadOnlyList<Tile> GetNeighbors(Tile tile)
        {
            FillNeighbors(tile, neighborBuffer);
            return neighborBuffer;
        }

        public void FillNeighbors(Tile tile, List<Tile> results)
        {
            results.Clear();

            if (tile == null)
            {
                return;
            }

            AddNeighbor(tile.X + 1, tile.Y, results);
            AddNeighbor(tile.X - 1, tile.Y, results);
            AddNeighbor(tile.X, tile.Y + 1, results);
            AddNeighbor(tile.X, tile.Y - 1, results);
        }

        public void SelectTile(Tile tile)
        {
            if (tile == null || tile == selectedTile)
            {
                return;
            }

            if (selectedTile != null)
            {
                selectedTile.SetSelected(false);
            }

            selectedTile = tile;
            selectedTile.SetSelected(true);
            Debug.Log($"Selected Tile: ({selectedTile.X},{selectedTile.Y})");
        }

        public void ClearSelectedTile()
        {
            if (selectedTile == null)
            {
                return;
            }

            selectedTile.SetSelected(false);
            selectedTile = null;
        }

        public bool TryGetTile(GridCoordinate coordinate, out Tile tile)
        {
            if (tiles == null || !IsValidCoordinate(coordinate))
            {
                tile = null;
                return false;
            }

            tile = tiles[coordinate.X, coordinate.Y];
            return tile != null;
        }

        public bool IsValidCoordinate(GridCoordinate coordinate)
        {
            return coordinate.X >= 0 && coordinate.X < width && coordinate.Y >= 0 && coordinate.Y < height;
        }

        private void AddNeighbor(int x, int y, List<Tile> results)
        {
            if (TryGetTile(new GridCoordinate(x, y), out Tile tile))
            {
                results.Add(tile);
            }
        }

        private void GenerateGrid()
        {
            if (tilePrefab == null)
            {
                Debug.LogError("GridSystem cannot generate a grid without a Tile prefab.", this);
                return;
            }

            ClearGeneratedTiles();
            tiles = new Tile[width, height];

            Vector3 originOffset = new Vector3((width - 1) * tileSize * 0.5f, 0f, (height - 1) * tileSize * 0.5f);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    GridCoordinate coordinate = new GridCoordinate(x, y);
                    Vector3 position = new Vector3(x * tileSize, 0f, y * tileSize) - originOffset;
                    Tile tile = Instantiate(tilePrefab, position, Quaternion.identity, transform);
                    tile.name = $"Tile {coordinate}";
                    tile.transform.localScale = new Vector3(tileSize, 0.1f, tileSize);
                    tile.Initialize(this, coordinate, tileColor, hoverColor, selectedColor, movementRangeColor);
                    tiles[x, y] = tile;
                }
            }
        }

        private void ClearGeneratedTiles()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                Destroy(transform.GetChild(i).gameObject);
            }
        }
    }
}
