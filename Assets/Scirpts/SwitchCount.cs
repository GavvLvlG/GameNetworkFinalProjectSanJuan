using UnityEngine;

public class SwitchCount : MonoBehaviour
{
    public static SwitchCount instance;

    public int switchCount;
    public GameObject wintext;

    private int onCount;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        if (wintext != null)
            wintext.SetActive(false);
    }

    public void SwitchChange(int points)
    {
        onCount += points;

        if (onCount >= switchCount && wintext != null)
            wintext.SetActive(true);
        else if (wintext != null)
            wintext.SetActive(false);
    }
}