using UnityEngine;

/// <summary>출시 빌드가 메뉴에서도 GPU를 무제한 사용하지 않도록 안전한 기본값을 적용한다.</summary>
public static class RuntimeQualityDefaults
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 1;
        Application.targetFrameRate = 120;
    }
}
