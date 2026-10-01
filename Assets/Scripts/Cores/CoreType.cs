// Every core (augment) the player can get during a run.
// Only the core system (Assets/Scripts/Cores) knows them; the rest of the game talks to it through CoreBridge.
public enum CoreType
{
    // Bullet cores: change the player's bullets, stage by stage (spawn → fly → hit → end), and stack with each other.
    // SPAWN
    ExtraBullet, // one more bullet per shot
    BackShot,    // every shot also fires backwards
    LuckyShot,   // some bullets are critical hits
    Nova,        // every few shots, a ring of bullets
    Giant,       // big, slow, strong bullets
    // FLY
    Homing,      // steers toward an enemy ahead
    Bounce,      // bounces off walls
    Pierce,      // flies through enemies
    Lob,         // falls in an arc
    Wave,        // wiggles up and down
    Boomerang,   // flies back to the player
    LongShot,    // stronger the farther it flies
    Drill,       // goes through a wall
    // HIT
    Explosive,   // explodes on every hit
    Hammer,      // much stronger knockback
    Fork,        // splits in two on the first enemy
    Chain,       // lightning jumps to nearby enemies
    Chill,       // slows enemies down
    Ignite,      // sets enemies on fire
    Vortex,      // pulls nearby enemies in
    Reaper,      // finishes off weak enemies
    // END
    Shrapnel,    // bursts into shards when it stops
    Mine,        // may leave a mine where it stops on a wall

    // Single cores: stand alone.
    // PLAYER
    GroundPound, // hard landings send out a shockwave
    ExtraJump,   // one more jump in the air
    Dash,        // Shift dashes, can't be hurt meanwhile
    ThickSkin,   // more max health
    ExtraLife,   // come back once after dying
    Regeneration,// health comes back while not hit
    // GUN
    HotBarrel,   // holding fire speeds the gun up
    SteadyAim,   // almost no spread
    AirGunner,   // more damage in the air
    Adrenaline,  // faster fire at low health
    Opener,      // first shot after a pause hits hard
    // ENEMY
    VoidFeast,   // enemies falling into the void heal the player
    DeathBlast,  // killed enemies explode
    Crash,       // enemies knocked into walls/each other get hurt
    Vampire,     // kills heal the player
}
