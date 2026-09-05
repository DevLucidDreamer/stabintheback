using UnityEngine;

/// <summary>
/// 옵션 화면에서 만지는 설정값. PlayerPrefs에 저장되어 다음 실행에도 유지된다.
/// </summary>
public static class GameOptions
{
    private const string VolumeKey = "opt_master_volume";
    private const string SfxVolumeKey = "opt_sfx_volume";
    private const string LanguageKey = "opt_language";
    private const string VoiceModeKey = "opt_voice_mode";

    public const string Korean = "ko";
    public const string English = "en";

    public static float MasterVolume { get; private set; } = 1f;
    public static float SfxVolume { get; private set; } = 1f;
    public static float MusicVolume { get; private set; } = 0.65f;
    public static bool PushToTalk { get; private set; } = true;

    /// <summary>현재 지원 언어. 전체 현지화가 완료되기 전까지 한국어만 노출한다.</summary>
    public static string Language { get; private set; } = Korean;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize() => Load();

    /// <summary>저장된 옵션을 읽고 즉시 적용한다.</summary>
    public static void Load()
    {
        MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, 1f));
        SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, 1f));
        MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat("opt_music_volume", 0.65f));
        PushToTalk = PlayerPrefs.GetInt(VoiceModeKey, 1) != 0;
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

    public static void SetSfxVolume(float value)
    {
        SfxVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SfxVolumeKey, SfxVolume);
        PlayerPrefs.Save();
        GameAudio.ApplyVolumeSettings();
    }

    public static void SetPushToTalk(bool enabled)
    {
        PushToTalk = enabled;
        PlayerPrefs.SetInt(VoiceModeKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    public static void SetMusicVolume(float value)
    {
        MusicVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat("opt_music_volume", MusicVolume);
        PlayerPrefs.Save();
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
        GameAudio.ApplyVolumeSettings();
    }
}
