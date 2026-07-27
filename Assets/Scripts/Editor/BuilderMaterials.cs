#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 맵 빌더들이 공용으로 쓰는 머티리얼 생성/보정.
/// 새로 만들 때뿐 아니라 **이미 있는 머티리얼의 셰이더가 잘못돼 있으면 고쳐서** 돌려준다
/// (URP에서 내장 Standard 셰이더는 분홍으로 렌더된다).
///
/// 메뉴: Tools > Materials > Fix Pink Materials — 맵을 다시 만들지 않고 전부 점검/수리한다.
/// </summary>
public static class BuilderMaterials
{
    public const string Folder = "Assets/Materials";

    /// <summary>Assets/Materials/{assetName}.mat 를 보장한다. 색과 셰이더를 항상 맞춰 준다.</summary>
    public static Material Ensure(string assetName, Color color)
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets", "Materials");

        string path = $"{Folder}/{assetName}.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material == null)
        {
            material = PipelineShaders.CreateLit(color);
            AssetDatabase.CreateAsset(material, path);
        }
        else if (PipelineShaders.IsBroken(material))
        {
            material.shader = PipelineShaders.Lit;
            Debug.Log($"[Materials] 셰이더가 잘못돼 있어 교체했습니다: {path}");
        }

        PipelineShaders.SetBaseColor(material, color);
        EditorUtility.SetDirty(material);
        return material;
    }

    [MenuItem("Tools/Materials/Fix Pink Materials")]
    public static void FixPinkMaterials()
    {
        Shader lit = PipelineShaders.Lit;
        if (lit == null)
        {
            EditorUtility.DisplayDialog("셰이더 없음", "현재 렌더 파이프라인의 기본 셰이더를 찾지 못했습니다.", "OK");
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
        int fixedCount = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.StartsWith("Assets/Mirror")) // 외부에서 가져온 애셋은 건드리지 않는다
                continue;

            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!PipelineShaders.IsBroken(material))
                continue;

            // 셰이더를 바꾸면 색 프로퍼티 이름도 바뀌므로(_Color → _BaseColor) 미리 읽어 둔다.
            Color color = PipelineShaders.GetBaseColor(material);
            material.shader = lit;
            PipelineShaders.SetBaseColor(material, color);
            EditorUtility.SetDirty(material);

            fixedCount++;
            Debug.Log($"[Materials] 셰이더 교체 → {lit.name} : {path}");
        }

        AssetDatabase.SaveAssets();
        Debug.Log(fixedCount == 0
            ? "[Materials] 분홍으로 보일 머티리얼이 없습니다."
            : $"[Materials] {fixedCount}개 수리 완료.");
    }
}
#endif
