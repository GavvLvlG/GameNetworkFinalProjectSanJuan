using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Handles player input for picking up, dropping, and rotating held items.
/// </summary>
public class PlayerInteraction : NetworkBehaviour
{
    [SerializeField]
    private float _rotateSensitivity = 0.15f;

    [SerializeField]
    private float _interactRange = 3f;

    [SerializeField]
    private Camera _interactionCamera;

    private PickupItem _heldItem;
    private Quaternion _heldRotation = Quaternion.identity;

    // Per-player input actions — MPPM-safe.
    private InputAction _interactAction;
    private InputAction _rotateAction;

    public bool IsRotatingItem { get; private set; }

    private void Awake()
    {
        if (_interactionCamera == null)
            _interactionCamera = GetComponentInChildren<Camera>(true);

        // Right-click item rotation is not in the shared asset — bind inline.
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

        var playerInput = GetComponent<PlayerInput>();
        _interactAction = playerInput.actions["Interact"];
        _rotateAction.Enable();
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
            _rotateAction?.Disable();

        _rotateAction?.Dispose();
    }

    private void Update()
    {
        HandlePickup();
        HandleItemRotation();
    }

    private void HandlePickup()
    {
        if (_interactAction == null) return;
        if (!_interactAction.WasPressedThisFrame()) return;

        if (_heldItem != null)
            DropItem();
        else
            TryPickUp();
    }

    private void TryPickUp()
    {
        if (_interactionCamera == null) return;

        Ray interactionRay = new Ray(
            _interactionCamera.transform.position,
            _interactionCamera.transform.forward);

        if (!Physics.Raycast(interactionRay, out var hit, _interactRange))
            return;

        var item = hit.collider.GetComponent<PickupItem>();
        if (item == null) return;

        item.PickUpRpc(OwnerClientId);
        _heldItem = item;
        _heldRotation = Quaternion.identity;
    }

    private void DropItem()
    {
        _heldItem.DropRpc();
        _heldItem = null;
        _heldRotation = Quaternion.identity;
    }

    private void HandleItemRotation()
    {
        IsRotatingItem = _heldItem != null &&
                         _rotateAction != null &&
                         _rotateAction.IsPressed();

        if (!IsRotatingItem) return;

        var mouseAction = GetComponent<PlayerInput>()?.actions["Look"];
        if (mouseAction == null) return;

        var delta = mouseAction.ReadValue<Vector2>() * _rotateSensitivity;

        _heldRotation = Quaternion.AngleAxis(delta.x, Vector3.up)
                      * Quaternion.AngleAxis(-delta.y, Vector3.right)
                      * _heldRotation;

        _heldItem.SetHeldRotationRpc(_heldRotation);
    }
}
