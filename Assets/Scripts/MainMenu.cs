using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 메인 타이틀 화면 로직. 빌더(TitleSceneSetup)가 만든 계층을 이름으로 찾아 연결한다.
///
/// 화면 구성
///   TitlePanel   : STAB IN THE / BACK + 게임 시작 / 옵션 / 종료
///   PlayPanel    : 방 만들기(호스트) 창 + 참가하기 창(코드 입력 · 빠른 참가)
///   OptionsPanel : 소리 / 언어 / 만든 사람
/// 호스트·참가 모두 대기실(Lobby) 씬으로 들어간다.
///
/// 빠른 참가는 LanRoomBeacon으로 같은 공유기 안의 열린 방을 찾아 코드 없이 바로 들어간다.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Tooltip("호스트/참가 시 이동할 대기실 씬")]
    [SerializeField] private string lobbyScene = "Lobby";

    [Tooltip("빠른 참가로 방을 찾을 때 기다리는 시간(초)")]
    [SerializeField] private float quickJoinSeconds = 2.5f;

    private GameObject titlePanel;
    private GameObject playPanel;
    private GameObject optionsPanel;

    private GameObject soundsContent;
    private GameObject languageContent;
    private GameObject creditContent;

    private TMP_InputField codeField;
    private TextMeshProUGUI status;
    private TextMeshProUGUI languageValue;

    private readonly List<Button> playButtons = new List<Button>();
    private bool searching;

    private void Start()
    {
        GameOptions.Load();

        titlePanel = Child("TitlePanel");
        playPanel = Child("PlayPanel");
        optionsPanel = Child("OptionsPanel");

        soundsContent = Child("SoundsContent");
        languageContent = Child("LanguageContent");
        creditContent = Child("CreditContent");

        codeField = Component<TMP_InputField>("CodeField");
        status = Component<TextMeshProUGUI>("Status");
        languageValue = Component<TextMeshProUGUI>("LanguageValue");

        Wire("GameStartButton", ShowPlay);
        Wire("OptionsButton", ShowOptions);
        Wire("QuitButton", OnQuit);

        playButtons.Clear();
        playButtons.Add(Wire("HostButton", OnHost));
        playButtons.Add(Wire("JoinButton", OnJoin));
        playButtons.Add(Wire("QuickJoinButton", OnQuickJoin));
        playButtons.RemoveAll(b => b == null);
        Wire("BackFromPlay", ShowTitle);

        Wire("SoundsButton", () => ShowOptionContent(soundsContent));
        Wire("LanguageButton", () => ShowOptionContent(languageContent));
        Wire("CreditButton", () => ShowOptionContent(creditContent));
        Wire("BackFromOptions", ShowTitle);

        Wire("KoreanButton", () => SetLanguage(GameOptions.Korean));
        Wire("EnglishButton", () => SetLanguage(GameOptions.English));

        Slider volume = Component<Slider>("VolumeSlider");
        if (volume != null)
        {
            volume.SetValueWithoutNotify(GameOptions.MasterVolume);
            TextMeshProUGUI volumeValue = Component<TextMeshProUGUI>("VolumeValue");
            UnityAction<float> onChanged = value =>
            {
                GameOptions.SetMasterVolume(value);
                if (volumeValue != null)
                    volumeValue.text = Mathf.RoundToInt(value * 100f) + "%";
            };
            volume.onValueChanged.AddListener(onChanged);
            onChanged(GameOptions.MasterVolume);
        }

        RefreshLanguageLabel();
        ShowTitle();

        // 게임에서 커서가 잠긴 채 타이틀로 돌아왔을 수 있으니 풀어준다.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Update()
    {
        // Esc로 한 단계 뒤로.
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && !searching &&
            titlePanel != null && !titlePanel.activeSelf)
        {
            ShowTitle();
        }
    }

    // ------------------------------------------------------------ 화면 전환

    private void ShowTitle() => ShowPanel(titlePanel);

    private void ShowPlay()
    {
        SetStatus(string.Empty);
        ShowPanel(playPanel);
    }

    private void ShowOptions()
    {
        ShowPanel(optionsPanel);
        ShowOptionContent(soundsContent);
    }

    private void ShowPanel(GameObject panel)
    {
        if (searching)
            return; // 방을 찾는 중에는 화면이 바뀌지 않게 둔다

        if (titlePanel != null) titlePanel.SetActive(panel == titlePanel);
        if (playPanel != null) playPanel.SetActive(panel == playPanel);
        if (optionsPanel != null) optionsPanel.SetActive(panel == optionsPanel);
    }

    private void ShowOptionContent(GameObject content)
    {
        if (soundsContent != null) soundsContent.SetActive(content == soundsContent);
        if (languageContent != null) languageContent.SetActive(content == languageContent);
        if (creditContent != null) creditContent.SetActive(content == creditContent);
    }

    // ------------------------------------------------------------ 게임 시작

    private void OnHost()
    {
        if (searching)
            return;

        string address = RoomCode.LocalAddress();
        string code = RoomCode.FromAddress(address);

        GameLaunch.Mode = GameLaunch.LaunchMode.Host;
        GameLaunch.Address = address;
        GameLaunch.Code = code;

        SetStatus(string.IsNullOrEmpty(code)
            ? "방을 엽니다..."
            : $"방을 엽니다. 코드: {code}");
        SceneManager.LoadScene(lobbyScene);
    }

    private void OnJoin()
    {
        if (searching)
            return;

        string input = codeField != null ? codeField.text.Trim() : string.Empty;
        if (string.IsNullOrEmpty(input))
        {
            SetStatus("방 코드를 입력하세요.");
            return;
        }

        string address;
        string code;
        if (RoomCode.LooksLikeAddress(input))
        {
            // IP 주소를 직접 적은 경우도 받아준다.
            address = input;
            code = RoomCode.FromAddress(input);
        }
        else if (!RoomCode.TryToAddress(input, out address))
        {
            SetStatus($"코드를 확인하세요. (영문 {RoomCode.Length}글자)");
            return;
        }
        else
        {
            code = input.ToUpperInvariant();
        }

        Join(address, code, $"{address} 에 접속합니다...");
    }

    /// <summary>같은 공유기 안에서 열려 있는 방을 찾아 아무 데나 들어간다.</summary>
    private void OnQuickJoin()
    {
        if (searching)
            return;

        StartCoroutine(QuickJoinRoutine());
    }

    private IEnumerator QuickJoinRoutine()
    {
        searching = true;
        SetPlayButtonsInteractable(false);
        SetStatus("주변에서 열린 방을 찾는 중...");

        var rooms = new List<LanRoomBeacon.Room>();
        yield return LanRoomBeacon.Discover(quickJoinSeconds, rooms);

        searching = false;
        SetPlayButtonsInteractable(true);

        if (rooms.Count == 0)
        {
            SetStatus("주변에 열린 방이 없습니다. '방 열기'로 직접 방을 만들어 보세요.");
            yield break;
        }

        // 자리가 남은 방을 먼저, 없으면 아무거나.
        LanRoomBeacon.Room pick = rooms[0];
        bool found = false;
        foreach (LanRoomBeacon.Room room in rooms)
        {
            if (room.IsFull)
                continue;
            pick = room;
            found = true;
            break;
        }

        if (!found)
        {
            SetStatus("찾은 방이 모두 꽉 찼습니다. 잠시 뒤 다시 시도해 보세요.");
            yield break;
        }

        string code = string.IsNullOrEmpty(pick.Code) ? RoomCode.FromAddress(pick.Address) : pick.Code;
        Join(pick.Address, code, $"방을 찾았습니다. 들어갑니다... ({code})");
    }

    private void Join(string address, string code, string message)
    {
        GameLaunch.Mode = GameLaunch.LaunchMode.Client;
        GameLaunch.Address = address;
        GameLaunch.Code = code;

        SetStatus(message);
        SceneManager.LoadScene(lobbyScene);
    }

    private void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ------------------------------------------------------------ 옵션

    private void SetLanguage(string language)
    {
        GameOptions.SetLanguage(language);
        RefreshLanguageLabel();
    }

    private void RefreshLanguageLabel()
    {
        if (languageValue != null)
            languageValue.text = GameOptions.Language == GameOptions.English ? "English" : "한국어";
    }

    private void SetStatus(string text)
    {
        if (status != null)
            status.text = text;
    }

    private void SetPlayButtonsInteractable(bool value)
    {
        foreach (Button button in playButtons)
            if (button != null)
                button.interactable = value;
    }

    // ------------------------------------------------------------ 계층 탐색 (이름은 씬 전체에서 유일하다)

    private Button Wire(string childName, UnityAction callback)
    {
        Button button = Component<Button>(childName);
        if (button != null)
            button.onClick.AddListener(callback);
        return button;
    }

    private T Component<T>(string childName) where T : Component
    {
        Transform child = Find(transform, childName);
        return child != null ? child.GetComponent<T>() : null;
    }

    private GameObject Child(string childName)
    {
        Transform child = Find(transform, childName);
        return child != null ? child.gameObject : null;
    }

    private static Transform Find(Transform parent, string name)
    {
        foreach (Transform child in parent)
        {
            if (child.name == name)
                return child;

            Transform found = Find(child, name);
            if (found != null)
                return found;
        }
        return null;
    }
}
