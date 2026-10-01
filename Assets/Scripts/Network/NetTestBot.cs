using UnityEngine;
using UnityEngine.UI;

// Test helper for LAN builds (command line -bot): drives this machine's player by itself (runs left and right,
// jumps, shoots at the nearest other player, picks the first card when one is offered) and logs every player's
// position and health once a second, so a session can be checked without anyone at the keyboard.
// MainMenu adds it.
public class NetTestBot : MonoBehaviour
{
    private float logTimer;
    private float shootTimer;

    private void Update()
    {
        PlayerNetwork local = PlayerNetwork.Local;
        if (local == null)
            return;

        var movement = local.GetComponent<PlayerMovement>();
        if (movement.enabled)
            movement.enabled = false; // the bot moves the body itself

        var rb = local.GetComponent<Rigidbody2D>();
        if (rb.simulated)
        {
            float vy = rb.linearVelocity.y;
            if (Mathf.Repeat(Time.time, 2f) < Time.deltaTime && Mathf.Abs(vy) < 0.1f)
                vy = 14f; // hop every 2 seconds
            rb.linearVelocity = new Vector2(Mathf.Sin(Time.time * 1.5f) * 3f, vy);
        }

        // Shoot at the nearest other player.
        PlayerNetwork aimAt = null;
        foreach (PlayerNetwork player in PlayerNetwork.All)
            if (player != local && player.InPlay && (aimAt == null || Vector2.Distance(player.transform.position, local.transform.position) <
                                                                      Vector2.Distance(aimAt.transform.position, local.transform.position)))
                aimAt = player;
        Transform pivot = local.transform.Find("GunPivot");
        if (aimAt != null)
        {
            Vector2 to = aimAt.transform.position - pivot.position;
            pivot.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(to.y, to.x) * Mathf.Rad2Deg);
            shootTimer -= Time.deltaTime;
            if (shootTimer <= 0f && local.InPlay)
            {
                shootTimer = 0.25f;
                Transform firePoint = pivot.Find("FirePoint");
                local.Cores.FireShot(local.GetComponent<PlayerWeapon>().ProjectilePrefab, firePoint.position, firePoint.rotation,
                                     30f, 10f, 2f, rb);
            }
        }

        // A card offer (after dying): take the first card.
        GameObject cards = GameObject.Find("HUD/CoreCards/Panel");
        if (cards != null && cards.activeInHierarchy)
        {
            Button first = cards.transform.Find("Card0").GetComponent<Button>();
            first.onClick.Invoke();
            Debug.Log("[BOT] picked a card, cores now " + local.Cores.OwnedCount);
        }

        logTimer -= Time.deltaTime;
        if (logTimer <= 0f)
        {
            logTimer = 1f;
            var line = new System.Text.StringBuilder("[BOT] t=" + Time.time.ToString("F1") + " players=" + PlayerNetwork.All.Count);
            foreach (PlayerNetwork player in PlayerNetwork.All)
                line.Append(" | P" + (player.Slot + 1) + (player == local ? "(me)" : "") + " pos=" + player.transform.position.ToString("F1") +
                            " hp=" + player.Health.ToString("F0") + (player.Dead ? " DEAD" : "") + " cores=" + player.Cores.OwnedCount);
            Debug.Log(line.ToString());
        }
    }
}
