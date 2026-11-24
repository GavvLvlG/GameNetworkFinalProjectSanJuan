using UnityEngine;
using UnityEngine.InputSystem;

public class SwitchTask : MonoBehaviour
{
    public GameObject up;
    public GameObject on;
    public bool isOn;
    public bool isUp;

    private RectTransform rectTransform;

    private void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
    }

    private void Start()
    {
        if (on != null)
            on.SetActive(isOn);

        if (up != null)
            up.SetActive(isUp);

        if (isOn && SwitchCount.instance != null)
            SwitchCount.instance.SwitchChange(1);
    }

    private void Update()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null || !mouse.leftButton.wasPressedThisFrame || rectTransform == null)
            return;

        Vector2 pointerPosition = mouse.position.ReadValue();
        if (!RectTransformUtility.RectangleContainsScreenPoint(rectTransform, pointerPosition, null))
            return;

        isUp = !isUp;
        isOn = !isOn;

        if (on != null)
            on.SetActive(isOn);

        if (up != null)
            up.SetActive(isUp);

        if (SwitchCount.instance != null)
            SwitchCount.instance.SwitchChange(isOn ? 1 : -1);
    }
}