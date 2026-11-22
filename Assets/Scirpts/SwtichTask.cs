using UnityEngine;

public class SwitchTask : MonoBehaviour
{
    public GameObject up;
    public GameObject on;
    public bool isOn;
    public bool isUp;

    private void Start()
    {
        on.SetActive(isOn);
        up.SetActive(isUp);

        if (isOn)
            SwitchCount.instance.SwitchChange(1);
    }

    private void OnMouseUp()
    {
        isUp = !isUp;
        isOn = !isOn;

        on.SetActive(isOn);
        up.SetActive(isUp);

        SwitchCount.instance.SwitchChange(isOn ? 1 : -1);
    }
}