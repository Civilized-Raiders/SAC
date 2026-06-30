using UnityEngine;

public class RotaryLaser : MonoBehaviour
{
    [SerializeField] private float rotateSpeed = 10f;

    private void Update()
    {
        transform.Rotate(0f, 0f, rotateSpeed * Time.deltaTime, Space.Self);
    }
}
