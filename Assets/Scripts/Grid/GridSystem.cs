using SLG.Core;
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

        private Tile[,] tiles;
        private Tile selectedTile;

        public int Width => width;
        public int Height => height;
        public float TileSize => tileSize;

        private void Start()
        {
            GenerateGrid();
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
                    tile.Initialize(this, coordinate, tileColor, hoverColor, selectedColor);
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
