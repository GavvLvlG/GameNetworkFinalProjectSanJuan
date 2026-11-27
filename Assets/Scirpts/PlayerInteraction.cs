using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles owner input for picking up, dropping, and rotating held items.
/// </summary>
public class PlayerInteraction : NetworkBehaviour
{
    [SerializeField] private float _rotateSensitivity = 0.15f;
    [SerializeField] private float _interactRange = 3f;
    [SerializeField] private Camera _interactionCamera;
    [SerializeField, Min(0f)] private float _pickupResponseTimeout = 1f;

    private PickupItem _heldItem;
    private Quaternion _heldRotation = Quaternion.identity;
    private InputAction _interactAction;
    private InputAction _rotateAction;
    private InputAction _lookAction;
    private float _pickupRequestTime;
    private bool _pickupPending;

    public bool IsRotatingItem { get; private set; }

    private void Awake()
    {
        if (_interactionCamera == null)
            _interactionCamera = GetComponentInChildren<Camera>(true);

        _rotateAction = new InputAction("RotateItem", InputActionType.Button);
        _rotateAction.AddBinding("<Mouse>/rightButton");
    }

    public override void OnNetworkSpawn()
    {
        if (!IsOwner)
        {
            enabled = false;
            return;
        }

        PlayerInput playerInput = GetComponent<PlayerInput>();
        if (playerInput == null || playerInput.actions == null)
        {
            Debug.LogError("PlayerInteraction requires a PlayerInput component with an assigned action asset.", this);
            enabled = false;
            return;
        }

        _interactAction = playerInput.actions.FindAction("Interact", false);
        _lookAction = playerInput.actions.FindAction("Look", false);
        if (_interactAction == null || _lookAction == null)
        {
            Debug.LogError("PlayerInput action asset must define Interact and Look actions.", this);
            enabled = false;
            return;
        }

        _rotateAction.Enable();
    }

    public override void OnNetworkDespawn()
    {
        _rotateAction?.Disable();
        _interactAction = null;
        _lookAction = null;
        _heldItem = null;
        _pickupPending = false;
        IsRotatingItem = false;
    }

    private void OnDestroy()
    {
        _rotateAction?.Dispose();
    }

    private void Update()
    {
        UpdatePickupRequestState();
        HandlePickup();
        HandleItemRotation();
    }

    private void UpdatePickupRequestState()
    {
        if (!_pickupPending || _heldItem == null)
            return;

        if (_heldItem.HeldBy.Value == OwnerClientId)
        {
            _pickupPending = false;
            return;
        }

        if (_heldItem.HeldBy.Value != ulong.MaxValue ||
            Time.unscaledTime - _pickupRequestTime >= _pickupResponseTimeout)
        {
            _heldItem = null;
            _heldRotation = Quaternion.identity;
            _pickupPending = false;
        }
    }

    private void HandlePickup()
    {
        if (_interactAction == null || !_interactAction.WasPressedThisFrame() || _pickupPending)
            return;

        if (_heldItem != null)
        {
            DropItem();
            return;
        }

        TryPickUp();
    }

    private void TryPickUp()
    {
        if (_interactionCamera == null)
            return;

        Ray interactionRay = new Ray(
            _interactionCamera.transform.position,
            _interactionCamera.transform.forward);

        if (!Physics.Raycast(interactionRay, out RaycastHit hit, Mathf.Max(0.1f, _interactRange)))
            return;

        PickupItem item = hit.collider.GetComponentInParent<PickupItem>();
        if (item == null || !item.IsSpawned || item.HeldBy.Value != ulong.MaxValue)
            return;

        item.PickUpRpc();
        _heldItem = item;
        _heldRotation = Quaternion.identity;
        _pickupPending = true;
        _pickupRequestTime = Time.unscaledTime;
    }

    private void DropItem()
    {
        if (_heldItem != null && _heldItem.HeldBy.Value == OwnerClientId)
            _heldItem.DropRpc();

        _heldItem = null;
        _heldRotation = Quaternion.identity;
        _pickupPending = false;
    }

    private void HandleItemRotation()
    {
        if (_heldItem != null && !_pickupPending && _heldItem.HeldBy.Value != OwnerClientId)
        {
            _heldItem = null;
            _heldRotation = Quaternion.identity;
        }

        IsRotatingItem = _heldItem != null &&
                         !_pickupPending &&
                         _heldItem.HeldBy.Value == OwnerClientId &&
                         _rotateAction != null &&
                         _rotateAction.IsPressed();

        if (!IsRotatingItem || _lookAction == null)
            return;

        Vector2 delta = _lookAction.ReadValue<Vector2>() * _rotateSensitivity;
        _heldRotation = Quaternion.AngleAxis(delta.x, Vector3.up) *
                        Quaternion.AngleAxis(-delta.y, Vector3.right) *
                        _heldRotation;

        _heldItem.SetHeldRotationRpc(_heldRotation);
    }
}
