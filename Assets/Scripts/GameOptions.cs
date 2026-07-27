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

    /// <summary>선택된 표시 언어. 저장만 되고 실제 문구 교체(로컬라이즈)는 아직 미구현.</summary>
    public static string Language { get; private set; } = Korean;

    /// <summary>게임 시작 시(타이틀 화면) 한 번 호출한다.</summary>
    public static void Load()
    {
        MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
        Language = PlayerPrefs.GetString(LanguageKey, Korean);
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
        Language = language == English ? English : Korean;
        PlayerPrefs.SetString(LanguageKey, Language);
        PlayerPrefs.Save();
    }

    private static void Apply()
    {
        AudioListener.volume = MasterVolume;
    }
}
