using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 메인 타이틀 화면 로직. 버튼 클릭을 이름으로 찾아 연결한다(빌더가 만든 계층 이름 기준).
/// - 게임 시작(호스트) / 참가하기(IP 입력) / 나가기
/// </summary>
public class MainMenu : MonoBehaviour
{
    [SerializeField] private string gameScene = "NetworkDemo";

    private InputField addressField;
    private Text status;

    private void Start()
    {
        Wire("HostButton", OnHost);
        Wire("JoinButton", OnJoin);
        Wire("QuitButton", OnQuit);

        Transform af = transform.Find("AddressField");
        if (af != null)
            addressField = af.GetComponent<InputField>();

        Transform st = transform.Find("Status");
        if (st != null)
            status = st.GetComponent<Text>();

        SetStatus(string.Empty);

        // 게임에서 커서가 잠긴 채 타이틀로 돌아왔을 수 있으니 풀어준다.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void Wire(string childName, UnityAction callback)
    {
        Transform c = transform.Find(childName);
        if (c != null && c.TryGetComponent(out Button button))
            button.onClick.AddListener(callback);
    }

    private void OnHost()
    {
        GameLaunch.Mode = GameLaunch.LaunchMode.Host;
        SetStatus("호스트로 게임을 시작합니다...");
        SceneManager.LoadScene(gameScene);
    }

    private void OnJoin()
    {
        string addr = addressField != null && !string.IsNullOrWhiteSpace(addressField.text)
            ? addressField.text.Trim()
            : "127.0.0.1";
        GameLaunch.Mode = GameLaunch.LaunchMode.Client;
        GameLaunch.Address = addr;
        SetStatus($"{addr} 에 접속합니다...");
        SceneManager.LoadScene(gameScene);
    }

    private void OnQuit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SetStatus(string text)
    {
        if (status != null)
            status.text = text;
    }
}
