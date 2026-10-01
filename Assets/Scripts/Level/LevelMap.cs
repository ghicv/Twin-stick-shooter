using UnityEngine;

// One map: the level blocks under it, plus the spots where enemies appear and where players arrive.
// MapManager turns one map on at a time.
public class LevelMap : MonoBehaviour
{
    [Tooltip("Shown when the player arrives on this map.")]
    [SerializeField] private string displayName = "MAP";

    [Tooltip("Enemies appear here and players arrive at them. Keep them a bit above the surfaces. LAN rounds give " +
             "player slot N the spot N * count / 4, so list the 4 player starts first, spread out.")]
    [SerializeField] private Transform[] spawnPoints;

    [Tooltip("A LAN round map (big, for up to 4 players). Off = a single player wave map.")]
    [SerializeField] private bool pvp;

    [Tooltip("The camera never shows less than this on this map (orthographic size; 0 = the camera's own size).")]
    [SerializeField] private float cameraSize;

    [Tooltip("Players falling below this height (world Y) die.")]
    [SerializeField] private float fallLimitY = -8f;

    public string DisplayName => displayName;
    public Transform[] SpawnPoints => spawnPoints;
    public bool Pvp => pvp;
    public float CameraSize => cameraSize;
    public float FallLimitY => fallLimitY;
}
