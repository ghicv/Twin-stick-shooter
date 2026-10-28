// Every core (augment) the player can get during a run.
// Only the core system (Assets/Scripts/Cores) knows them; the rest of the game talks to it through CoreBridge.
// The catalog (CoreInventory on the Player prefab) stores them as numbers: a new core goes last, and removing one
// means renumbering the catalog's entries after it.
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
    Accelerate,  // starts slow, speeds up, hits harder the faster it goes
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
    BlackHole,   // may leave a black hole that pulls targets in

    // Single cores: stand alone.
    // PLAYER
    GroundPound, // hard landings send out a shockwave
    ExtraJump,   // one more jump in the air
    Dash,        // faster dashes, can't be hurt meanwhile
    ThickSkin,   // more max health
    ExtraLife,   // come back once after dying
    Orbit,       // flying swords circle the player and strike nearby targets
    // GUN
    HotBarrel,   // holding fire speeds the gun up
    AirGunner,   // more damage in the air
    Adrenaline,  // faster fire at low health
    // TARGETS
    VoidFeast,   // targets knocked into the void heal the player and bring the dash back
    DeathBlast,  // killed targets explode
    Vampire,     // damage dealt heals the player
    // GUN
    DrawSword,   // the gun becomes a sword: melee slashes that cut bullets, each sending out a sword wave
    // BULLET
    Fan,         // SPAWN: two more bullets fanned out to the sides
    Wings,       // SPAWN: two small bullets out to the sides, at right angles
    Helix,       // SPAWN: every bullet becomes two twisting around each other
    Wave,        // FLY: weaves up and down
    Zigzag,      // FLY: zigzags
    Split,       // FLY: splits into three after a moment
    Cross,       // HIT: every hit bursts into four rays in a + shape
    // GUN
    Gatling,     // much faster fire, a bit more spread
}
