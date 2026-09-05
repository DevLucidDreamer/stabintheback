#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>CC0 scan materials and physically sized box UVs; no stretched wall textures.</summary>
public static class TempleSurfaceBuilder
{
    private static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
    private const string MeshFolder = "Assets/Models/Generated/TempleSurfaces";
    public static Material Surface(string name, Color tint, bool paving)
    {
        string asset = paving ? "PavingStones136" : "Bricks089";
        Material mat = BuilderMaterials.Ensure(name, tint);
        mat.DisableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", Color.black);
        mat.SetTexture("_BaseMap", Texture(asset, "Color", false, true));
        mat.SetTexture("_BumpMap", Texture(asset, "NormalGL", true, false));
        mat.SetTexture("_OcclusionMap", Texture(asset, "AmbientOcclusion", false, false));
        mat.EnableKeyword("_NORMALMAP"); mat.EnableKeyword("_OCCLUSIONMAP");
        mat.SetFloat("_BumpScale", 0.75f); mat.SetFloat("_OcclusionStrength", 0.5f);
        mat.SetFloat("_Smoothness", 0.12f); mat.SetFloat("_Metallic", 0f);
        // Brick scan covers 2.2 x 1.1 metres; paving uses a 2m repeat.
        mat.SetTextureScale("_BaseMap", paving ? Vector2.one * 0.5f : new Vector2(1f / 2.2f, 1f / 1.1f));
        EditorUtility.SetDirty(mat);
        return mat;
    }
    private static Texture2D Texture(string asset, string map, bool normal, bool srgb)
    {
        string path = "Assets/Textures/Temple/" + asset + "_" + map + ".jpg";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new System.IO.FileNotFoundException(path);
        var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        if (importer.textureType != type || importer.sRGBTexture != srgb || importer.anisoLevel != 8 || importer.wrapMode != TextureWrapMode.Repeat)
        {
            importer.textureType = type; importer.sRGBTexture = srgb; importer.anisoLevel = 8;
            importer.wrapMode = TextureWrapMode.Repeat; importer.maxTextureSize = 2048;
            importer.mipmapEnabled = true; importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    public static void ApplyBoxUV(GameObject go)
    {
        if (!AssetDatabase.IsValidFolder(MeshFolder)) AssetDatabase.CreateFolder("Assets/Models/Generated", "TempleSurfaces");
        Vector3 size = go.transform.lossyScale;
        string key = $"Box_{Mathf.RoundToInt(size.x * 1000)}_{Mathf.RoundToInt(size.y * 1000)}_{Mathf.RoundToInt(size.z * 1000)}";
        if (!meshes.TryGetValue(key, out Mesh mesh) || mesh == null)
        {
            string path = MeshFolder + "/" + key + ".asset";
            mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = Object.Instantiate(go.GetComponent<MeshFilter>().sharedMesh); mesh.name = key;
                var vertices = mesh.vertices; var normals = mesh.normals; var uv = new Vector2[vertices.Length];
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 p = Vector3.Scale(vertices[i], size);
                    Vector3 n = normals[i];
                    uv[i] = Mathf.Abs(n.y) > 0.5f ? new Vector2(p.x, p.z * Mathf.Sign(n.y)) :
                        Mathf.Abs(n.x) > 0.5f ? new Vector2(p.z * -Mathf.Sign(n.x), p.y) : new Vector2(p.x * Mathf.Sign(n.z), p.y);
                }
                mesh.uv = uv; mesh.RecalculateTangents();
                AssetDatabase.CreateAsset(mesh, path);
            }
            meshes[key] = mesh;
        }
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
    }
}
#endif
