using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Resources/Audio/SFX 아래의 짧은 효과음을 한곳에서 재생한다.
/// 씬마다 AudioSource를 수동으로 연결하지 않아도 멀티플레이 RPC와 빌더 씬에서 같은 소리를 쓴다.
/// </summary>
public static class GameAudio
{
    private const string ResourceRoot = "Audio/SFX/";
    private static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
    private static AudioSource uiSource;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Clips.Clear();
        uiSource = null;
    }

    public static AudioClip Load(string key)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        if (!Clips.TryGetValue(key, out AudioClip clip))
        {
            clip = Resources.Load<AudioClip>(ResourceRoot + key);
            Clips[key] = clip;
        }
        return clip;
    }

    public static void PlayAt(string key, Vector3 position, float volume = 0.5f,
        float pitch = 1f, float minDistance = 1.5f, float maxDistance = 16f)
    {
        AudioClip clip = Load(key);
        if (clip == null)
            return;

        var go = new GameObject("SFX_" + key);
        go.transform.position = position;
        AudioSource source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.volume = Mathf.Clamp01(volume);
        source.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.dopplerLevel = 0f;
        source.Play();
        Object.Destroy(go, clip.length / Mathf.Abs(source.pitch) + 0.1f);
    }

    public static void PlayUi(string key, float volume = 0.4f, float pitch = 1f)
    {
        AudioClip clip = Load(key);
        if (clip == null)
            return;

        if (uiSource == null)
        {
            var go = new GameObject("UI SFX");
            Object.DontDestroyOnLoad(go);
            uiSource = go.AddComponent<AudioSource>();
            uiSource.spatialBlend = 0f;
            uiSource.playOnAwake = false;
        }

        uiSource.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
        uiSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    public static AudioSource CreateLoop(Transform owner, string key, float volume,
        float minDistance = 1.5f, float maxDistance = 14f)
    {
        if (owner == null)
            return null;

        AudioClip clip = Load(key);
        if (clip == null)
            return null;

        var go = new GameObject("Loop_" + key);
        go.transform.SetParent(owner, false);
        AudioSource source = go.AddComponent<AudioSource>();
        source.clip = clip;
        source.loop = true;
        source.playOnAwake = false;
        source.volume = Mathf.Clamp01(volume);
        source.spatialBlend = 1f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;
        source.dopplerLevel = 0f;
        return source;
    }
}
