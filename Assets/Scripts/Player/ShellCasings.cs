using UnityEngine;

// Spent shells flying out of the guns (PlayerWeapon, every shot on every machine): little brass pixels that pop up
// and back, spin, bounce on the ground and fade. One shared particle system for all the guns, made on first use.
public static class ShellCasings
{
    private static ParticleSystem system;

    // Backward = the way out of the gun (opposite to where it shoots).
    public static void Eject(Vector2 position, Vector2 backward, Material material, Color color)
    {
        if (material == null)
            return;
        if (system == null)
            Create(material);
        var shell = new ParticleSystem.EmitParams
        {
            position = position,
            velocity = backward.normalized * Random.Range(1.5f, 3f) + Vector2.up * Random.Range(3f, 5f),
            startColor = color,
            startSize = 0.09f,
            startLifetime = Random.Range(0.9f, 1.3f),
            rotation = Random.Range(0f, 360f),
            angularVelocity = Random.Range(-900f, 900f),
            applyShapeToPosition = false,
        };
        system.Emit(shell, 1);
    }

    private static void Create(Material material)
    {
        var holder = new GameObject("ShellCasings");
        system = holder.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = system.main;
        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 2.5f;
        main.maxParticles = 300;
        main.startSpeed = 0f;

        ParticleSystem.EmissionModule emission = system.emission;
        emission.enabled = false;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.enabled = false;

        // Bounce on the level (not on the players).
        ParticleSystem.CollisionModule collision = system.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision2D;
        collision.bounce = 0.35f;
        collision.dampen = 0.4f;
        collision.collidesWith = ~LayerMask.GetMask("Player");

        // Fade out over the last part of their life.
        ParticleSystem.ColorOverLifetimeModule fade = system.colorOverLifetime;
        fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.7f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;

        var renderer = holder.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingOrder = 5;
        system.Play();
    }
}
