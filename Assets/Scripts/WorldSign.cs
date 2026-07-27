using UnityEngine;

/// <summary>
/// 월드에 세우는 나무 팻말의 글자(TextMesh)를 담당.
/// 내장 폰트/머티리얼 참조가 씬에 저장되지 않아도 실행 시 다시 붙여준다.
/// </summary>
[RequireComponent(typeof(TextMesh))]
[RequireComponent(typeof(MeshRenderer))]
public class WorldSign : MonoBehaviour
{
    private void Awake()
    {
        var text = GetComponent<TextMesh>();
        if (text.font == null)
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var renderer = GetComponent<MeshRenderer>();
        if (text.font != null && renderer.sharedMaterial != text.font.material)
            renderer.sharedMaterial = text.font.material;
    }
}
