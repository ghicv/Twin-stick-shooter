using UnityEngine;

// A mine a bullet left on a wall or the floor (Mine core, made by BulletCores). It arms after a moment, then blows up
// when a target (an enemy, or another player) comes close, or by itself when its time runs out. It blinks during its
// last second. A look-alike mine (LAN, not the host) blows up the same way but only shows the blast.
[RequireComponent(typeof(SpriteRenderer))]
public class CoreMine : MonoBehaviour
{
    private BulletCores owner;
    private HitTarget ownerTarget;
    private SpriteRenderer sprite;
    private float armTimer;
    private float lifeTimer;
    private float triggerRadius;
    private bool lookAlike;

    public void Setup(BulletCores owner, HitTarget ownerTarget, float armTime, float lifetime, float triggerRadius, bool lookAlike)
    {
        this.owner = owner;
        this.ownerTarget = ownerTarget;
        sprite = GetComponent<SpriteRenderer>();
        armTimer = armTime;
        lifeTimer = lifetime;
        this.triggerRadius = triggerRadius;
        this.lookAlike = lookAlike;
    }

    private void Update()
    {
        armTimer -= Time.deltaTime;
        lifeTimer -= Time.deltaTime;
        sprite.enabled = lifeTimer > 1f || Mathf.Repeat(lifeTimer, 0.16f) < 0.08f;

        if (lifeTimer <= 0f || (armTimer <= 0f && TargetClose()))
        {
            owner.MineBlast(transform.position, lookAlike);
            Destroy(gameObject);
        }
    }

    private bool TargetClose()
    {
        foreach (HitTarget target in HitTarget.Active)
            if (target != ownerTarget && !target.IsDying && Vector2.Distance(target.transform.position, transform.position) < triggerRadius)
                return true;
        return false;
    }
}
