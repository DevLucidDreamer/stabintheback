#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Assets/Models/weapons/*.blend 에서 뽑아낸 JSON을 실제 Unity 무기 프리팹으로 만든다.
///
/// 이 PC에는 Blender가 설치돼 있지 않아 Unity의 .blend → FBX 변환 파이프라인을 쓸 수 없다.
/// 그래서 Tools/blend_import/blend2json.py 가 .blend 컨테이너를 직접 읽어
/// Assets/Models/weapons/Converted/*.mesh.json 을 만들어 두었고, 이 도구가 그것을 읽는다.
/// (모델을 수정했다면 그 파이썬 스크립트를 다시 돌린 뒤 이 메뉴를 실행하면 된다)
///
/// 메뉴: Tools > Weapons > Build Weapon Prefabs
/// </summary>
public static class WeaponPrefabBuilder
{
    private const string JsonFolder = "Assets/Models/weapons/Converted";
    private const string MeshFolder = "Assets/Models/weapons/Generated";
    private const string PrefabFolder = "Assets/Prefabs/Weapons";

    // ---------------------------------------------------------------- 무기 정의표

    /// <summary>
    /// 모델은 전부 "손잡이가 -X 쪽, 원점 바닥에 놓인" 형태로 만들어져 있고 크기도 제각각이라,
    /// 여기서 실제 게임 크기 / 손에 쥐는 지점 / 드는 자세를 정해 준다.
    /// grip은 모델 바운딩박스 안의 정규화 좌표(0~1)로, 손이 닿는 지점이다.
    /// </summary>
    private readonly struct Def
    {
        public readonly string File;      // Converted/{File}.mesh.json
        public readonly string Display;   // 화면에 보이는 이름
        public readonly float Length;     // 가장 긴 축의 실제 길이(m)
        public readonly Vector3 Grip;     // 바운딩박스 정규화 좌표(0~1)
        public readonly Vector3 HoldEuler;// 손 소켓 기준 회전
        public readonly float Reach;      // 휘둘렀을 때 닿는 앞쪽 거리(m)
        public readonly float Radius;     // 타격 판정 반경(m)

        public Def(string file, string display, float length, Vector3 grip, Vector3 holdEuler, float reach, float radius)
        {
            File = file; Display = display; Length = length;
            Grip = grip; HoldEuler = holdEuler; Reach = reach; Radius = radius;
        }
    }

    private static readonly Def[] Defs =
    {
        new Def("Sausage_Skewer",        "소세지 꼬치",     0.95f, new Vector3(0.02f, 0.5f,  0.5f), new Vector3(-10f, -90f, 0f), 1.5f, 0.85f),
        new Def("Great_Ladle",           "거대 국자",       1.10f, new Vector3(0.03f, 0.5f,  0.5f), new Vector3(-15f, -90f, 0f), 1.6f, 1.00f),
        new Def("Frozen_Tuna",           "냉동 참치",       1.05f, new Vector3(0.95f, 0.5f,  0.5f), new Vector3(-10f,  90f, 0f), 1.5f, 1.05f),
        new Def("Carrot_Greatsword",     "당근 대검",       1.30f, new Vector3(0.05f, 0.5f,  0.5f), new Vector3(-15f, -90f, 0f), 1.9f, 0.95f),
        new Def("Baguette_Club",         "바게트 몽둥이",   0.85f, new Vector3(0.05f, 0.5f,  0.5f), new Vector3(-12f, -90f, 0f), 1.4f, 0.85f),
        new Def("Whisk_Axe",             "거품기 도끼",     0.90f, new Vector3(0.03f, 0.5f,  0.5f), new Vector3(-12f, -90f, 0f), 1.5f, 0.95f),
        new Def("Pineapple_MorningStar", "파인애플 철퇴",   0.85f, new Vector3(0.5f,  0.95f, 0.5f), new Vector3(-75f,   0f, 0f), 1.4f, 1.05f),
        new Def("Banana_Bow",            "바나나 활",       1.10f, new Vector3(0.08f, 0.5f,  0.35f), new Vector3(-5f, -90f, 0f), 1.3f, 0.80f),
        new Def("Bread_Shield",          "식빵 방패",       0.62f, new Vector3(0.5f,  0.5f,  0.5f), new Vector3( 90f,   0f, 0f), 1.1f, 0.90f),
        new Def("Rubber_Duck",           "고무 오리",       0.38f, new Vector3(0.35f, 0.3f,  0.5f), new Vector3(  0f, -90f, 0f), 1.0f, 0.70f),
    };

