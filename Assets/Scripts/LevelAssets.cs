using UnityEngine;

[CreateAssetMenu(fileName = "LevelAssets", menuName = "Lox/Level Assets")]
public class LevelAssets : ScriptableObject
{
    public Material[] materials;
    public Mesh lensMesh;

    public Material FindMaterial(string materialName)
    {
        if (materials == null || string.IsNullOrEmpty(materialName))
        {
            return null;
        }

        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];

            if (material != null && material.name == materialName)
            {
                return material;
            }
        }

        return null;
    }
}
