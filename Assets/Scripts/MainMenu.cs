using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 메인 타이틀 화면 로직. 빌더(TitleSceneSetup)가 만든 계층을 이름으로 찾아 연결한다.
///
/// 화면 구성
///   Title       : Game Start / Options / Quit
///   Game Start  : 방 코드 입력 → Host Game(방 만들기) / Join(참가)
///   Options     : Sounds / Language / Credit
/// 호스트·참가 모두 대기실(Lobby) 씬으로 들어간다.
/// </summary>
public class MainMenu : MonoBehaviour
{
    [Tooltip("호스트/참가 시 이동할 대기실 씬")]
    [SerializeField] private string lobbyScene = "Lobby";

    private GameObject titlePanel;
    private GameObject gameStartPanel;
    private GameObject optionsPanel;

    private GameObject soundsContent;
    private GameObject languageContent;
    private GameObject creditContent;

    private InputField codeField;
    private Text status;
    private Text languageValue;

    private void Start()
    {
        GameOptions.Load();

        titlePanel = Child("TitlePanel");
        gameStartPanel = Child("GameStartPanel");
        optionsPanel = Child("OptionsPanel");

        soundsContent = Child("SoundsContent");
        languageContent = Child("LanguageContent");
        creditContent = Child("CreditContent");

        codeField = Component<InputField>("CodeField");
        status = Component<Text>("Status");
        languageValue = Component<Text>("LanguageValue");

        Wire("GameStartButton", ShowGameStart);
        Wire("OptionsButton", ShowOptions);
        Wire("QuitButton", OnQuit);

        Wire("HostButton", OnHost);
        Wire("JoinButton", OnJoin);
        Wire("BackFromGameStart", ShowTitle);

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
            Text volumeValue = Component<Text>("VolumeValue");
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

    // ------------------------------------------------------------ 화면 전환

    private void ShowTitle() => ShowPanel(titlePanel);

    private void ShowGameStart()
    {
        SetStatus(string.Empty);
        ShowPanel(gameStartPanel);
    }

    private void ShowOptions()
    {
        ShowPanel(optionsPanel);
        ShowOptionContent(soundsContent);
    }

    private void ShowPanel(GameObject panel)
    {
        if (titlePanel != null) titlePanel.SetActive(panel == titlePanel);
        if (gameStartPanel != null) gameStartPanel.SetActive(panel == gameStartPanel);
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

        GameLaunch.Mode = GameLaunch.LaunchMode.Client;
        GameLaunch.Address = address;
        GameLaunch.Code = code;

        SetStatus($"{address} 에 접속합니다...");
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

    // ------------------------------------------------------------ 계층 탐색 (이름은 씬 전체에서 유일하다)

    private void Wire(string childName, UnityAction callback)
    {
        Button button = Component<Button>(childName);
        if (button != null)
            button.onClick.AddListener(callback);
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
