using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Spawns one server-owned player object at an available spawn point per approved client.
/// </summary>
[DefaultExecutionOrder(-10)]
public class PlayerSpawnManager : MonoBehaviour
{
    [SerializeField] private Transform[] _spawnPoints;

    private readonly Dictionary<ulong, int> _clientSpawnSlots = new Dictionary<ulong, int>();
    private NetworkManager _networkManager;

    private void Start()
    {
        _networkManager = NetworkManager.Singleton;
        if (_networkManager == null)
        {
            Debug.LogError("[SpawnManager] No active NetworkManager was found.", this);
            return;
        }

        _networkManager.OnClientConnectedCallback += OnClientConnected;
        _networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        _networkManager.OnServerStopped += OnServerStopped;

        if (_networkManager.IsServer)
        {
            foreach (ulong clientId in _networkManager.ConnectedClients.Keys)
                OnClientConnected(clientId);
        }
    }

    private void OnDestroy()
    {
        if (_networkManager == null)
            return;

        _networkManager.OnClientConnectedCallback -= OnClientConnected;
        _networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        _networkManager.OnServerStopped -= OnServerStopped;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (_networkManager == null || !_networkManager.IsServer || _clientSpawnSlots.ContainsKey(clientId))
            return;

        int spawnIndex = FindAvailableSpawnIndex();
        if (spawnIndex < 0)
        {
            Debug.LogWarning($"[SpawnManager] No valid spawn slot is available for client {clientId}; disconnecting.", this);
            _networkManager.DisconnectClient(clientId);
            return;
        }

        GameObject playerPrefab = _networkManager.NetworkConfig.PlayerPrefab;
        if (playerPrefab == null)
        {
            Debug.LogError("[SpawnManager] PlayerPrefab is not assigned in NetworkConfig.", this);
            _networkManager.DisconnectClient(clientId);
            return;
        }

        Transform spawnPoint = _spawnPoints[spawnIndex];
        GameObject player = Instantiate(playerPrefab, spawnPoint.position, spawnPoint.rotation);
        NetworkObject networkObject = player.GetComponent<NetworkObject>();
        if (networkObject == null)
        {
            Debug.LogError("[SpawnManager] Player prefab has no NetworkObject component.", player);
            Destroy(player);
            _networkManager.DisconnectClient(clientId);
            return;
        }

        networkObject.SpawnAsPlayerObject(clientId, destroyWithScene: true);
        _clientSpawnSlots.Add(clientId, spawnIndex);

        PlayerColour colour = player.GetComponent<PlayerColour>();
        if (colour != null)
            colour.PlayerIndex.Value = spawnIndex;
    }

    private void OnClientDisconnected(ulong clientId)
    {
        _clientSpawnSlots.Remove(clientId);
    }

    private void OnServerStopped(bool _)
    {
        _clientSpawnSlots.Clear();
    }

    private int FindAvailableSpawnIndex()
    {
        if (!HasValidSpawnPoints())
            return -1;

        for (int index = 0; index < _spawnPoints.Length; index++)
        {
            if (!_clientSpawnSlots.ContainsValue(index))
                return index;
        }

        return -1;
    }

    private bool HasValidSpawnPoints()
    {
        if (_spawnPoints == null || _spawnPoints.Length == 0)
            return false;

        for (int index = 0; index < _spawnPoints.Length; index++)
        {
            if (_spawnPoints[index] == null)
                return false;
        }

        return true;
    }
}
