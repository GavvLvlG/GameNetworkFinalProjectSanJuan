using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Networked item that can be picked up, held, and dropped by players.
/// All ownership decisions are validated by the server using the RPC sender identity.
/// </summary>
public class PickupItem : NetworkBehaviour
{
    private const ulong NoHolder = ulong.MaxValue;

    [SerializeField] private float _holdDistance = 3f;
    [SerializeField] private float _minHoldDistance = 1.8f;
    [SerializeField] private float _stuckDropTime = 0.5f;
    [SerializeField, Min(0f)] private float _rotationUpdateInterval = 0.02f;

    public NetworkVariable<ulong> HeldBy = new NetworkVariable<ulong>(
        NoHolder,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private Rigidbody _rb;
    private Collider _col;
    private Quaternion _heldRotation = Quaternion.identity;
    private Transform _holderCamPoint;
    private GameObject _holderRoot;
    private float _currentDist;
    private float _distVelocity;
    private float _stuckTimer;
    private float _nextRotationUpdateTime;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _col = GetComponent<Collider>();
        _currentDist = _holdDistance;
    }

    public override void OnNetworkSpawn()
    {
        HeldBy.OnValueChanged += OnHeldByChanged;
        OnHeldByChanged(NoHolder, HeldBy.Value);
    }

    public override void OnNetworkDespawn()
    {
        HeldBy.OnValueChanged -= OnHeldByChanged;
    }

    private void OnHeldByChanged(ulong previous, ulong current)
    {
        bool held = current != NoHolder;

        if (_col != null)
            _col.enabled = !held;

        if (_rb == null)
            return;

        if (held)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
        }

        _rb.isKinematic = held;
        _rb.interpolation = held ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
    }

    private void LateUpdate()
    {
        if (!IsServer || HeldBy.Value == NoHolder)
            return;

        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null ||
            !networkManager.ConnectedClients.TryGetValue(HeldBy.Value, out NetworkClient client) ||
            client.PlayerObject == null)
        {
            ClearHolderState();
            HeldBy.Value = NoHolder;
            return;
        }

        Transform cameraPoint = _holderCamPoint != null ? _holderCamPoint : client.PlayerObject.transform;
        Vector3 origin = cameraPoint.position;
        Vector3 forward = cameraPoint.forward;
        float rawTarget = Mathf.Max(_minHoldDistance, _holdDistance);
        RaycastHit[] hits = Physics.RaycastAll(origin, forward, rawTarget);
        Array.Sort(hits, (first, second) => first.distance.CompareTo(second.distance));

        foreach (RaycastHit hit in hits)
        {
            if (_holderRoot != null && hit.collider.transform.IsChildOf(_holderRoot.transform))
                continue;

            rawTarget = Mathf.Max(hit.distance - 0.15f, _minHoldDistance);
            break;
        }

        if (rawTarget <= _minHoldDistance + 0.01f)
        {
            _stuckTimer += Time.deltaTime;
            if (_stuckTimer >= _stuckDropTime)
            {
                ClearHolderState();
                HeldBy.Value = NoHolder;
                return;
            }
        }
        else
        {
            _stuckTimer = 0f;
        }

        float smoothTime = rawTarget < _currentDist ? 0.04f : 0.3f;
        _currentDist = Mathf.SmoothDamp(_currentDist, rawTarget, ref _distVelocity, smoothTime);
        transform.position = origin + forward * _currentDist;
        transform.rotation = client.PlayerObject.transform.rotation * _heldRotation;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void SetHeldRotationRpc(Quaternion rotation, RpcParams rpcParams = default)
    {
        if (!IsServer || HeldBy.Value != rpcParams.Receive.SenderClientId || !IsFinite(rotation))
            return;

        float magnitudeSquared = rotation.x * rotation.x + rotation.y * rotation.y +
                                rotation.z * rotation.z + rotation.w * rotation.w;
        if (magnitudeSquared < 0.0001f || magnitudeSquared > 100f)
            return;

        if (Time.unscaledTime < _nextRotationUpdateTime)
            return;

        _nextRotationUpdateTime = Time.unscaledTime + _rotationUpdateInterval;
        float inverseMagnitude = 1f / Mathf.Sqrt(magnitudeSquared);
        _heldRotation = new Quaternion(
            rotation.x * inverseMagnitude,
            rotation.y * inverseMagnitude,
            rotation.z * inverseMagnitude,
            rotation.w * inverseMagnitude);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void PickUpRpc(RpcParams rpcParams = default)
    {
        if (!IsServer || HeldBy.Value != NoHolder || _col == null || !NetworkObject.IsSpawned)
            return;

        NetworkManager networkManager = NetworkManager.Singleton;
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        if (networkManager == null ||
            !networkManager.ConnectedClients.TryGetValue(senderClientId, out NetworkClient client) ||
            client.PlayerObject == null)
        {
            return;
        }

        Transform cameraPoint = client.PlayerObject.transform.Find("CameraPoint");
        if (cameraPoint == null)
            cameraPoint = client.PlayerObject.transform;

        if (!IsWithinPickupRange(cameraPoint) || !HasLineOfSight(cameraPoint, client.PlayerObject.gameObject))
            return;

        _holderCamPoint = cameraPoint;
        _holderRoot = client.PlayerObject.gameObject;
        _currentDist = Mathf.Max(_minHoldDistance, _holdDistance);
        _distVelocity = 0f;
        _stuckTimer = 0f;
        _heldRotation = Quaternion.identity;
        _nextRotationUpdateTime = 0f;
        HeldBy.Value = senderClientId;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    public void DropRpc(RpcParams rpcParams = default)
    {
        if (!IsServer || HeldBy.Value == NoHolder || HeldBy.Value != rpcParams.Receive.SenderClientId)
            return;

        Vector3 throwDirection = _holderCamPoint != null ? _holderCamPoint.forward : Vector3.forward;
        ClearHolderState();
        HeldBy.Value = NoHolder;

        if (_rb != null)
            _rb.AddForce(throwDirection * 4f, ForceMode.Impulse);
    }

    private bool IsWithinPickupRange(Transform cameraPoint)
    {
        Vector3 closestPoint = _col.ClosestPoint(cameraPoint.position);
        float allowedDistance = Mathf.Max(0.1f, _holdDistance) + 0.1f;
        return Vector3.Distance(cameraPoint.position, closestPoint) <= allowedDistance;
    }

    private bool HasLineOfSight(Transform cameraPoint, GameObject playerRoot)
    {
        Vector3 targetPoint = _col.ClosestPoint(cameraPoint.position);
        Vector3 direction = targetPoint - cameraPoint.position;
        float distance = direction.magnitude;
        if (distance <= 0.001f)
            return true;

        RaycastHit[] hits = Physics.RaycastAll(
            cameraPoint.position,
            direction / distance,
            distance + 0.05f,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);
        Array.Sort(hits, (first, second) => first.distance.CompareTo(second.distance));

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(playerRoot.transform))
                continue;

            return hit.collider.GetComponentInParent<NetworkObject>() == NetworkObject;
        }

        return false;
    }

    private void ClearHolderState()
    {
        _holderCamPoint = null;
        _holderRoot = null;
        _heldRotation = Quaternion.identity;
        _currentDist = Mathf.Max(_minHoldDistance, _holdDistance);
        _distVelocity = 0f;
        _stuckTimer = 0f;
    }

    private static bool IsFinite(Quaternion rotation)
    {
        return IsFinite(rotation.x) && IsFinite(rotation.y) && IsFinite(rotation.z) && IsFinite(rotation.w);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
