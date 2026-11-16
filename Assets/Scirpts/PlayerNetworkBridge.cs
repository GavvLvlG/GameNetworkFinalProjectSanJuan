using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Bridges PlayerMovement with NGO ownership and gameplay scene loading.
/// </summary>
public class PlayerNetworkBridge : NetworkBehaviour
{
    [SerializeField] private string gameSceneName = "Gameplay";

    private PlayerMovement _movement;

    private void Awake()
    {
        _movement = GetComponent<PlayerMovement>();

        if (_movement == null)
            Debug.LogError("PlayerMovement component is missing.", this);
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            // Remote players must be on Default layer so other FPS cameras can see them.
            int defaultLayer = LayerMask.NameToLayer("Default");

            if (defaultLayer >= 0)
            {
                foreach (Transform t in GetComponentsInChildren<Transform>(true))
                    t.gameObject.layer = defaultLayer;
            }

            PlayerInput playerInput = GetComponent<PlayerInput>();
            if (playerInput != null)
                playerInput.enabled = false;

            if (_movement != null)
                _movement.enabled = false;

            return;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;

        // Handle the case where this player spawns after the gameplay scene is loaded.
        if (SceneManager.GetActiveScene().name == gameSceneName)
            StartPlaying();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (IsOwner && scene.name == gameSceneName)
            StartPlaying();
    }

   private void StartPlaying()
{
    if (_movement != null)
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