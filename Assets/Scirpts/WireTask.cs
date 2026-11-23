using UnityEngine;

public class WireTask : MonoBehaviour
{
    public SpriteRenderer wireEnd;
    public GameObject lightOn;

    Vector3 startPoint;
    Vector3 startPosition;

    void Start()
    {
        startPoint = transform.parent.position;
        startPosition = transform.position;
    }

    private void OnMouseDrag()
    {
        Vector3 newPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        newPosition.z = 0;

        Collider2D[] colliders = Physics2D.OverlapCircleAll(newPosition, 0.2f);
        foreach (Collider2D collider in colliders)
        {
            if (collider.gameObject != gameObject)
            {
                UpdateWire(collider.transform.position);

                if (transform.parent.name == collider.transform.parent.name)
                {
                    SwitchCount.instance.SwitchChange(1);
                    collider.GetComponent<WireTask>()?.Done();
                    Done();
                }
                return;
            }
        }

        UpdateWire(newPosition);
    }

    void Done()
    {
        lightOn.SetActive(true);
        Destroy(this);
    }

    private void OnMouseUp()
    {
        UpdateWire(startPosition);
    }

    void UpdateWire(Vector3 newPosition)
    {
        transform.position = newPosition;
        Vector3 direction = newPosition - startPoint;
        transform.right = direction;

        float dist = Vector2.Distance(newPosition, startPoint);
        wireEnd.size = new Vector2(dist, wireEnd.size.y);
    }
}