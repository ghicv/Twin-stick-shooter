using UnityEngine;

// Gently follows the players: the rig moves only part of the way toward them (Follow Amount), smoothly,
// and never further than Max Offset from where it started, so the whole arena stays on screen.
// With more than one player (LAN) the camera also zooms out so every player still in play is on screen.
// A map can ask for a bigger picture (LevelMap.CameraSize): the big LAN maps are shown whole.
// Put it on the CameraRig; the CameraShake on the camera inside keeps working on top of it.
public class CameraFollow : MonoBehaviour
{
    [Tooltip("Followed when there are no networked players. Optional.")]
    [SerializeField] private Transform target;

    [Tooltip("How much of the players' distance from the start position the camera follows (0 = static, 1 = fully).")]
    [Range(0f, 1f)]
    [SerializeField] private float followAmount = 0.2f;

    [Tooltip("The camera never moves further than this from its start position (units, per axis).")]
    [SerializeField] private Vector2 maxOffset = new Vector2(1.5f, 1f);

    [Tooltip("Roughly how long the camera takes to catch up (seconds). Higher = floatier.")]
    [SerializeField] private float smoothTime = 0.3f;

    [Header("Zoom (more than one player)")]
    [Tooltip("Players are kept at least this far inside the screen edge (units).")]
    [SerializeField] private float zoomMargin = 2f;

    [Tooltip("The camera never zooms out further than this (orthographic size).")]
    [SerializeField] private float maxSize = 11f;

    [SerializeField] private float zoomSmoothTime = 0.4f;

    private Camera cam;
    private MapManager maps;
    private Vector3 startPosition;
    private float baseSize;
    private Vector3 velocity;
    private float zoomVelocity;

    private void Awake()
    {
        cam = GetComponentInChildren<Camera>();
        startPosition = transform.position;
        baseSize = cam.orthographicSize;
    }

    // LateUpdate: after the players have moved (and been interpolated) this frame.
    private void LateUpdate()
    {
        // Who to keep on screen: the players still in play, or the fallback target.
        Vector2 min = Vector2.positiveInfinity;
        Vector2 max = Vector2.negativeInfinity;
        int count = 0;
        foreach (PlayerNetwork player in PlayerNetwork.All)
        {
            if (!player.InPlay)
                continue;
            min = Vector2.Min(min, player.transform.position);
            max = Vector2.Max(max, player.transform.position);
            count++;
        }
        if (count == 0 && target != null)
        {
            min = max = target.position;
            count = 1;
        }
        if (count == 0)
            return;

        Vector2 center = (min + max) * 0.5f;
        Vector2 offset = (center - (Vector2)startPosition) * followAmount;
        offset.x = Mathf.Clamp(offset.x, -maxOffset.x, maxOffset.x);
        offset.y = Mathf.Clamp(offset.y, -maxOffset.y, maxOffset.y);

        Vector3 desired = startPosition + (Vector3)offset; // keeps the rig's own z
        // Real time, so the camera keeps following during slow motion (e.g. the respawn pull).
        transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);

        // Never smaller than the map asks for (big LAN maps show whole); zoom out just enough to see everyone
        // (single player keeps that size).
        if (maps == null)
            maps = FindAnyObjectByType<MapManager>();
        float mapSize = maps != null ? Mathf.Max(baseSize, maps.CurrentMap.CameraSize) : baseSize;
        float size = mapSize;
        if (count > 1)
        {
            float halfHeight = Mathf.Max(Mathf.Abs(max.y - desired.y), Mathf.Abs(min.y - desired.y)) + zoomMargin;
            float halfWidth = Mathf.Max(Mathf.Abs(max.x - desired.x), Mathf.Abs(min.x - desired.x)) + zoomMargin;
            size = Mathf.Clamp(Mathf.Max(halfHeight, halfWidth / cam.aspect), mapSize, Mathf.Max(maxSize, mapSize));
        }
        cam.orthographicSize = Mathf.SmoothDamp(cam.orthographicSize, size, ref zoomVelocity, zoomSmoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
    }
}
