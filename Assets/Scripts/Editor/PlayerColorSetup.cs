#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 색깔별 goshi 모델에서 머티리얼을 뽑아 PlayerColors 팔레트를 굽고,
/// NetworkPlayer 프리팹에 PlayerColor를 달아 준다.
/// 메뉴: Tools > Player > Setup Player Colors
///
/// 색깔 모델(goshi(red).fbx 등)은 메시·뼈대가 전부 같고 머티리얼 색만 다르다.
/// 그래서 모델을 통째로 갈아끼우는 대신 렌더러 순서대로 머티리얼만 베껴 둔다.
/// 기준은 플레이어 프리팹이 실제로 쓰는 모델(GoshiModel.FindModelPath)이고,
/// 렌더러 개수가 다른 모델은 색이 엉뚱한 곳에 칠해지므로 건너뛴다.
///
/// 모델을 새로 추가하면(goshi(purple).fbx 같은 것) 이 메뉴만 다시 돌리면 된다.
/// </summary>
public static class PlayerColorSetup
{
    private const string PalettePath = "Assets/Player/PlayerColors.asset";
    private const string PlayerPrefabPath = "Assets/Prefabs/NetworkPlayer.prefab";

    /// <summary>파일 이름 안의 영어 색 이름 → 화면에 보여줄 이름.</summary>
    private static readonly Dictionary<string, string> KoreanNames = new Dictionary<string, string>
    {
        { "red", "빨강" },
        { "green", "초록" },
        { "blue", "파랑" },
        { "yellow", "노랑" },
        { "purple", "보라" },
        { "orange", "주황" },
        { "pink", "분홍" },
        { "black", "검정" },
        { "white", "하양" },
    };

    [MenuItem("Tools/Player/Setup Player Colors")]
    public static void SetupPlayerColors()
    {
        string basePath = GoshiModel.FindModelPath();
        if (basePath == null)
        {
            EditorUtility.DisplayDialog("모델 없음", GoshiModel.MissingMessage, "OK");
            return;
        }

        if (!TryReadRenderers(basePath, out Renderer[] baseRenderers))
        {
            EditorUtility.DisplayDialog("모델 없음",
                $"{basePath} 에서 렌더러를 찾지 못했습니다.", "OK");
            return;
        }

        // 프리팹이 쓰는 모델을 0번(기본)으로 두고, 나머지를 뒤에 붙인다.
        var order = new List<string> { basePath };
        order.AddRange(TitleActorSetup.FindModels().Where(p => p != basePath));

        var variants = new List<PlayerColorPalette.Variant>();
        var skipped = new List<string>();

        foreach (string path in order)
        {
            if (!TryReadRenderers(path, out Renderer[] renderers))
            {
                skipped.Add(Path.GetFileName(path) + " (렌더러 없음)");
                continue;
            }

            if (renderers.Length != baseRenderers.Length)
            {
                skipped.Add($"{Path.GetFileName(path)} (렌더러 {renderers.Length}개, 기준 {baseRenderers.Length}개)");
                continue;
            }

            variants.Add(BuildVariant(path, renderers));
        }

        if (variants.Count == 0)
        {
            EditorUtility.DisplayDialog("색 없음", "쓸 수 있는 캐릭터 모델이 없습니다.", "OK");
            return;
        }

        PlayerColorPalette palette = EnsurePalette();
        palette.SetVariants(variants.ToArray());
        EditorUtility.SetDirty(palette);

        bool wired = WireIntoPrefab(palette);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string names = string.Join(", ", variants.Select(v => v.displayName));
        Debug.Log($"[PlayerColor] 색 {variants.Count}종 준비 완료: {names}\n" +
                  $"  팔레트: {PalettePath}\n" +
                  (wired ? $"  {PlayerPrefabPath} 에 PlayerColor를 연결했습니다.\n" : "") +
                  (skipped.Count > 0 ? "  건너뜀: " + string.Join(", ", skipped) : string.Empty),
                  palette);
    }

