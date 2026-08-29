using UnityEngine;

/// <summary>
/// 옵션 화면에서 만지는 설정값. PlayerPrefs에 저장되어 다음 실행에도 유지된다.
/// </summary>
public static class GameOptions
{
    private const string VolumeKey = "opt_master_volume";
    private const string LanguageKey = "opt_language";

    public const string Korean = "ko";
    public const string English = "en";

    public static float MasterVolume { get; private set; } = 1f;

    /// <summary>현재 지원 언어. 전체 현지화가 완료되기 전까지 한국어만 노출한다.</summary>
    public static string Language { get; private set; } = Korean;

    /// <summary>게임 시작 시(타이틀 화면) 한 번 호출한다.</summary>
    public static void Load()
    {
        MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
        Language = Korean;
        Apply();
    }

    public static void SetMasterVolume(float value)
    {
        MasterVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(VolumeKey, MasterVolume);
        PlayerPrefs.Save();
        Apply();
    }

    public static void SetLanguage(string language)
    {
        Language = Korean;
        PlayerPrefs.SetString(LanguageKey, Language);
        PlayerPrefs.Save();
    }

    private static void Apply()
    {
        AudioListener.volume = MasterVolume;
    }
}
