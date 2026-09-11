using System.Collections;
using UnityEngine;

// Game speed.
// Freeze() = freeze frame ("hitstop"): game time stops for a moment, then runs again.
// SetSlowMotion() = slow motion (1 = normal speed), e.g. while the player's death plays out.
// A freeze frame always goes back to the current slow-motion speed, so the two never fight.
// Physics steps shrink with the game speed, so slow motion stays smooth.
// It lives on the camera so it keeps running even if whatever asked for it
// (e.g. an exploding dummy) is disabled right away.
public class HitStop : MonoBehaviour
{
    private float normalFixedDeltaTime;
    private float slowMotion = 1f;
    private bool frozen;

    private void Awake()
    {
        normalFixedDeltaTime = Time.fixedDeltaTime;
    }

    public void Freeze(float duration)
    {
        StopAllCoroutines();
        StartCoroutine(FreezeRoutine(duration));
    }

    public void SetSlowMotion(float speed)
    {
        slowMotion = Mathf.Clamp(speed, 0.01f, 1f);
        if (!frozen)
            ApplySpeed(slowMotion);
    }

    private IEnumerator FreezeRoutine(float duration)
    {
        frozen = true;
        Time.timeScale = 0f;
        yield return new WaitForSecondsRealtime(duration); // real time keeps running
        frozen = false;
        ApplySpeed(slowMotion);
    }

    private void ApplySpeed(float speed)
    {
        Time.timeScale = speed;
        Time.fixedDeltaTime = normalFixedDeltaTime * speed; // same number of physics steps per real second
    }

    // Never leave the game slowed down (e.g. leaving Play Mode in the middle of a slow motion).
    private void OnDestroy()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = normalFixedDeltaTime;
    }
}
