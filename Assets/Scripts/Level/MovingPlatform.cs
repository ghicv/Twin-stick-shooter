using UnityEngine;

// A platform that swings back and forth around where it was placed (Travel = how far to each side) and/or spins.
// It follows the MapClock, so it is in the same place on every machine. It is a kinematic body driven by its
// velocity, so players and crates standing on it are carried along (PlayerMovement reads that velocity).
[RequireComponent(typeof(Rigidbody2D))]
public class MovingPlatform : MonoBehaviour
{
    [Tooltip("How far it goes to each side of its start (units). (0, 0) = it stays.")]
    [SerializeField] private Vector2 travel = new Vector2(3f, 0f);

    [Tooltip("Seconds for one full trip there and back.")]
    [Min(0.1f)]
    [SerializeField] private float period = 5f;

    [Tooltip("Where in the trip it starts (0..1). Two platforms half a trip apart move as mirror images.")]
    [Range(0f, 1f)]
    [SerializeField] private float phase;

    [Tooltip("Spin speed (degrees/sec, 0 = no spin).")]
    [SerializeField] private float spinSpeed;

    private Rigidbody2D body;
    private Vector2 start;

    private void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        start = transform.position;
    }

    // The map was just turned on: jump to where the platform is now.
    private void OnEnable()
    {
        float time = MapClock.Time;
        body.position = PositionAt(time);
        body.rotation = spinSpeed * time;
    }

    // Steered by velocity (not teleported), so whatever stands on it moves with it.
    private void FixedUpdate()
    {
        float time = MapClock.Time + Time.fixedDeltaTime; // where it must be at the end of this step
        body.linearVelocity = (PositionAt(time) - body.position) / Time.fixedDeltaTime;
        body.angularVelocity = Mathf.DeltaAngle(body.rotation, spinSpeed * time) / Time.fixedDeltaTime;
    }

    private Vector2 PositionAt(float time) => start + travel * Mathf.Sin((time / period + phase) * Mathf.PI * 2f);
}