    // ---------------------------------------------------------------- 팔레트 만들기

    private static bool TryReadRenderers(string modelPath, out Renderer[] renderers)
    {
        renderers = null;

        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (asset == null)
            return false;

        renderers = asset.GetComponentsInChildren<Renderer>(true);
        return renderers.Length > 0;
    }

    private static PlayerColorPalette.Variant BuildVariant(string modelPath, Renderer[] renderers)
    {
        var slots = new PlayerColorPalette.RendererSlot[renderers.Length];
        var all = new List<Material>();

        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] materials = renderers[i].sharedMaterials;
            slots[i] = new PlayerColorPalette.RendererSlot { materials = materials };
            all.AddRange(materials.Where(m => m != null));
        }

        return new PlayerColorPalette.Variant
        {
            displayName = DisplayName(modelPath),
            uiColor = RepresentativeColor(all),
            renderers = slots,
            sourceModel = modelPath,
        };
    }

    /// <summary>goshi(red).fbx → "빨강". 괄호 안을 이름으로 쓰고, 아는 색이면 한국어로 바꾼다.</summary>
    private static string DisplayName(string modelPath)
    {
        string file = Path.GetFileNameWithoutExtension(modelPath);

        int open = file.IndexOf('(');
        int close = file.LastIndexOf(')');
        string inner = open >= 0 && close > open ? file.Substring(open + 1, close - open - 1) : file;

        inner = inner.Trim().Trim('!').Trim();
        if (inner.Length == 0)
            return file;

        if (KoreanNames.TryGetValue(inner.ToLowerInvariant(), out string korean))
            return korean;

        // final / final! 처럼 색 이름이 아니면 '기본'으로 부른다.
        return inner.ToLowerInvariant().Contains("final") ? "기본" : inner;
    }

    /// <summary>
    /// 대기실 이름표 같은 데 쓸 대표 색. 가장 쨍한(채도×명도가 큰) 머티리얼 색을 고른다.
    /// 살색·검정 같은 공용 파트를 피하고 실제로 달라지는 옷 색이 잡히도록 하려는 것.
    /// </summary>
    private static Color RepresentativeColor(List<Material> materials)
    {
        Color best = Color.white;
        float bestScore = -1f;

        foreach (Material material in materials)
        {
            Color color = PipelineShaders.GetBaseColor(material);
            Color.RGBToHSV(color, out _, out float saturation, out float value);

            float score = saturation * value;
            if (score > bestScore)
            {
                bestScore = score;
                best = color;
            }
        }

        best.a = 1f;
        return best;
    }

    private static PlayerColorPalette EnsurePalette()
    {
        var palette = AssetDatabase.LoadAssetAtPath<PlayerColorPalette>(PalettePath);
        if (palette != null)
            return palette;

        palette = ScriptableObject.CreateInstance<PlayerColorPalette>();
        AssetDatabase.CreateAsset(palette, PalettePath);
        return palette;
    }

    // ---------------------------------------------------------------- 프리팹 배선

    /// <summary>
    /// NetworkPlayer 프리팹에 PlayerColor를 달고 팔레트를 물린다.
    /// modelRoot는 일부러 비워 둔다 — 애니메이션 셋업이 RemoteAvatar를 통째로 다시 만들어도
    /// PlayerColor가 이름으로 찾아 스스로 복구하게 하려는 것.
    /// </summary>
    private static bool WireIntoPrefab(PlayerColorPalette palette)
    {
        if (!File.Exists(PlayerPrefabPath))
        {
            Debug.LogWarning($"[PlayerColor] {PlayerPrefabPath} 가 없어 프리팹 배선을 건너뜁니다.");
            return false;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
        try
        {
            var color = root.GetComponent<PlayerColor>();
            if (color == null)
                color = root.AddComponent<PlayerColor>();

            var so = new SerializedObject(color);
            so.FindProperty("palette").objectReferenceValue = palette;
            so.FindProperty("modelRoot").objectReferenceValue = null;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            return true;
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
#endif
