using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Restricts local input, cameras, and audio listeners to the owning client while keeping server movement active.
/// </summary>
public class PlayerNetworkBridge : NetworkBehaviour
{
    [SerializeField] private string gameSceneName = "Gameplay";

    private PlayerMovement _movement;
    private PlayerInput _playerInput;
    private Camera[] _playerCameras;
    private AudioListener[] _audioListeners;

    private void Awake()
    {
        _movement = GetComponent<PlayerMovement>();
        _playerInput = GetComponent<PlayerInput>();
        _playerCameras = GetComponentsInChildren<Camera>(true);
        _audioListeners = GetComponentsInChildren<AudioListener>(true);

        if (_movement == null)
            Debug.LogError("PlayerMovement component is missing.", this);
    }

    public override void OnNetworkSpawn()
    {
        bool isLocalOwner = IsOwner && IsClient;
        ConfigureLocalComponents(isLocalOwner);

        if (!isLocalOwner)
        {
            int defaultLayer = LayerMask.NameToLayer("Default");
            if (defaultLayer >= 0)
            {
                foreach (Transform child in GetComponentsInChildren<Transform>(true))
                    child.gameObject.layer = defaultLayer;
            }
        }

        if (!IsOwner)
            return;

        SceneManager.sceneLoaded += OnSceneLoaded;
        if (SceneManager.GetActiveScene().name == gameSceneName)
            StartPlaying();
    }

    private void ConfigureLocalComponents(bool isLocalOwner)
    {
        if (_playerInput != null)
            _playerInput.enabled = isLocalOwner;

        foreach (Camera playerCamera in _playerCameras)
        {
            if (playerCamera != null)
                playerCamera.enabled = isLocalOwner;
        }

        foreach (AudioListener audioListener in _audioListeners)
        {
            if (audioListener != null)
                audioListener.enabled = isLocalOwner;
        }

        if (_movement != null)
            _movement.enabled = IsServer || isLocalOwner;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsOwner && scene.name == gameSceneName)
            StartPlaying();
    }

    private void StartPlaying()
    {
        if (_movement != null && (IsServer || IsOwner))
            _movement.enabled = true;
    }

    public override void OnNetworkDespawn()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
}
