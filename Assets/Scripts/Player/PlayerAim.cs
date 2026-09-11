using UnityEngine;
using UnityEngine.InputSystem;

// Rotates the gun pivot so the gun always points at the mouse (full 360°).
// Aim is independent from movement: the player can run right while aiming left.
// Runs before PlayerWeapon (default order 0) so a shot always uses this frame's aim.
[DefaultExecutionOrder(-10)]
public class PlayerAim : MonoBehaviour
{
    [Tooltip("Rotates toward the mouse. The gun and the fire point are children of it.")]
    [SerializeField] private Transform gunPivot;

    private Camera mainCamera;

    private void Awake()
    {
        mainCamera = Camera.main;
    }

    private void Update()
    {
        if (Mouse.current == null)
            return;

        // Mouse position on screen → point on the gameplay plane (z = 0).
        Vector3 mouseScreen = Mouse.current.position.ReadValue();
        mouseScreen.z = -mainCamera.transform.position.z;
        Vector2 mouseWorld = mainCamera.ScreenToWorldPoint(mouseScreen);

        Vector2 toMouse = mouseWorld - (Vector2)gunPivot.position;
        if (toMouse.sqrMagnitude < 0.0001f)
            return; // mouse is right on the pivot: keep the last direction

        float angle = Mathf.Atan2(toMouse.y, toMouse.x) * Mathf.Rad2Deg;
        gunPivot.rotation = Quaternion.Euler(0f, 0f, angle);
    }
}
