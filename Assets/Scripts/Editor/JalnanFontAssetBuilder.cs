#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// Jalnan2 TTF로 TMP 폰트 에셋을 만든다. 메뉴: Tools > Fonts > Create Jalnan2 TMP Font Asset
///
/// Atlas Population Mode는 Dynamic — 한글 완성형 11172자를 전부 정적 아틀라스로 구우면
/// 1024 텍스처가 10장 넘게 나오므로, 실제로 쓰인 글자만 런타임에 채우게 둔다.
/// (ASCII만 미리 구워둬서 영문/숫자는 첫 프레임부터 바로 나온다.)
/// 텍스처와 머티리얼은 .asset 안에 서브에셋으로 들어간다.
/// </summary>
public static class JalnanFontAssetBuilder
{
    private const string SourceFontPath = "Assets/Fonts/Jalnan2/Jalnan2TTF.ttf";
    private const string OutputPath = "Assets/Fonts/Jalnan2/Jalnan2 SDF.asset";

    private const int SamplingPointSize = 90;
    private const int AtlasPadding = 9;
    private const int AtlasWidth = 1024;
    private const int AtlasHeight = 1024;

    /// <summary>
    /// Jalnan2 SDF를 보장한다. 없으면 만들고, 있으면 그대로 돌려준다.
    /// 다른 빌더(타이틀 화면 등)가 폰트를 필요로 할 때 부른다.
    /// </summary>
    public static TMP_FontAsset Ensure()
    {
        var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputPath);
        if (existing != null)
            return existing;

        Create();
        return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputPath);
    }

    [MenuItem("Tools/Fonts/Create Jalnan2 TMP Font Asset")]
    public static void CreateJalnan2FontAsset()
    {
        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputPath) != null &&
            !EditorUtility.DisplayDialog("Jalnan2 SDF",
                $"{OutputPath} 가 이미 있다. 덮어쓸까?", "덮어쓰기", "취소"))
        {
            return;
        }

        Create();
    }

    private static void Create()
    {
        var font = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
        if (font == null)
        {
            Debug.LogError($"[JalnanFont] 원본 폰트를 찾을 수 없다: {SourceFontPath}");
            return;
        }

        var fontAsset = TMP_FontAsset.CreateFontAsset(
            font,
            SamplingPointSize,
            AtlasPadding,
            GlyphRenderMode.SDFAA,
            AtlasWidth,
            AtlasHeight,
            AtlasPopulationMode.Dynamic,
            enableMultiAtlasSupport: true);

        if (fontAsset == null)
        {
            Debug.LogError("[JalnanFont] 폰트 에셋 생성 실패. 폰트 임포터의 Include Font Data가 켜져 있는지 확인.");
            return;
        }

        fontAsset.name = "Jalnan2 SDF";

        // 인스펙터의 Generation Settings / Update Atlas Texture가 원본을 찾을 수 있게 채워둔다.
        var settings = fontAsset.creationSettings;
        settings.sourceFontFileName = System.IO.Path.GetFileName(SourceFontPath);
        settings.sourceFontFileGUID = AssetDatabase.AssetPathToGUID(SourceFontPath);
        settings.pointSizeSamplingMode = 1; // Custom Size
        settings.pointSize = SamplingPointSize;
        settings.padding = AtlasPadding;
        settings.packingMode = 4;            // Fast
        settings.atlasWidth = AtlasWidth;
        settings.atlasHeight = AtlasHeight;
        settings.characterSetSelectionMode = 8; // Unicode Range (Hex)
        settings.characterSequence = "20-7E";
        settings.renderMode = (int)GlyphRenderMode.SDFAA;
        settings.includeFontFeatures = false;
        fontAsset.creationSettings = settings;

        AssetDatabase.CreateAsset(fontAsset, OutputPath);

        // 아틀라스 텍스처와 머티리얼을 서브에셋으로 묶는다.
        var atlas = fontAsset.atlasTextures[0];
        atlas.name = fontAsset.name + " Atlas";
        AssetDatabase.AddObjectToAsset(atlas, fontAsset);

        var material = fontAsset.material;
        material.name = fontAsset.name + " Material";
        AssetDatabase.AddObjectToAsset(material, fontAsset);

        // ASCII를 미리 구워둔다. 한글은 Dynamic이라 런타임에 필요한 글자만 채워진다.
        var ascii = new System.Text.StringBuilder(95);
        for (char c = ' '; c <= '~'; c++)
            ascii.Append(c);

        if (!fontAsset.TryAddCharacters(ascii.ToString(), out string missing))
            Debug.LogWarning($"[JalnanFont] 일부 ASCII 글리프를 넣지 못했다: {missing}");

        EditorUtility.SetDirty(fontAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[JalnanFont] 생성 완료: {OutputPath}", fontAsset);
        Selection.activeObject = fontAsset;
        EditorGUIUtility.PingObject(fontAsset);
    }

    /// <summary>
    /// 만들어진 Jalnan2 SDF를 TMP의 기본 폰트로 지정한다.
    /// 이후 새로 만드는 TextMeshPro 컴포넌트가 이 폰트를 쓴다. (기존 오브젝트는 그대로)
    /// </summary>
    [MenuItem("Tools/Fonts/Set Jalnan2 SDF as TMP Default")]
    public static void SetAsTmpDefault()
    {
        var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputPath);
        if (fontAsset == null)
        {
            Debug.LogError("[JalnanFont] Jalnan2 SDF가 없다. 먼저 Create Jalnan2 TMP Font Asset을 실행.");
            return;
        }

        var tmpSettings = AssetDatabase.LoadAssetAtPath<TMP_Settings>("Assets/TextMesh Pro/Resources/TMP Settings.asset");
        if (tmpSettings == null)
        {
            Debug.LogError("[JalnanFont] TMP Settings.asset을 찾을 수 없다.");
            return;
        }

        var so = new SerializedObject(tmpSettings);
        so.FindProperty("m_defaultFontAsset").objectReferenceValue = fontAsset;
        so.ApplyModifiedProperties();

        EditorUtility.SetDirty(tmpSettings);
        AssetDatabase.SaveAssets();

        Debug.Log("[JalnanFont] TMP 기본 폰트를 Jalnan2 SDF로 지정했다.", tmpSettings);
    }
}
#endif
