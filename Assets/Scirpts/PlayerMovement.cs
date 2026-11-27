using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Collects owner input and simulates movement on the server to prevent client-authored transform movement.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    private const float InputSendInterval = 1f / 20f;
    private const float StaleInputTimeout = 0.25f;
    private const float MaximumTurnRate = 720f;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintSpeed = 8f;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float groundedForce = -2f;

    [Header("Camera")]
    [Tooltip("Leave empty to use the current Camera tagged MainCamera.")]
    [SerializeField] private Camera playerCamera;

    [Tooltip("Optional camera or Cinemachine follow target used for looking.")]
    [SerializeField] private Transform cameraPivot;

    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float minLookAngle = -80f;
    [SerializeField] private float maxLookAngle = 80f;

    [Header("Cursor")]
    [SerializeField] private bool lockCursorOnStart = true;

    private CharacterController _characterController;
    private float _verticalVelocity;
    private float _cameraPitch;
    private Vector2 _serverMoveInput;
    private bool _serverSprinting;
    private float _serverYaw;
    private float _lastInputReceivedTime;
    private float _nextInputSendTime;
    private float _nextServerInputAcceptTime;

    private void Awake()
    {
        _characterController = GetComponent<CharacterController>();

        if (playerCamera == null)
            playerCamera = GetComponentInChildren<Camera>(true);

        if (cameraPivot == null && playerCamera != null)
            cameraPivot = playerCamera.transform;

        _serverYaw = transform.eulerAngles.y;
    }

    public override void OnNetworkSpawn()
    {
        _serverYaw = transform.eulerAngles.y;
        _lastInputReceivedTime = Time.unscaledTime;

        if (IsOwner && IsClient && lockCursorOnStart)
            SetCursorLocked(true);
    }

    private void Update()
    {
        if (IsOwner && IsClient)
            CollectOwnerInput();

        if (IsServer)
            SimulateServerMovement();
    }

    private void CollectOwnerInput()
    {
        HandleLook();

        Vector2 input = Vector2.ClampMagnitude(
            new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")), 1f);
        bool sprinting = Input.GetKey(KeyCode.LeftShift);
        float yaw = transform.eulerAngles.y;

        if (IsServer)
        {
            _serverMoveInput = input;
            _serverSprinting = sprinting;
            _serverYaw = yaw;
            _lastInputReceivedTime = Time.unscaledTime;
        }
        else if (Time.unscaledTime >= _nextInputSendTime)
        {
            _nextInputSendTime = Time.unscaledTime + InputSendInterval;
            SubmitMovementInputRpc(input, sprinting, yaw);
        }

        if (Input.GetKeyDown(KeyCode.Escape))
            SetCursorLocked(false);

        if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked)
            SetCursorLocked(true);
    }

    private void HandleLook()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
            return;

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;
        transform.Rotate(Vector3.up * mouseX);

        _cameraPitch = Mathf.Clamp(_cameraPitch - mouseY, minLookAngle, maxLookAngle);
        if (cameraPivot != null)
            cameraPivot.localRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);
    }

    private void SimulateServerMovement()
    {
        if (Time.unscaledTime - _lastInputReceivedTime > StaleInputTimeout)
        {
            _serverMoveInput = Vector2.zero;
            _serverSprinting = false;
        }

        transform.rotation = Quaternion.Euler(0f, _serverYaw, 0f);
        Vector3 movement = transform.right * _serverMoveInput.x + transform.forward * _serverMoveInput.y;
        float currentSpeed = _serverSprinting ? sprintSpeed : moveSpeed;

        if (_characterController.isGrounded)
            _verticalVelocity = groundedForce;
        else
            _verticalVelocity += gravity * Time.deltaTime;

        movement *= Mathf.Max(0f, currentSpeed);
        movement.y = _verticalVelocity;
        _characterController.Move(movement * Time.deltaTime);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner, Delivery = RpcDelivery.Unreliable)]
    private void SubmitMovementInputRpc(Vector2 moveInput, bool sprinting, float requestedYaw, RpcParams rpcParams = default)
    {
        if (!IsServer || !IsSpawned || !IsFinite(moveInput.x) || !IsFinite(moveInput.y) || !IsFinite(requestedYaw))
            return;

        if (Time.unscaledTime < _nextServerInputAcceptTime || moveInput.sqrMagnitude > 1.21f)
            return;

        _nextServerInputAcceptTime = Time.unscaledTime + InputSendInterval;

        float elapsed = Mathf.Clamp(Time.unscaledTime - _lastInputReceivedTime, 0.01f, 0.25f);
        float yawDelta = Mathf.DeltaAngle(_serverYaw, Mathf.Repeat(requestedYaw, 360f));
        float maximumYawDelta = MaximumTurnRate * elapsed;
        _serverYaw = Mathf.Repeat(_serverYaw + Mathf.Clamp(yawDelta, -maximumYawDelta, maximumYawDelta), 360f);
        _serverMoveInput = Vector2.ClampMagnitude(moveInput, 1f);
        _serverSprinting = sprinting;
        _lastInputReceivedTime = Time.unscaledTime;
    }

    private void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
