using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One persistent pair of 2D sources, with scene crossfades and independent music volume.</summary>
public sealed class GameMusic : MonoBehaviour
{
    private AudioSource current, previous;
    private float fade;
    private string currentKey;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<GameMusic>() != null || Application.isBatchMode) return;
        var go = new GameObject("Background Music");
        DontDestroyOnLoad(go);
        go.AddComponent<GameMusic>();
    }
    private void Awake()
    {
        current = gameObject.AddComponent<AudioSource>();
        previous = gameObject.AddComponent<AudioSource>();
        foreach (var source in new[] { current, previous })
        { source.loop = true; source.playOnAwake = false; source.spatialBlend = 0f; source.volume = 0f; }
        SceneManager.activeSceneChanged += SceneChanged;
        Select(SceneManager.GetActiveScene().name);
    }
    private void OnDestroy() => SceneManager.activeSceneChanged -= SceneChanged;
    private void SceneChanged(Scene before, Scene after) => Select(after.name);
    private void Select(string scene)
    {
        string key = scene == "Stage1_StoneTemple" || scene == "Stage2_BrokenBridge" || scene == "Stage3_UndergroundAltar" ? "temple_mystic" :
            scene == "Lobby" || scene == "MainTitle" ? "lobby_dungeon" : null;
        if (key == currentKey) return;
        currentKey = key;
        var swap = previous; previous = current; current = swap;
        current.Stop();
        current.clip = key == null ? null : Resources.Load<AudioClip>("Audio/Music/" + key);
        current.volume = 0f;
        if (current.clip != null) current.Play();
        fade = 0f;
    }
    private void Update()
    {
        fade = Mathf.MoveTowards(fade, 1f, Time.unscaledDeltaTime / 2f);
        float volume = GameOptions.MusicVolume * 0.32f;
        current.volume = volume * fade;
        previous.volume = volume * (1f - fade);
        if (fade >= 1f && previous.isPlaying) previous.Stop();
    }
}
