using UnityEngine;

// A mine a bullet left on a wall or the floor (Mine core, made by BulletCores). It arms after a moment, then blows up
// when an enemy comes close, or by itself when its time runs out. It blinks during its last second.
[RequireComponent(typeof(SpriteRenderer))]
public class CoreMine : MonoBehaviour
{
    private BulletCores owner;
    private SpriteRenderer sprite;
    private float armTimer;
    private float lifeTimer;
    private float triggerRadius;

    public void Setup(BulletCores owner, float armTime, float lifetime, float triggerRadius)
    {
        this.owner = owner;
        sprite = GetComponent<SpriteRenderer>();
        armTimer = armTime;
        lifeTimer = lifetime;
        this.triggerRadius = triggerRadius;
    }

    private void Update()
    {
        armTimer -= Time.deltaTime;
        lifeTimer -= Time.deltaTime;
        sprite.enabled = lifeTimer > 1f || Mathf.Repeat(lifeTimer, 0.16f) < 0.08f;

        if (lifeTimer <= 0f || (armTimer <= 0f && EnemyClose()))
        {
            owner.MineBlast(transform.position);
            Destroy(gameObject);
        }
    }

    private bool EnemyClose()
    {
        foreach (Dummy dummy in Dummy.Active)
            if (!dummy.IsDying && Vector2.Distance(dummy.transform.position, transform.position) < triggerRadius)
                return true;
        return false;
    }
}
