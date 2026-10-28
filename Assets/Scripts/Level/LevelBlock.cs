using UnityEngine;

// Settings of one block of the level (floor, wall, platform).
// Bullets Pass Through: bullets (the player's and the enemies') fly through the block, and it doesn't
// block a ranged enemy's view. Turn it on for thin platforms/walls; thick ground and walls keep it off.
// Slippery: ice. Players standing on it speed up and stop slowly (PlayerMovement's Ice Grip).
// Conveyor Speed: a conveyor belt; players standing on it are carried sideways (PlayerMovement).
public class LevelBlock : MonoBehaviour
{
    [Tooltip("On: bullets fly through this block (thin platforms/walls). Off: it stops bullets (thick ground, walls).")]
    [SerializeField] private bool bulletsPassThrough;

    [Tooltip("Ice: players slide on it.")]
    [SerializeField] private bool slippery;

    [Tooltip("Conveyor belt: players on it are carried at this speed (units/sec, + = right, 0 = none).")]
    [SerializeField] private float conveyorSpeed;

    public bool BulletsPassThrough => bulletsPassThrough;
    public bool Slippery => slippery;
    public float ConveyorSpeed => conveyorSpeed;
}
