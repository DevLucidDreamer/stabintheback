using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 현재 렌더 파이프라인(URP)에 맞는 셰이더를 구한다.
///
/// Shader.Find("Universal Render Pipeline/Lit") 하나만 믿으면 안 된다.
/// 도메인 리로드 직후처럼 아직 셰이더가 로드되지 않은 시점에는 null이 나오고,
/// 그때 내장 "Standard"로 떨어지면 URP에서는 렌더가 안 되어 **분홍(마젠타)** 으로 보인다.
/// 그래서 파이프라인 애셋이 직접 알려주는 기본 셰이더를 먼저 쓴다.
/// </summary>
public static class PipelineShaders
{
    /// <summary>이 프로젝트에서 써야 할 불투명 Lit 셰이더.</summary>
    public static Shader Lit
    {
        get
        {
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline != null && pipeline.defaultShader != null)
                return pipeline.defaultShader;

            Shader urp = Shader.Find("Universal Render Pipeline/Lit");
            if (urp != null)
                return urp;

            return Shader.Find("Standard");
        }
    }

    /// <summary>Lit 셰이더로 단색 머티리얼을 만든다.</summary>
    public static Material CreateLit(Color color)
    {
        var material = new Material(Lit);
        SetBaseColor(material, color);
        return material;
    }

    /// <summary>셰이더가 URP든 Standard든 상관없이 기본 색을 지정한다.</summary>
    public static void SetBaseColor(Material material, Color color)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    /// <summary>현재 지정된 기본 색.</summary>
    public static Color GetBaseColor(Material material)
    {
        if (material == null)
            return Color.white;
        if (material.HasProperty("_BaseColor"))
            return material.GetColor("_BaseColor");
        if (material.HasProperty("_Color"))
            return material.GetColor("_Color");
        return Color.white;
    }

    /// <summary>
    /// 이 머티리얼이 현재 파이프라인에서 분홍으로 보이는 셰이더를 쓰고 있는지.
    /// (셰이더 유실, 또는 URP에서 지원하지 않는 내장 Standard/Legacy 셰이더)
    /// </summary>
    public static bool IsBroken(Material material)
    {
        if (material == null)
            return false;

        Shader shader = material.shader;
        if (shader == null || shader.name == "Hidden/InternalErrorShader")
            return true;

        // URP를 쓰는 프로젝트에서 내장 파이프라인 전용 셰이더는 렌더되지 않는다.
        if (GraphicsSettings.currentRenderPipeline == null)
            return false;

        return shader.name == "Standard"
               || shader.name == "Standard (Specular setup)"
               || shader.name.StartsWith("Legacy Shaders/");
    }
}
