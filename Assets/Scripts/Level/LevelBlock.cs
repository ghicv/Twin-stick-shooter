using UnityEngine;

// Settings of one block of the level (floor, wall, platform).
// Bullets Pass Through: bullets (the player's and the enemies') fly through the block, and it doesn't
// block a ranged enemy's view. Turn it on for thin platforms/walls; thick ground and walls keep it off.
public class LevelBlock : MonoBehaviour
{
    [Tooltip("On: bullets fly through this block (thin platforms/walls). Off: it stops bullets (thick ground, walls).")]
    [SerializeField] private bool bulletsPassThrough;

    public bool BulletsPassThrough => bulletsPassThrough;
}
