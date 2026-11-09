
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using Unity.Netcode.Transports.UTP;

public interface IMultiplayerMenuController
{
    void QuitGame();
    void StartClientGame();
    void StartHostGame();
}

public class MultiplayerMenuController : MonoBehaviour, IMultiplayerMenuController
{
    [Header("UI Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject lobbyPanel;

    [SerializeField] private string gameSceneName = "Gameplay";

    [Header("Inputs & Buttons")]
    [SerializeField] private TMP_InputField ipAddressInput;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;

    private void Start()
    {
        if (mainPanel == null || lobbyPanel == null ||
            hostButton == null || clientButton == null)
        {
            Debug.LogError(
                "Assign Main Panel, Lobby Panel, Host Button, and Client Button in the Inspector.",
                this
            );
            enabled = false;
            return;
        }

        mainPanel.SetActive(true);
        lobbyPanel.SetActive(false);

        hostButton.onClick.AddListener(StartHostGame);
        clientButton.onClick.AddListener(StartClientGame);
    }

    private void OnDestroy()
    {
        if (hostButton != null)
            hostButton.onClick.RemoveListener(StartHostGame);

        if (clientButton != null)
            clientButton.onClick.RemoveListener(StartClientGame);
    }

    public void StartHostGame()
{
    if (NetworkManager.Singleton == null)
    {
        Debug.LogError("No active NetworkManager found in the scene.");
        return;
    }

    if (NetworkManager.Singleton.StartHost())
    {
        Debug.Log("Host started successfully.");
        OpenLobbyPanel();
    }
    else
    {
        Debug.LogError("Failed to start host.");
    }
}

public void StartClientGame()
{
    if (ipAddressInput == null)
    {
        Debug.LogError("Assign the IP Address Input Field in the Inspector.", this);
        return;
    }

    if (NetworkManager.Singleton == null)
    {
        Debug.LogError("No active NetworkManager found in the scene.");
        return;
    }

    UnityTransport transport =
        NetworkManager.Singleton.GetComponent<UnityTransport>();

    if (transport == null)
    {
        Debug.LogError("UnityTransport is missing from the NetworkManager.");
        return;
    }

    string targetIP = string.IsNullOrWhiteSpace(ipAddressInput.text)
        ? "127.0.0.1"
        : ipAddressInput.text.Trim();

    transport.ConnectionData.Address = targetIP;

    if (NetworkManager.Singleton.StartClient())
    {
        Debug.Log($"Connecting to host at {targetIP}...");
        OpenLobbyPanel();
    }
    else
    {
        Debug.LogError("Failed to start client.");
    }
}

private void OpenLobbyPanel()
{
    mainPanel.SetActive(false);
    lobbyPanel.SetActive(true);
}

public void EnterGameScene()
{
    if (NetworkManager.Singleton == null ||
        !NetworkManager.Singleton.IsHost)
    {
        Debug.LogWarning("Only the host can start the game.");
        return;
    }

    NetworkManager.Singleton.SceneManager.LoadScene(
        gameSceneName,
        LoadSceneMode.Single
    );
}

    public void QuitGame()
    {
        Application.Quit();
    }
}