    /// <summary>캠핑장에 놓을 무기 이름(정의표의 File). Stage2Builder가 참조한다.</summary>
    public static readonly string[] CampgroundWeapons =
    {
        "Sausage_Skewer", "Great_Ladle", "Frozen_Tuna", "Whisk_Axe",
        "Carrot_Greatsword", "Baguette_Club", "Pineapple_MorningStar", "Rubber_Duck",
    };

    public static string PrefabPath(string file) => $"{PrefabFolder}/{file}.prefab";

    // ---------------------------------------------------------------- 메뉴

    [MenuItem("Tools/Weapons/Build Weapon Prefabs")]
    public static void BuildAll()
    {
        if (!Directory.Exists(JsonFolder))
        {
            EditorUtility.DisplayDialog("변환 결과 없음",
                $"{JsonFolder} 가 없습니다.\n\nTools/blend_import/blend2json.py 를 먼저 실행하세요.", "OK");
            return;
        }

        EnsureFolder(MeshFolder);
        EnsureFolder(PrefabFolder);

        var built = new List<string>();
        foreach (Def def in Defs)
        {
            string path = $"{JsonFolder}/{def.File}.mesh.json";
            var text = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (text == null)
            {
                Debug.LogWarning($"[Weapons] JSON 없음, 건너뜀: {path}");
                continue;
            }

            if (BuildOne(def, text.text))
                built.Add(def.Display);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[Weapons] 무기 프리팹 {built.Count}개 생성 → {PrefabFolder}\n  " + string.Join(", ", built));
    }

    // ---------------------------------------------------------------- 한 자루 만들기

    private static bool BuildOne(Def def, string json)
    {
        BlendDoc doc = JsonUtility.FromJson<BlendDoc>(json);
        if (doc == null || doc.parts == null || doc.parts.Count == 0)
        {
            Debug.LogWarning($"[Weapons] 파트가 없습니다: {def.File}");
            return false;
        }

        // 모델 전체 바운즈에서 스케일과 손잡이 위치를 계산한다.
        Bounds raw = RawBounds(doc);
        float longest = Mathf.Max(raw.size.x, Mathf.Max(raw.size.y, raw.size.z));
        float scale = longest > 1e-4f ? def.Length / longest : 1f;

        Vector3 gripLocal = new Vector3(
            Mathf.Lerp(raw.min.x, raw.max.x, def.Grip.x),
            Mathf.Lerp(raw.min.y, raw.max.y, def.Grip.y),
            Mathf.Lerp(raw.min.z, raw.max.z, def.Grip.z));

        var root = new GameObject(def.File);
        try
        {
            var weapon = root.AddComponent<Weapon>();
            weapon.SetDisplayName(def.Display);
            weapon.holdPosition = Vector3.zero;
            weapon.holdEuler = def.HoldEuler;
            weapon.swingReach = def.Reach;
            weapon.swingRadius = def.Radius;

            Bounds worldBounds = new Bounds(Vector3.zero, Vector3.zero);
            bool first = true;

            for (int i = 0; i < doc.parts.Count; i++)
            {
                BlendPart part = doc.parts[i];
                Mesh mesh = BuildMesh(part, def.File, i, scale, gripLocal);
                if (mesh == null)
                    continue;

                var go = new GameObject(string.IsNullOrEmpty(part.name) ? "Model" : part.name);
                go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = BuildMaterials(part);

                if (first) { worldBounds = mesh.bounds; first = false; }
                else worldBounds.Encapsulate(mesh.bounds);
            }

            if (first)
            {
                Debug.LogWarning($"[Weapons] 메시를 만들지 못했습니다: {def.File}");
                return false;
            }

            // 줍기 판정용 콜라이더. 얇은 무기도 조준하기 쉽도록 최소 두께를 준다.
            var box = root.AddComponent<BoxCollider>();
            box.center = worldBounds.center;
            box.size = new Vector3(
                Mathf.Max(worldBounds.size.x, 0.12f),
                Mathf.Max(worldBounds.size.y, 0.12f),
                Mathf.Max(worldBounds.size.z, 0.12f));

            // 원점이 손잡이라 모델이 원점보다 아래로 내려간다. 바닥에 놓을 때 묻히지 않게 알려 준다.
            weapon.groundOffset = Mathf.Max(0f, -worldBounds.min.y);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath(def.File));
            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    /// <summary>메시를 실제 게임 크기로 줄이고, 손잡이 지점이 원점에 오도록 옮긴다.</summary>
    private static Mesh BuildMesh(BlendPart part, string file, int index, float scale, Vector3 gripLocal)
    {
        int vertexCount = part.vertices.Length / 3;
        if (vertexCount == 0)
            return null;

        var verts = new Vector3[vertexCount];
        for (int i = 0; i < vertexCount; i++)
        {
            verts[i] = new Vector3(
                (part.vertices[i * 3] - gripLocal.x) * scale,
                (part.vertices[i * 3 + 1] - gripLocal.y) * scale,
                (part.vertices[i * 3 + 2] - gripLocal.z) * scale);
        }

        var mesh = new Mesh { name = index == 0 ? file : $"{file}_{index}" };
        mesh.SetVertices(verts);

        if (part.normals != null && part.normals.Length == part.vertices.Length)
        {
            var normals = new Vector3[vertexCount];
            for (int i = 0; i < vertexCount; i++)
                normals[i] = new Vector3(part.normals[i * 3], part.normals[i * 3 + 1], part.normals[i * 3 + 2]);
            mesh.SetNormals(normals);
        }

        if (part.uvs != null && part.uvs.Length == vertexCount * 2)
        {
            var uvs = new Vector2[vertexCount];
            for (int i = 0; i < vertexCount; i++)
                uvs[i] = new Vector2(part.uvs[i * 2], part.uvs[i * 2 + 1]);
            mesh.SetUVs(0, uvs);
        }

        mesh.subMeshCount = part.submeshes.Count;
        for (int s = 0; s < part.submeshes.Count; s++)
            mesh.SetTriangles(part.submeshes[s].triangles ?? Array.Empty<int>(), s);

        if (part.normals == null || part.normals.Length != part.vertices.Length)
            mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        mesh.RecalculateTangents();

        string path = $"{MeshFolder}/{mesh.name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing != null)
        {
            // 이미 프리팹이 참조하고 있으므로 링크가 끊기지 않게 내용만 갈아끼운다.
            existing.Clear();
            EditorUtility.CopySerialized(mesh, existing);
            UnityEngine.Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        AssetDatabase.CreateAsset(mesh, path);
        return mesh;
    }

    private static Material[] BuildMaterials(BlendPart part)
    {
        var mats = new Material[part.materials.Count];
        for (int i = 0; i < part.materials.Count; i++)
        {
            BlendMat m = part.materials[i];
            Color color = m.color != null && m.color.Length >= 3
                ? new Color(m.color[0], m.color[1], m.color[2], m.color.Length > 3 ? m.color[3] : 1f)
                : Color.gray;

            Material mat = BuilderMaterials.Ensure(m.name, color);
            if (mat.HasProperty("_Metallic"))
                mat.SetFloat("_Metallic", m.metallic);
            if (mat.HasProperty("_Smoothness"))
                mat.SetFloat("_Smoothness", m.smoothness);
            else if (mat.HasProperty("_Glossiness"))
                mat.SetFloat("_Glossiness", m.smoothness);
            EditorUtility.SetDirty(mat);
            mats[i] = mat;
        }
        return mats;
    }

    private static Bounds RawBounds(BlendDoc doc)
    {
        var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
        var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);

        foreach (BlendPart part in doc.parts)
        {
            for (int i = 0; i + 2 < part.vertices.Length; i += 3)
            {
                min.x = Mathf.Min(min.x, part.vertices[i]);
                min.y = Mathf.Min(min.y, part.vertices[i + 1]);
                min.z = Mathf.Min(min.z, part.vertices[i + 2]);
                max.x = Mathf.Max(max.x, part.vertices[i]);
                max.y = Mathf.Max(max.y, part.vertices[i + 1]);
                max.z = Mathf.Max(max.z, part.vertices[i + 2]);
            }
        }

        var b = new Bounds();
        b.SetMinMax(min, max);
        return b;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
            return;

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    // ---------------------------------------------------------------- JSON 스키마

    [Serializable] private class BlendDoc { public string name; public List<BlendPart> parts; }

    [Serializable]
    private class BlendPart
    {
        public string name;
        public float[] vertices;
        public float[] normals;
        public float[] uvs;
        public List<BlendSub> submeshes;
        public List<BlendMat> materials;
    }

    [Serializable] private class BlendSub { public int[] triangles; }

    [Serializable]
    private class BlendMat
    {
        public string name;
        public float[] color;
        public float metallic;
        public float smoothness;
    }
}
#endif
