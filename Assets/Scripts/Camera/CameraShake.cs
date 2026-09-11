using UnityEngine;

// Small screen shake. Shake() jiggles the camera around its local start position and fades out.
// The camera sits under a parent ("CameraRig"): move the rig to follow the action,
// this script only offsets the camera inside the rig, so the two never fight.
public class CameraShake : MonoBehaviour
{
    private Vector3 restPosition;
    private float strength;
    private float duration;
    private float timeLeft;

    private void Awake()
    {
        restPosition = transform.localPosition;
    }

    public void Shake(float strength, float duration)
    {
        // A weaker shake doesn't cut short a stronger one that is still going.
        float remaining = timeLeft > 0f ? this.strength * (timeLeft / this.duration) : 0f;
        if (strength < remaining)
            return;

        this.strength = strength;
        this.duration = duration;
        timeLeft = duration;
    }

    private void LateUpdate()
    {
        if (timeLeft <= 0f)
            return;

        timeLeft -= Time.unscaledDeltaTime; // real time: keeps shaking through freeze frames and slow motion
        if (timeLeft <= 0f)
        {
            transform.localPosition = restPosition;
            return;
        }

        float fade = timeLeft / duration; // 1 → 0
        transform.localPosition = restPosition + (Vector3)(Random.insideUnitCircle * strength * fade);
    }
}
