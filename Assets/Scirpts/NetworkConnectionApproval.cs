using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Applies connection approval before clients can join the lobby and reserves capacity for pending connections.
/// </summary>
[DefaultExecutionOrder(-20)]
public class NetworkConnectionApproval : MonoBehaviour
{
    [SerializeField, Min(1)] private int _maxPlayers = 4;

    private readonly HashSet<ulong> _pendingClientIds = new HashSet<ulong>();
    private NetworkManager _networkManager;

    private void Awake()
    {
        _networkManager = GetComponent<NetworkManager>();
        if (_networkManager == null)
            _networkManager = NetworkManager.Singleton;

        if (_networkManager == null)
        {
            Debug.LogError("[ConnectionApproval] No NetworkManager was found.", this);
            enabled = false;
            return;
        }

        _maxPlayers = Mathf.Max(1, _maxPlayers);
        _networkManager.NetworkConfig.ConnectionApproval = true;
        _networkManager.ConnectionApprovalCallback += OnApproveConnection;
        _networkManager.OnClientConnectedCallback += OnClientConnected;
        _networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        _networkManager.OnServerStopped += OnServerStopped;
    }

    private void OnDestroy()
    {
        if (_networkManager == null)
            return;

        _networkManager.ConnectionApprovalCallback -= OnApproveConnection;
        _networkManager.OnClientConnectedCallback -= OnClientConnected;
        _networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        _networkManager.OnServerStopped -= OnServerStopped;
    }

    private void OnApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
    {
        response.CreatePlayerObject = false;

        if (_networkManager == null || !_networkManager.IsServer)
        {
            response.Approved = false;
            response.Reason = "Server is not ready to accept connections.";
            return;
        }

        if (request.ClientNetworkId == NetworkManager.ServerClientId)
        {
            response.Approved = true;
            return;
        }

        bool alreadyPending = _pendingClientIds.Contains(request.ClientNetworkId);
        int reservedSlots = _networkManager.ConnectedClients.Count + _pendingClientIds.Count;
        if (!alreadyPending && reservedSlots >= _maxPlayers)
        {
            response.Approved = false;
            response.Reason = "Server is full.";
            return;
        }

        _pendingClientIds.Add(request.ClientNetworkId);
        response.Approved = true;
    }

    private void OnClientConnected(ulong clientId)
    {
        _pendingClientIds.Remove(clientId);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        _pendingClientIds.Remove(clientId);
    }

    private void OnServerStopped(bool _)
    {
        _pendingClientIds.Clear();
    }
}
