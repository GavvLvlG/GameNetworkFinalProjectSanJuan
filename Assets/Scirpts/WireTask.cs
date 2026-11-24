using UnityEngine;
using UnityEngine.InputSystem;

public class WireTask : MonoBehaviour
{
    public SpriteRenderer wireEnd;
    public GameObject lightOn;

    private Transform wireInTransform;
    private SpriteRenderer wireInRenderer;
    private Collider2D wireCollider;
    private Vector3 startPoint;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private Vector3 wireEndStartLocalPosition;
    private Vector3 wireInStartLocalPosition;
    private Vector2 wireEndStartSize;
    private float copperLength;
    private float localRightSign;
    private bool isDragging;
    private bool isConnected;

    private void Start()
    {
        if (transform.parent == null || wireEnd == null)
        {
            Debug.LogError($"{nameof(WireTask)} requires a parent transform and a Wire_end SpriteRenderer.", this);
            enabled = false;
            return;
        }

        wireCollider = GetComponent<Collider2D>();
        wireInTransform = wireEnd.transform.parent.Find("Wire_in");
        wireInRenderer = wireInTransform != null ? wireInTransform.GetComponent<SpriteRenderer>() : null;
        if (wireCollider == null || wireInTransform == null || wireInRenderer == null || wireInRenderer.sprite == null)
        {
            Debug.LogError($"{nameof(WireTask)} requires a Collider2D and a valid Wire_in SpriteRenderer.", this);
            enabled = false;
            return;
        }

        startPoint = transform.parent.position;
        startPosition = transform.position;
        startRotation = transform.rotation;
        localRightSign = transform.lossyScale.x < 0f ? -1f : 1f;
        copperLength = wireInRenderer.sprite.bounds.size.x;
        wireEndStartLocalPosition = wireEnd.transform.localPosition;
        wireInStartLocalPosition = wireInTransform.localPosition;
        wireEndStartSize = wireEnd.size;
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        Camera mainCamera = Camera.main;
        if (mouse == null || mainCamera == null || isConnected)
            return;

        Vector2 screenPosition = mouse.position.ReadValue();
        float cameraDistance = transform.position.z - mainCamera.transform.position.z;
        Vector3 pointerPosition = mainCamera.ScreenToWorldPoint(
            new Vector3(screenPosition.x, screenPosition.y, cameraDistance));
        pointerPosition.z = transform.position.z;

        if (!isDragging && mouse.leftButton.wasPressedThisFrame && wireCollider.OverlapPoint(pointerPosition))
            isDragging = true;

        if (isDragging && mouse.leftButton.isPressed)
            DragWire(pointerPosition);

        if (isDragging && mouse.leftButton.wasReleasedThisFrame)
        {
            isDragging = false;
            if (!isConnected)
                RetractWire();
        }
    }

    private void DragWire(Vector3 pointerPosition)
    {
        const float connectionRadius = 0.25f;
        Collider2D[] colliders = Physics2D.OverlapCircleAll(pointerPosition, connectionRadius);

        foreach (Collider2D collider in colliders)
        {
            if (collider == wireCollider)
                continue;

            WireTask targetWire = collider.GetComponent<WireTask>();
            if (targetWire == null || targetWire == this || targetWire.isConnected)
                continue;

            Vector3 targetPosition = collider.bounds.center;
            UpdateWire(targetPosition);

            if (transform.parent != null && targetWire.transform.parent != null &&
                transform.parent.name == targetWire.transform.parent.name)
            {
                if (SwitchCount.instance != null)
                    SwitchCount.instance.SwitchChange(1);

                targetWire.CompleteConnection();
                CompleteConnection();
            }

            return;
        }

        UpdateWire(pointerPosition);
    }

    private void UpdateWire(Vector3 endpoint)
    {
        Vector3 direction = endpoint - startPoint;
        float distance = direction.magnitude;
        if (distance <= 0.001f)
            return;

        direction /= distance;
        transform.position = endpoint;
        transform.right = direction * localRightSign;

        float bodyLength = Mathf.Max(wireEndStartSize.x, distance - copperLength);
        Vector3 endLocalPosition = wireEndStartLocalPosition;
        endLocalPosition.x = -(distance + copperLength) * 0.5f;
        wireEnd.transform.localPosition = endLocalPosition;
        wireEnd.size = new Vector2(bodyLength, wireEndStartSize.y);

        Vector3 copperLocalPosition = wireInStartLocalPosition;
        copperLocalPosition.x = -copperLength * 0.5f;
        wireInTransform.localPosition = copperLocalPosition;
    }

    private void RetractWire()
    {
        transform.position = startPosition;
        transform.rotation = startRotation;
        wireEnd.transform.localPosition = wireEndStartLocalPosition;
        wireEnd.size = wireEndStartSize;
        wireInTransform.localPosition = wireInStartLocalPosition;
    }

    private void CompleteConnection()
    {
        if (isConnected)
            return;

        isConnected = true;
        if (lightOn != null)
            lightOn.SetActive(true);

        wireCollider.enabled = false;
        Destroy(this);
    }
}
