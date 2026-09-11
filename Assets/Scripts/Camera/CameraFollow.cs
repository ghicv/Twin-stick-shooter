using UnityEngine;

// Gently follows the target: the rig moves only part of the way toward it (Follow Amount), smoothly,
// and never further than Max Offset from where it started, so the whole arena stays on screen.
// Put it on the CameraRig; the CameraShake on the camera inside keeps working on top of it.
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;

    [Tooltip("How much of the target's distance from the start position the camera follows (0 = static, 1 = fully).")]
    [Range(0f, 1f)]
    [SerializeField] private float followAmount = 0.2f;

    [Tooltip("The camera never moves further than this from its start position (units, per axis).")]
    [SerializeField] private Vector2 maxOffset = new Vector2(1.5f, 1f);

    [Tooltip("Roughly how long the camera takes to catch up (seconds). Higher = floatier.")]
    [SerializeField] private float smoothTime = 0.3f;

    private Vector3 startPosition;
    private Vector3 velocity;

    private void Awake()
    {
        startPosition = transform.position;
    }

    // LateUpdate: after the player has moved (and been interpolated) this frame.
    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector2 offset = (Vector2)(target.position - startPosition) * followAmount;
        offset.x = Mathf.Clamp(offset.x, -maxOffset.x, maxOffset.x);
        offset.y = Mathf.Clamp(offset.y, -maxOffset.y, maxOffset.y);

        Vector3 desired = startPosition + (Vector3)offset; // keeps the rig's own z
        // Real time, so the camera keeps following during slow motion (e.g. the respawn pull).
        transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
    }
}
