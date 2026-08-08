using UnityEngine;

namespace SLG.Visuals
{
    public static class RuntimeVisualMaterials
    {
        private const string TileMaterialPath = "RuntimeMaterials/Tile";
        private const string UnitMaterialPath = "RuntimeMaterials/Unit";

        private static Material tileMaterial;
        private static Material unitMaterial;

        public static Material TileMaterial => tileMaterial != null ? tileMaterial : tileMaterial = LoadMaterial(TileMaterialPath, "Runtime Tile Material");
        public static Material UnitMaterial => unitMaterial != null ? unitMaterial : unitMaterial = LoadMaterial(UnitMaterialPath, "Runtime Unit Material");

        private static Material LoadMaterial(string resourcePath, string fallbackName)
        {
            Material material = Resources.Load<Material>(resourcePath);
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Standard");
            return shader != null ? new Material(shader) { name = fallbackName } : null;
        }
    }
}
