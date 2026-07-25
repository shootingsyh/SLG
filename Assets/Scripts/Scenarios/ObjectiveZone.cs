using System.Collections.Generic;
using SLG.Core;
using SLG.Grid;
using UnityEngine;

namespace SLG.Scenarios
{
    public sealed class ObjectiveZone : MonoBehaviour
    {
        [SerializeField] private List<GridCoordinate> coordinates = new List<GridCoordinate>();
        [SerializeField] private Color zoneColor = new Color(0.1f, 0.8f, 1f, 0.35f);

        private readonly List<GameObject> visuals = new List<GameObject>();
        private static Material sharedMaterial;

        public IReadOnlyList<GridCoordinate> Coordinates => coordinates;

        public void Configure(IEnumerable<GridCoordinate> zoneCoordinates, GridSystem grid = null, bool createVisuals = true)
        {
            coordinates.Clear();
            if (zoneCoordinates != null)
            {
                coordinates.AddRange(zoneCoordinates);
            }

            ClearVisuals();
            if (createVisuals && grid != null)
            {
                BuildVisuals(grid);
            }
        }

        public bool Contains(GridCoordinate coordinate)
        {
            for (int i = 0; i < coordinates.Count; i++)
            {
                if (coordinates[i].Equals(coordinate))
                {
                    return true;
                }
            }

            return false;
        }

        private void BuildVisuals(GridSystem grid)
        {
            Material material = GetSharedMaterial();
            for (int i = 0; i < coordinates.Count; i++)
            {
                if (!grid.TryGetTile(coordinates[i], out Tile tile))
                {
                    continue;
                }

                GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                marker.name = $"Objective Zone {coordinates[i]}";
                marker.transform.SetParent(transform, false);
                marker.transform.position = tile.transform.position + Vector3.up * 0.08f;
                marker.transform.localScale = new Vector3(grid.TileSize * 0.85f, 0.03f, grid.TileSize * 0.85f);
                Object.Destroy(marker.GetComponent<Collider>());
                Renderer renderer = marker.GetComponent<Renderer>();
                renderer.sharedMaterial = material;
                visuals.Add(marker);
            }
        }

        private static Material GetSharedMaterial()
        {
            if (sharedMaterial == null)
            {
                sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                sharedMaterial.name = "Objective Zone Shared Material";
                sharedMaterial.color = new Color(0.1f, 0.8f, 1f, 0.35f);
            }

            return sharedMaterial;
        }

        private void ClearVisuals()
        {
            for (int i = visuals.Count - 1; i >= 0; i--)
            {
                if (visuals[i] != null)
                {
                    Destroy(visuals[i]);
                }
            }

            visuals.Clear();
        }
    }
}
