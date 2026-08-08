#if UNITY_EDITOR
using TMPro;
using UnityEngine;

/// <summary>
/// 맵 빌더들이 세우는 월드 글자(팻말 등)를 한 곳에서 만든다.
///
/// 예전에는 내장 폰트 + TextMesh를 썼는데, 내장 폰트 참조가 씬에 저장되지 않아
/// 실행할 때마다 다시 붙여 주는 보조 스크립트(WorldSign)가 필요했다.
/// TMP 폰트 애셋(Jalnan2 SDF)은 프로젝트 애셋이라 그냥 저장되고, 한글도 또렷하게 나온다.
///
/// 글자 크기는 자동 맞춤(auto sizing)에 맡긴다 — 팻말 판 크기만 정해 주면
/// 문구가 길어지든 짧아지든 판 안에 알아서 들어간다.
/// </summary>
public static class BuilderText
{
    /// <summary>팻말 글자 기본색(짙은 나무에 새긴 느낌).</summary>
    public static readonly Color SignInk = new Color(0.13f, 0.10f, 0.07f);

    /// <summary>
    /// 월드에 놓이는 3D 글자를 만든다.
    /// </summary>
    /// <param name="size">글자가 들어갈 판의 크기(가로, 세로). 이 안에 맞춰 자동 축소된다.</param>
    public static TextMeshPro World(Transform parent, string name, Vector3 localPos, string text,
        Vector2 size, Color color, float maxFontSize = 6f)
    {
        // TMP_Text는 RectTransform이 필요하다. 만들 때 같이 붙여야 한다
        // (일반 Transform이 이미 붙은 뒤에는 RectTransform으로 바꿀 수 없다).
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = size;

        var label = go.AddComponent<TextMeshPro>();
        TMP_FontAsset font = JalnanFontAssetBuilder.Ensure();
        if (font != null)
            label.font = font;

        label.text = text;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.enableAutoSizing = true;
        label.fontSizeMin = 0.3f;
        label.fontSizeMax = maxFontSize;

        return label;
    }
}
#endif
