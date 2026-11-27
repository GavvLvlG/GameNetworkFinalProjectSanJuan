using System.Net;
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
    [SerializeField] private GameObject hostingPanel;
    [SerializeField] private GameObject lobbyPanel;

    [SerializeField] private string gameSceneName = "Gameplay";

    [Header("Inputs & Buttons")]
    [SerializeField] private TMP_InputField ipAddressInput;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button clientButton;
    [SerializeField] private Button continueToLobbyButton;

    private NetworkManager _subscribedNetworkManager;

    private void Start()
    {
        if (mainPanel == null || hostingPanel == null || lobbyPanel == null ||
            hostButton == null || clientButton == null || continueToLobbyButton == null)
        {
            Debug.LogError("Assign all panels and buttons in the Inspector.", this);
            enabled = false;
            return;
        }

        mainPanel.SetActive(true);
        hostingPanel.SetActive(false);
        lobbyPanel.SetActive(false);

        hostButton.onClick.AddListener(StartHostGame);
        clientButton.onClick.AddListener(StartClientGame);
        continueToLobbyButton.onClick.AddListener(OpenLobbyPanel);

        _subscribedNetworkManager = NetworkManager.Singleton;
        if (_subscribedNetworkManager != null)
            _subscribedNetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
    }

    private void OnDestroy()
    {
        if (hostButton != null)
            hostButton.onClick.RemoveListener(StartHostGame);

        if (clientButton != null)
            clientButton.onClick.RemoveListener(StartClientGame);

        if (continueToLobbyButton != null)
            continueToLobbyButton.onClick.RemoveListener(OpenLobbyPanel);

        if (_subscribedNetworkManager != null)
            _subscribedNetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
    }

    public void StartHostGame()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null)
        {
            Debug.LogError("No active NetworkManager found in the scene.", this);
            return;
        }

        if (networkManager.IsListening)
        {
            Debug.LogWarning("A network session is already active.", this);
            return;
        }

        if (networkManager.StartHost())
        {
            Debug.Log("Host started successfully.", this);
            OpenHostingPanel();
        }
        else
        {
            Debug.LogError("Failed to start host.", this);
        }
    }

    public void StartClientGame()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null)
        {
            Debug.LogError("No active NetworkManager found in the scene.", this);
            return;
        }

        if (networkManager.IsListening)
        {
            Debug.LogWarning("A network session is already active.", this);
            return;
        }

        if (ipAddressInput == null || string.IsNullOrWhiteSpace(ipAddressInput.text))
        {
            Debug.LogWarning("Enter the host IP address before joining.", this);
            return;
        }

        if (!IPAddress.TryParse(ipAddressInput.text.Trim(), out IPAddress targetAddress) ||
            targetAddress.Equals(IPAddress.Any) || targetAddress.Equals(IPAddress.IPv6Any))
        {
            Debug.LogWarning("Enter a valid IPv4 or IPv6 host address.", this);
            return;
        }

        UnityTransport transport = networkManager.GetComponent<UnityTransport>();
        if (transport == null)
        {
            Debug.LogError("UnityTransport is missing from the NetworkManager.", this);
            return;
        }

        transport.ConnectionData.Address = targetAddress.ToString();
        if (networkManager.StartClient())
        {
            Debug.Log($"Connecting to host at {targetAddress}...", this);
            OpenLobbyPanel();
        }
        else
        {
            Debug.LogError("Failed to start client.", this);
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (_subscribedNetworkManager == null || _subscribedNetworkManager.IsServer ||
            clientId != _subscribedNetworkManager.LocalClientId)
        {
            return;
        }

        Debug.LogWarning("Disconnected from the host. Return to the main menu and try again.", this);
        OpenMainPanel();
    }

    private void OpenHostingPanel()
    {
        mainPanel.SetActive(false);
        hostingPanel.SetActive(true);
        lobbyPanel.SetActive(false);
    }

    private void OpenLobbyPanel()
    {
        mainPanel.SetActive(false);
        hostingPanel.SetActive(false);
        lobbyPanel.SetActive(true);
    }

    private void OpenMainPanel()
    {
        mainPanel.SetActive(true);
        hostingPanel.SetActive(false);
        lobbyPanel.SetActive(false);
    }

    public void EnterGameScene()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || !networkManager.IsListening || !networkManager.IsHost)
        {
            Debug.LogWarning("Only the active host can start the game.", this);
            return;
        }

        if (networkManager.SceneManager == null || string.IsNullOrWhiteSpace(gameSceneName))
        {
            Debug.LogError("Network scene management or the gameplay scene name is not configured.", this);
            return;
        }

        networkManager.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
    }

    public void QuitGame()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager != null && networkManager.IsListening)
            networkManager.Shutdown();

        Application.Quit();
    }
}
