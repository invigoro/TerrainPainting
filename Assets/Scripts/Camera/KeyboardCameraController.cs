using UnityEngine;

public class KeyboardCameraController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 10f;

    [Header("Rotation")]
    [SerializeField] private float rotationSpeed = 90f; // degrees per second

    private void Update()
    {
        HandleMovement();
        HandleRotation();
    }

    private void HandleMovement()
    {
        Vector3 movement = Vector3.zero;

        if (Input.GetKey(KeyCode.W))
            movement += Vector3.forward;

        if (Input.GetKey(KeyCode.S))
            movement += Vector3.back;

        if (Input.GetKey(KeyCode.A))
            movement += Vector3.left;

        if (Input.GetKey(KeyCode.D))
            movement += Vector3.right;

        if (movement.sqrMagnitude > 0f)
        {
            movement.Normalize();
            transform.Translate(movement * moveSpeed * Time.deltaTime, Space.Self);
        }
    }

    private void HandleRotation()
    {
        float rotationInput = 0f;

        if (Input.GetKey(KeyCode.Q))
            rotationInput += 1f;

        if (Input.GetKey(KeyCode.E))
            rotationInput -= 1f;

        if (rotationInput != 0f)
        {
            float rotationAmount = rotationInput * rotationSpeed * Time.deltaTime;
            transform.Rotate(Vector3.up, rotationAmount, Space.World);
        }
    }
}
