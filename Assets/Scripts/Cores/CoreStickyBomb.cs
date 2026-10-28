using UnityEngine;

// A bomb stuck on a target (Sticky Bomb core, made by BulletCores): it rides along on the target, blinks faster and
// faster, then blows up and throws everyone near away. A look-alike bomb (LAN, not the host) only shows the blast.
[RequireComponent(typeof(SpriteRenderer))]
public class CoreStickyBomb : MonoBehaviour
{
    private BulletCores owner;
    private SpriteRenderer sprite;
    private float delay;
    private float timer;
    private bool lookAlike;

    public void Setup(BulletCores owner, float delay, bool lookAlike)
    {
        this.owner = owner;
        this.delay = delay;
        this.lookAlike = lookAlike;
        sprite = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        timer += Time.deltaTime;
        float blink = Mathf.Lerp(0.18f, 0.03f, timer / delay);
        sprite.enabled = Mathf.Repeat(timer, blink * 2f) < blink;
        if (timer >= delay)
        {
            owner.StickyBlast(transform.position, lookAlike);
            Destroy(gameObject);
        }
    }
}
