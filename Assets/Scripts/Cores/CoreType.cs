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
    Echo,        // every shot fires again a moment later
    // FLY
    Homing,      // steers toward a target ahead
    Bounce,      // bounces off walls
    Pierce,      // flies through targets and crates
    Mortar,      // lobbed high, falls in an arc, explodes where it lands
    Boomerang,   // flies back to the player
    LongShot,    // stronger the farther it flies
    Drill,       // goes through a wall
    Accelerate,  // starts slow, speeds up, hits harder the faster it goes
    Deflect,     // destroys the bullets it touches
    // HIT
    Explosive,   // explodes on every hit
    Hammer,      // much stronger knockback
    Fork,        // splits in two on the first target
    Chain,       // lightning jumps to nearby targets and barrels
    Chill,       // slows targets down
    Ignite,      // sets targets on fire
    Vortex,      // pulls nearby targets in
    Reaper,      // finishes off weak targets
    StickyBomb,  // sticks a bomb on the target that blows it away
    Thunder,     // lightning strikes the target from the sky
    // END
    Shrapnel,    // bursts into shards when it stops
    Mine,        // may leave a mine where it stops on a wall
    BlackHole,   // may leave a black hole that pulls targets in

    // Single cores: stand alone.
    // PLAYER
    GroundPound, // hard landings send out a shockwave
    ExtraJump,   // one more jump in the air
    Dash,        // faster dashes, can't be hurt meanwhile
    ThickSkin,   // more max health
    ExtraLife,   // come back once after dying
    Orbit,       // two orbs circle the player, blocking bullets
    Anchor,      // pushed around much less
    Medic,       // health packs heal more and protect
    // GUN
    HotBarrel,   // holding fire speeds the gun up
    SteadyAim,   // almost no spread
    AirGunner,   // more damage in the air
    Adrenaline,  // faster fire at low health
    Opener,      // first shot after a pause hits hard
    Demolition,  // barrels go off at once, crates fly far
    GunNut,      // special guns come with more ammo
    // TARGETS
    VoidFeast,   // targets knocked into the void heal the player and bring the dash back
    DeathBlast,  // killed targets explode
    Crash,       // targets knocked into walls/each other get hurt
    Vampire,     // damage dealt heals the player
    // GUN (added later: new values go last, the catalog stores them as numbers)
    DrawSword,   // the gun becomes a sword: melee slashes that cut bullets
}
