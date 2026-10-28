using UnityEngine;

// Lava, spikes...: a player touching this trigger dies at once, like falling off the map (the last player who hit
// them gets the kill). The machine that moves the player notices it and tells the host (PlayerRespawn).
[RequireComponent(typeof(Collider2D))]
public class Hazard : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        PlayerRespawn player = other.GetComponentInParent<PlayerRespawn>();
        if (player != null)
            player.TouchHazard();
    }
}
