using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Builds the LAN round maps from text layouts (Tools > Rebuild LAN Maps), replacing every old round map; the single
// player maps and the lobby stay. A map is 28 x 16 cells, one cell = one unit: exactly what the camera shows (size 8),
// so the whole arena is always on screen. Each map has a theme, whose pixel art (ground, platforms, hazards,
// decorations, background) is painted here and saved under Assets/Sprites/Maps/<Theme>.
//
// Layout characters (row 0 is the top of the screen):
//   .  empty             #  ground (stops bullets)      %  ice (ground, players slide on it)
//   =  thin platform (one-way: jump up through it, S to drop down; bullets fly through)
//   L  lava (kills)      ^  spikes (kill)
//   < >  conveyor belt (ground that carries players left / right)
//   G H  ghost platforms (come and go; the G ones and the H ones take turns)
//   M N O R ...  a moving platform, as the layout's Motions say (one-way; a spinning one is a solid bar)
//   X  a crate (the host spawns it each time the map starts; bullets knock it around)
//   1 2 3 4  player starts (slot order; 1 and 2 far apart)
//   a b c  the theme's decorations, standing on what is below them (no collision)
// Falling off the bottom or the sides kills.
public static class MapBuilder
{
    private const int Columns = 28;
    private const int Rows = 16;
    private const float CameraSize = Rows * 0.5f;
    private const float FallLimit = -10f;
    private const float ConveyorSpeed = 2.5f;
    private const int SkylineWidth = 224; // units: the background reaches far past the screen
    private const string CratePrefabPath = "Assets/Prefabs/Crate.prefab";
    private const int Ppu = 16;
    private const string ArtRoot = "Assets/Sprites/Maps";
    private const string UnlitMaterial = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

    // Draw order, back to front.
    private const int SkyOrder = -30;
    private const int SkylineOrder = -29;
    private const int DecorOrder = -6;
    private const int LavaOrder = -4;
    private const int BodyOrder = -3;
    private const int TopOrder = -2;

    // ---------- Themes ----------

    private enum TopStyle { Plain, Stripes }
    private enum Skyline { Pines, Dunes, City, Hills, Volcano, Peaks }
    private enum DecorKind { Tree, Pine, Bush, Lantern, Cactus, Skull, DeadTree, Tombstone, Candle, Pipe, Lamp, Sign, Rock, Crystal, Snowman }

    // Every theme is a handful of flat colors, like the rest of the game (retro, the CRT style): the ground is the
    // stone tile with a light top, the background one dark color with one slightly lighter skyline, the decorations
    // dark too, so the players and the bullets stand out.
    private class Theme
    {
        public string name;
        public Color color;           // its maps' color on the map roulette
        public Color background, skylineColor;
        public Skyline skyline;
        public bool stars;
        public Color moon;            // alpha 0 = none
        public Color[] ground;        // light edge, fill, dark edge, speck
        public TopStyle topStyle;
        public Color[] top;           // light, mid, dark
        public Color[] plank;         // light, mid, dark
        public DecorKind[] decor;     // a, b, c
        public Color[] decorColors;   // dark, mid, light, accent (lights and flames keep it)
    }

    private static Color H(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color color);
        return color;
    }

    private static readonly Color None = new Color(0f, 0f, 0f, 0f);
    private static readonly Color[] IceGround = { H("#a8e0ff"), H("#78c0ec"), H("#3c7cb4"), H("#9ad4f8") };
    private static readonly Color[] IceTop = { H("#ffffff"), H("#e0f8ff"), H("#b0e4ff") };
    private static readonly Color[] Lava = { H("#a02010"), H("#e0480c"), H("#ff9020"), H("#ffd040") };
    private static readonly Color[] Metal = { H("#2a2e38"), H("#8a94a6"), H("#d0d8e4") }; // dark, mid, light

    private static readonly Theme Woods = new Theme
    {
        name = "Woods", color = H("#5cc84a"),
        background = H("#0e1612"), skylineColor = H("#16241c"), skyline = Skyline.Pines, moon = None,
        ground = new[] { H("#4a3a30"), H("#3a2c24"), H("#241a16"), H("#30241d") },
        topStyle = TopStyle.Plain, top = new[] { H("#7ccf52"), H("#5aa83e"), H("#3f7a2e") },
        plank = new[] { H("#a0703c"), H("#7a5228"), H("#4e3418") },
        decor = new[] { DecorKind.Tree, DecorKind.Bush, DecorKind.Lantern },
        decorColors = new[] { H("#16281c"), H("#22402a"), H("#33603c"), H("#ffd060") },
    };

    private static readonly Theme Desert = new Theme
    {
        name = "Desert", color = H("#f0a040"),
        background = H("#1a120e"), skylineColor = H("#2a1d15"), skyline = Skyline.Dunes, stars = true, moon = H("#e8d8b0"),
        ground = new[] { H("#a86c40"), H("#8a5530"), H("#54301a"), H("#76482a") },
        topStyle = TopStyle.Plain, top = new[] { H("#f0d088"), H("#d8b468"), H("#b08a48") },
        plank = new[] { H("#c08a54"), H("#946638"), H("#5a3c20") },
        decor = new[] { DecorKind.Cactus, DecorKind.Skull, DecorKind.DeadTree },
        decorColors = new[] { H("#2a1e16"), H("#3e2c20"), H("#5a4230"), None },
    };

    private static readonly Theme Factory = new Theme
    {
        name = "Factory", color = H("#f0d020"),
        background = H("#0e1016"), skylineColor = H("#181b24"), skyline = Skyline.City, stars = true, moon = None,
        ground = new[] { H("#4e5668"), H("#3c4352"), H("#22262f"), H("#343a47") },
        topStyle = TopStyle.Stripes, top = new[] { H("#ffe070"), H("#f0c020"), H("#22242a") },
        plank = new[] { H("#8a94a6"), H("#5e6676"), H("#353a44") },
        decor = new[] { DecorKind.Pipe, DecorKind.Lamp, DecorKind.Sign },
        decorColors = new[] { H("#1a1e26"), H("#262c36"), H("#38404c"), H("#fff0a0") },
    };

    private static readonly Theme Ghost = new Theme
    {
        name = "Ghost", color = H("#b07af0"),
        background = H("#100c1a"), skylineColor = H("#1c1628"), skyline = Skyline.Hills, stars = true, moon = H("#d8d4f0"),
        ground = new[] { H("#4a4462"), H("#3a3550"), H("#221f30"), H("#322d46") },
        topStyle = TopStyle.Plain, top = new[] { H("#8a9a6a"), H("#6a7a52"), H("#4a5638") },
        plank = new[] { H("#8a7a70"), H("#665850"), H("#40363a") },
        decor = new[] { DecorKind.Tombstone, DecorKind.DeadTree, DecorKind.Candle },
        decorColors = new[] { H("#1c1828"), H("#2a243a"), H("#3e3654"), H("#ffd040") },
    };

    private static readonly Theme Volcano = new Theme
    {
        name = "Volcano", color = H("#ff5a20"),
        background = H("#160a0a"), skylineColor = H("#2a100e"), skyline = Skyline.Volcano, moon = None,
        ground = new[] { H("#463a3e"), H("#342b2f"), H("#1c1618"), H("#5a2a1c") },
        topStyle = TopStyle.Plain, top = new[] { H("#6e6066"), H("#544850"), H("#3a3036") },
        plank = new[] { H("#5a4868"), H("#42344e"), H("#261c2e") },
        decor = new[] { DecorKind.Rock, DecorKind.Crystal, DecorKind.Skull },
        decorColors = new[] { H("#221416"), H("#341e1e"), H("#4e2a26"), H("#ff8a20") },
    };

    private static readonly Theme Frost = new Theme
    {
        name = "Ice", color = H("#60c8ff"),
        background = H("#0c1220"), skylineColor = H("#16223a"), skyline = Skyline.Peaks, stars = true, moon = H("#e8f0ff"),
        ground = new[] { H("#5a8ac0"), H("#4672a4"), H("#2a4870"), H("#3c6496") },
        topStyle = TopStyle.Plain, top = IceTop,
        plank = new[] { H("#90a8c4"), H("#6a809c"), H("#3e4e66") },
        decor = new[] { DecorKind.Pine, DecorKind.Crystal, DecorKind.Snowman },
        decorColors = new[] { H("#1a2640"), H("#26385a"), H("#3a5280"), H("#ff8a30") },
    };

    private static readonly Theme[] Themes = { Woods, Desert, Factory, Ghost, Volcano, Frost };

    // ---------- Maps ----------

    private class Layout
    {
        public string name;
        public Theme theme;
        public string[] rows;
        public Motion[] motions = new Motion[0];
    }

    // How the platform drawn with Key moves (MovingPlatform): Travel to each side and back every Period seconds,
    // and/or Spin. A spinning one is a solid bar (a one-way platform can't turn over).
    private class Motion
    {
        public char key;
        public Vector2 travel;
        public float period = 5f;
        public float phase;
        public float spin;
    }

    private static readonly Layout[] Layouts =
    {
        new Layout { name = "GLADE", theme = Woods, rows = new[] {
            "............................",
            "............................",
            "............................",
            "............................",
            "............................",
            "........=====..=====........",
            "............................",
            "..1......................2..",
            "======................======",
            "............................",
            ".........#........#.........",
            "......3..#..a..a..#..4......",
            "....####################....",
            "....####################....",
            ".....##################.....",
            "......################......" } },

        new Layout { name = "CANOPY", theme = Woods, rows = new[] {
            "............................",
            "............................",
            "............................",
            ".........==========.........",
            "............................",
            ".....1...............2......",
            ".....##..............##.....",
            ".....##..............##.....",
            "..3..##..............##..4..",
            "=====##=====....=====##=====",
            ".....##..............##.....",
            ".....##...b....b.....##.....",
            ".....##...========...##.....",
            ".....##..............##.....",
            ".....##..............##.....",
            ".....##..............##....." } },

        new Layout { name = "MESA", theme = Desert, rows = new[] {
            "............................",
            "............................",
            "............................",
            ".......1............2.......",
            ".....=====........=====.....",
            "............................",
            "..3......................4..",
            "=====........XX........=====",
            "............####............",
            "..........########..........",
            "..........########..........",
            ".....X..############..X.....",
            "...a.XX.############.XXa....",
            "...######################...",
            "....####################....",
            ".....##################....." } },

        new Layout { name = "DUNES", theme = Desert, rows = new[] {
            "............................",
            "............................",
            "....3..................4....",
            "..=====..............=====..",
            "............................",
            "............................",
            "..........========..........",
            "...#....................#...",
            "...#....................#...",
            ".1.#..cXX..........XXc..#.2.",
            "#########==========#########",
            "#########..........#########",
            "#########..........#########",
            "########............########",
            "########............########",
            "#######..............#######" } },

        new Layout { name = "ASSEMBLY", theme = Factory, rows = new[] {
            "............................",
            "............................",
            "............................",
            "............................",
            ".......3............4.......",
            ".....======..b...======.....",
            "............####............",
            "............................",
            "..#......................#..",
            "..#...MMMMMM....NNNNNN...#..",
            "..#......................#..",
            "..#......................#..",
            "..#.1......^^^^^^.....2..#..",
            "..####>>>>>######<<<<<####..",
            "...######################...",
            "....####################...." },
            motions = new[] {
                new Motion { key = 'M', travel = new Vector2(1.5f, 0f), period = 4f },
                new Motion { key = 'N', travel = new Vector2(-1.5f, 0f), period = 4f } } },

        new Layout { name = "FURNACE", theme = Factory, rows = new[] {
            "............................",
            "............................",
            "............................",
            "..3......................4..",
            ".=====................=====.",
            "............................",
            "..........#......#..........",
            "....1.....#......#.....2....",
            "..MMMMMM..#.RRRR.#..NNNNNN..",
            "..........#......#..........",
            "..........#......#..........",
            "..........########..........",
            "..........########..........",
            "^^^^..a..##########..a..^^^^",
            "############################",
            "############################" },
            motions = new[] {
                new Motion { key = 'M', travel = new Vector2(0f, 1.5f), period = 5f },
                new Motion { key = 'N', travel = new Vector2(0f, -1.5f), period = 5f },
                new Motion { key = 'R', spin = 60f } } },

        new Layout { name = "GRAVEYARD", theme = Ghost, rows = new[] {
            "............................",
            "............................",
            "............................",
            "............................",
            "...........HHHHHH...........",
            "............................",
            "......3..............4......",
            "....GGGGG..........HHHHH....",
            "............................",
            "............................",
            "...1..a..b........b..a..2...",
            ".##########GGGGGG##########.",
            ".##########......##########.",
            "..#########......#########..",
            "...########......########...",
            "....#######......#######...." } },

        new Layout { name = "CRYPT", theme = Ghost, rows = new[] {
            "............................",
            "............................",
            "............................",
            "...3....................4...",
            "..GGGGG..............HHHHH..",
            "............................",
            "...........######...........",
            "...........#....#...........",
            "......=====#....#=====......",
            "............................",
            ".............c..............",
            "..1..a.....######.....a..2..",
            "############################",
            "############################",
            ".##########################.",
            "..########################.." } },

        new Layout { name = "MAGMA", theme = Volcano, rows = new[] {
            "............................",
            "............................",
            "............................",
            "............................",
            "...........MMMMMM...........",
            "............................",
            ".........3........4.........",
            ".......=====....=====.......",
            "............................",
            "...1..a..............a..2...",
            "..######............######..",
            "..######....b..b....######..",
            "..######...######...######..",
            "...####....######....####...",
            "LLLLLLLLLLLLLLLLLLLLLLLLLLLL",
            "LLLLLLLLLLLLLLLLLLLLLLLLLLLL" },
            motions = new[] { new Motion { key = 'M', travel = new Vector2(5f, 0f), period = 7f } } },

        new Layout { name = "CALDERA", theme = Volcano, rows = new[] {
            "............................",
            "............................",
            "............................",
            "....3..................4....",
            "...=====............=====...",
            "............................",
            "............................",
            "..........MMMMMMMM..........",
            "............................",
            "..1.a................a..2...",
            "#######..............#######",
            "#######..............#######",
            "#########....##....#########",
            "#########LLLLLLLLLL#########",
            "#########LLLLLLLLLL#########",
            "#########LLLLLLLLLL#########" },
            motions = new[] { new Motion { key = 'M', travel = new Vector2(0f, 2f), period = 6f } } },

        new Layout { name = "GLACIER", theme = Frost, rows = new[] {
            "............................",
            "............................",
            "............................",
            "............................",
            "..3......................4..",
            ".=====................=====.",
            "............................",
            "............%%%%............",
            "............%%%%............",
            "............................",
            "......=====......=====......",
            "............................",
            "..1..a...X........X...a..2..",
            "..%%%%%%%%%%%%%%%%%%%%%%%%..",
            "...######################...",
            "....####################...." } },

        new Layout { name = "ICE LAKE", theme = Frost, rows = new[] {
            "............................",
            "............................",
            "............................",
            "............................",
            "...........======...........",
            "............................",
            "....3..................4....",
            "...%%%%%............%%%%%...",
            "...%%%%%............%%%%%...",
            "............................",
            "............................",
            "..1...c.X..........X.c...2..",
            "%%%%%%%%%%........%%%%%%%%%%",
            "##########........##########",
            "##########........##########",
            "##########........##########" } },
    };

    // ---------- Build ----------

    [MenuItem("Tools/Rebuild LAN Maps")]
    public static void BuildAll()
    {
        MapManager manager = Object.FindAnyObjectByType<MapManager>(FindObjectsInactive.Include);
        var so = new SerializedObject(manager);
        SerializedProperty list = so.FindProperty("maps");
        SerializedProperty lobbyIndex = so.FindProperty("lobbyMap");
        LevelMap lobby = lobbyIndex.intValue >= 0 ? (LevelMap)list.GetArrayElementAtIndex(lobbyIndex.intValue).objectReferenceValue : null;

        // Keep the single player maps and the lobby; every old round map goes.
        var maps = new List<LevelMap>();
        for (int i = 0; i < list.arraySize; i++)
        {
            var map = (LevelMap)list.GetArrayElementAtIndex(i).objectReferenceValue;
            if (map == null)
                continue;
            if (map.Pvp)
                Object.DestroyImmediate(map.gameObject);
            else
                maps.Add(map);
        }

        var art = new Dictionary<Theme, ThemeArt>();
        foreach (Theme theme in Themes)
            art[theme] = PaintTheme(theme);
        foreach (Layout layout in Layouts)
            maps.Add(Build(manager.transform, layout, art[layout.theme]));

        // The host spawns each map's crates (MapProps, next to the MapManager).
        MapProps props = manager.GetComponent<MapProps>();
        if (props == null)
            props = manager.gameObject.AddComponent<MapProps>();
        UiKit.Wire(props, "cratePrefab", CratePrefab(art[Woods].crate));

        // A picture of every round map, for the map roulette.
        for (int i = 0; i < Layouts.Length; i++)
            SaveThumbnail(maps[maps.Count - Layouts.Length + i], Layouts[i].theme, maps);

        list.arraySize = maps.Count;
        for (int i = 0; i < maps.Count; i++)
            list.GetArrayElementAtIndex(i).objectReferenceValue = maps[i];
        if (lobby != null)
            lobbyIndex.intValue = maps.IndexOf(lobby);
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
    }

    private static LevelMap Build(Transform parent, Layout layout, ThemeArt art)
    {
        if (layout.rows.Length != Rows)
            throw new System.Exception(layout.name + ": " + layout.rows.Length + " rows, expected " + Rows);
        for (int r = 0; r < Rows; r++)
            if (layout.rows[r].Length != Columns)
                throw new System.Exception(layout.name + " row " + r + ": " + layout.rows[r].Length + " characters, expected " + Columns);

        string objectName = "Map_" + layout.theme.name + "_" + layout.name.Replace(" ", "");
        var root = new GameObject(objectName).transform;
        root.SetParent(parent, false);
        var map = root.gameObject.AddComponent<LevelMap>();
        Transform blocks = Child(root, "Blocks");
        Transform tops = Child(root, "Tops");
        Transform hazards = Child(root, "Hazards");
        Transform decor = Child(root, "Decor");
        Transform spawns = Child(root, "SpawnPoints");

        char[,] cells = new char[Columns, Rows];
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Columns; c++)
                cells[c, r] = layout.rows[r][c];

        // Ground, ice, conveyors and lava: as few boxes as possible (each a run of cells, as tall as it stays the same).
        bool[,] used = new bool[Columns, Rows];
        int count = 0;
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Columns; c++)
            {
                char kind = cells[c, r];
                if ((!IsSolid(kind) && kind != 'L') || used[c, r])
                    continue;
                int width = 1;
                while (c + width < Columns && cells[c + width, r] == kind && !used[c + width, r])
                    width++;
                int height = 1;
                while (r + height < Rows && RowMatches(cells, used, c, width, r + height, kind))
                    height++;
                for (int y = r; y < r + height; y++)
                    for (int x = c; x < c + width; x++)
                        used[x, y] = true;
                count++;
                if (kind == 'L')
                    AddLava(hazards, count, c, r, width, height, art);
                else if (kind == '<' || kind == '>')
                    AddConveyor(blocks, count, c, r, width, height, kind == '>' ? 1f : -1f, art);
                else
                    AddGround(blocks, count, c, r, width, height, kind == '%', art);
            }

        // One row at a time: grass/sand/snow on top of the ground, platforms, spikes.
        for (int r = 0; r < Rows; r++)
        {
            int c = 0;
            while (c < Columns)
            {
                char kind = cells[c, r];
                bool solid = IsSolid(kind);
                bool openAbove = r == 0 || !IsSolid(cells[c, r - 1]);
                int width = 1;
                while (c + width < Columns && cells[c + width, r] == kind &&
                       (!solid || (r == 0 || !IsSolid(cells[c + width, r - 1])) == openAbove))
                    width++;
                Motion motion = System.Array.Find(layout.motions, m => m.key == kind);
                if ((kind == '#' || kind == '%') && openAbove)
                    AddTop(tops, c, r, width, kind == '%' ? art.iceTop : art.top);
                else if (kind == '=')
                    AddPlatform(blocks, c, r, width, art.plank, true);
                else if (kind == 'G' || kind == 'H')
                    AddGhostPlatform(blocks, c, r, width, kind == 'H' ? 0.5f : 0f, art);
                else if (motion != null)
                    AddMovingPlatform(blocks, c, r, width, motion, art);
                else if (kind == '^')
                    AddSpikes(hazards, c, r, width, art);
                c += width;
            }
        }

        // Starts, crates and decorations.
        var spawnPoints = new Transform[4];
        var crateSpots = new List<Transform>();
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Columns; c++)
            {
                char kind = cells[c, r];
                if (kind >= '1' && kind <= '4')
                {
                    Transform point = Child(spawns, "Spawn" + kind);
                    point.localPosition = new Vector3(CellX(c) + 0.5f, CellBottom(r) + 1f, 0f);
                    spawnPoints[kind - '1'] = point;
                }
                else if (kind == 'X')
                {
                    Transform spot = Child(spawns, "Crate");
                    spot.localPosition = new Vector3(CellX(c) + 0.5f, CellBottom(r) + 0.5f, 0f);
                    crateSpots.Add(spot);
                }
                else if (kind >= 'a' && kind <= 'c')
                {
                    SpriteRenderer sprite = Sprite(decor, "Decor", art.decor[kind - 'a'], DecorOrder);
                    sprite.transform.localPosition = new Vector3(CellX(c) + 0.5f, CellBottom(r) - 1f / Ppu, 0f);
                    sprite.flipX = c >= Columns / 2; // mirrored on the right half, like the layouts
                }
            }
        foreach (Transform point in spawnPoints)
            if (point == null)
                throw new System.Exception(layout.name + ": needs the starts 1, 2, 3 and 4");

        AddBackground(root, layout.theme, art);

        var mapSo = new SerializedObject(map);
        mapSo.FindProperty("displayName").stringValue = layout.name;
        mapSo.FindProperty("pvp").boolValue = true;
        mapSo.FindProperty("cameraSize").floatValue = CameraSize;
        mapSo.FindProperty("fallLimitY").floatValue = FallLimit;
        SerializedProperty points = mapSo.FindProperty("spawnPoints");
        points.arraySize = 4;
        for (int i = 0; i < 4; i++)
            points.GetArrayElementAtIndex(i).objectReferenceValue = spawnPoints[i];
        SerializedProperty crates = mapSo.FindProperty("crateSpots");
        crates.arraySize = crateSpots.Count;
        for (int i = 0; i < crateSpots.Count; i++)
            crates.GetArrayElementAtIndex(i).objectReferenceValue = crateSpots[i];
        mapSo.FindProperty("crateSprite").objectReferenceValue = art.crate;
        mapSo.ApplyModifiedPropertiesWithoutUndo();

        root.gameObject.SetActive(false);
        return map;
    }

    // Renders the map alone, 1 art pixel = 1 picture pixel (28 x 16 units), and gives it the theme's color.
    private static void SaveThumbnail(LevelMap map, Theme theme, List<LevelMap> all)
    {
        Camera camera = Camera.main;
        var volumes = Object.FindObjectsByType<UnityEngine.Rendering.Volume>(FindObjectsSortMode.None);
        var wereOn = new bool[volumes.Length];
        for (int i = 0; i < volumes.Length; i++)
        {
            wereOn[i] = volumes[i].enabled;
            if (volumes[i].name == "MenuBlur")
                volumes[i].enabled = false; // the game's look (bloom...) stays, so the picture matches the real map
        }
        var wasActive = new bool[all.Count];
        for (int i = 0; i < all.Count; i++)
        {
            wasActive[i] = all[i].gameObject.activeSelf;
            all[i].gameObject.SetActive(all[i] == map);
        }
        float size = camera.orthographicSize;
        Vector3 position = camera.transform.position;
        camera.orthographicSize = CameraSize;
        camera.transform.position = new Vector3(0f, 0f, position.z);
        var cameraData = camera.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        bool postProcessing = cameraData.renderPostProcessing;
        cameraData.renderPostProcessing = true; // as in a round

        var target = new RenderTexture(Columns * Ppu, Rows * Ppu, 24);
        camera.targetTexture = target;
        camera.Render();
        camera.targetTexture = null;
        RenderTexture.active = target;
        var picture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
        picture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        picture.Apply();
        RenderTexture.active = null;
        target.Release();

        camera.orthographicSize = size;
        camera.transform.position = position;
        cameraData.renderPostProcessing = postProcessing;
        for (int i = 0; i < all.Count; i++)
            all[i].gameObject.SetActive(wasActive[i]);
        for (int i = 0; i < volumes.Length; i++)
            volumes[i].enabled = wereOn[i];

        Sprite thumbnail = Save(ArtRoot + "/Thumbnails", map.name, picture, new Vector2(0.5f, 0.5f), FilterMode.Point);
        var so = new SerializedObject(map);
        so.FindProperty("thumbnail").objectReferenceValue = thumbnail;
        so.FindProperty("themeColor").colorValue = theme.color;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // The networked crate (made once, then kept: its network id must not change). Real physics on the host,
    // copied to the others.
    private static Crate CratePrefab(Sprite look)
    {
        var existing = AssetDatabase.LoadAssetAtPath<Crate>(CratePrefabPath);
        if (existing != null)
            return existing;

        var crate = new GameObject("Crate");
        SpriteRenderer sprite = crate.AddComponent<SpriteRenderer>();
        sprite.sprite = look; // each map sets its own (Crate)
        sprite.sortingOrder = BodyOrder;
        sprite.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterial);
        crate.AddComponent<BoxCollider2D>().size = new Vector2(0.96f, 0.96f);
        var body = crate.AddComponent<Rigidbody2D>();
        body.mass = 6f;
        body.gravityScale = 3f;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        crate.AddComponent<Unity.Netcode.NetworkObject>();
        var sync = crate.AddComponent<Unity.Netcode.Components.NetworkTransform>();
        sync.AuthorityMode = Unity.Netcode.Components.NetworkTransform.AuthorityModes.Server;
        sync.SyncPositionZ = false;
        sync.SyncRotAngleX = false;
        sync.SyncRotAngleY = false;
        sync.SyncScaleX = sync.SyncScaleY = sync.SyncScaleZ = false;
        crate.AddComponent<Unity.Netcode.Components.NetworkRigidbody2D>();
        crate.AddComponent<LevelBlock>(); // stops bullets
        crate.AddComponent<Crate>();
        // Netcode adds a new networked prefab to its default list (DefaultNetworkPrefabs) by itself.
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(crate, CratePrefabPath);
        Object.DestroyImmediate(crate);
        return prefab.GetComponent<Crate>();
    }

    private static bool IsSolid(char kind) => kind == '#' || kind == '%' || kind == '<' || kind == '>';

    private static bool RowMatches(char[,] cells, bool[,] used, int c, int width, int r, char kind)
    {
        for (int x = c; x < c + width; x++)
            if (cells[x, r] != kind || used[x, r])
                return false;
        // Stop where the run gets wider below, so the next row's run stays whole.
        if (c > 0 && cells[c - 1, r] == kind && !used[c - 1, r])
            return false;
        if (c + width < Columns && cells[c + width, r] == kind && !used[c + width, r])
            return false;
        return true;
    }

    // Cell (c, r): left edge x and top edge y in map units (the map's middle is 0, 0).
    private static float CellX(int c) => c - Columns * 0.5f;
    private static float CellTop(int r) => Rows * 0.5f - r;
    private static float CellBottom(int r) => CellTop(r) - 1f;

    private static Transform Child(Transform parent, string name)
    {
        var child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }

    private static SpriteRenderer Sprite(Transform parent, string name, Sprite sprite, int order)
    {
        var renderer = new GameObject(name).AddComponent<SpriteRenderer>();
        renderer.transform.SetParent(parent, false);
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(UnlitMaterial);
        return renderer;
    }

    private static SpriteRenderer Tiled(Transform parent, string name, Sprite sprite, int order, Vector2 size)
    {
        SpriteRenderer renderer = Sprite(parent, name, sprite, order);
        renderer.drawMode = SpriteDrawMode.Tiled;
        renderer.size = size;
        return renderer;
    }

    private static void AddGround(Transform parent, int index, int c, int r, int width, int height, bool ice, ThemeArt art)
    {
        var block = new GameObject((ice ? "Ice_" : "Ground_") + index);
        block.transform.SetParent(parent, false);
        block.transform.localPosition = new Vector3(CellX(c) + width * 0.5f, CellTop(r) - height * 0.5f, 0f);
        var box = block.AddComponent<BoxCollider2D>();
        box.size = new Vector2(width, height);
        if (ice)
            box.sharedMaterial = IceFriction(); // crates slide on it too
        var so = new SerializedObject(block.AddComponent<LevelBlock>());
        so.FindProperty("slippery").boolValue = ice;
        so.ApplyModifiedPropertiesWithoutUndo();
        Tiled(block.transform, "Body", ice ? art.ice : art.body, BodyOrder, new Vector2(width, height));
    }

    private static PhysicsMaterial2D IceFriction()
    {
        string path = ArtRoot + "/IceFriction.physicsMaterial2D";
        var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>(path);
        if (material == null)
        {
            material = new PhysicsMaterial2D("IceFriction") { friction = 0.02f, bounciness = 0f };
            AssetDatabase.CreateAsset(material, path);
        }
        return material;
    }

    // Ground with a moving tread on top: players on it are carried that way (direction 1 = right, -1 = left).
    private static void AddConveyor(Transform parent, int index, int c, int r, int width, int height, float direction, ThemeArt art)
    {
        var block = new GameObject("Conveyor_" + index);
        block.transform.SetParent(parent, false);
        block.transform.localPosition = new Vector3(CellX(c) + width * 0.5f, CellTop(r) - height * 0.5f, 0f);
        block.AddComponent<BoxCollider2D>().size = new Vector2(width, height);
        var so = new SerializedObject(block.AddComponent<LevelBlock>());
        so.FindProperty("conveyorSpeed").floatValue = ConveyorSpeed * direction;
        so.ApplyModifiedPropertiesWithoutUndo();
        SpriteRenderer body = Tiled(block.transform, "Body", art.conveyor[0], BodyOrder, new Vector2(width, height));
        var frames = new SerializedObject(body.gameObject.AddComponent<SpriteFrames>());
        UiKit.WireArray(frames.targetObject, "frames", art.conveyor);
        frames.Update();
        // The tread moves 2 pixels a frame, as fast as the belt carries.
        frames.FindProperty("framesPerSecond").floatValue = ConveyorSpeed * Ppu / 2f * direction;
        frames.ApplyModifiedPropertiesWithoutUndo();
    }

    // The ground's light top edge: its top 4 pixels.
    private static void AddTop(Transform parent, int c, int r, int width, Sprite sprite)
    {
        SpriteRenderer top = Tiled(parent, "Top", sprite, TopOrder, new Vector2(width, 4f / Ppu));
        top.transform.localPosition = new Vector3(CellX(c) + width * 0.5f, CellTop(r) - 2f / Ppu, 0f);
    }

    // Half a cell tall, at the top of its cell. One-way: jump up through it, bullets fly through. Otherwise solid.
    private static GameObject AddPlatform(Transform parent, int c, int r, int width, Sprite sprite, bool oneWay)
    {
        var platform = new GameObject("Platform");
        platform.transform.SetParent(parent, false);
        platform.transform.localPosition = new Vector3(CellX(c) + width * 0.5f, CellTop(r) - 0.25f, 0f);
        var box = platform.AddComponent<BoxCollider2D>();
        box.size = new Vector2(width, 0.5f);
        if (oneWay)
        {
            box.usedByEffector = true;
            var effector = platform.AddComponent<PlatformEffector2D>();
            effector.useOneWay = true;
            effector.surfaceArc = 180f;
        }
        var so = new SerializedObject(platform.AddComponent<LevelBlock>());
        so.FindProperty("bulletsPassThrough").boolValue = oneWay;
        so.ApplyModifiedPropertiesWithoutUndo();
        Tiled(platform.transform, "Body", sprite, TopOrder, new Vector2(width, 0.5f));
        return platform;
    }

    private static void AddGhostPlatform(Transform parent, int c, int r, int width, float phase, ThemeArt art)
    {
        GameObject platform = AddPlatform(parent, c, r, width, art.plank, true);
        platform.name = "GhostPlatform";
        platform.GetComponentInChildren<SpriteRenderer>().color = new Color(0.75f, 0.85f, 1f); // a cold, ghostly tint
        var so = new SerializedObject(platform.AddComponent<GhostPlatform>());
        so.FindProperty("phase").floatValue = phase;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void AddMovingPlatform(Transform parent, int c, int r, int width, Motion motion, ThemeArt art)
    {
        bool spinning = motion.spin != 0f;
        GameObject platform = AddPlatform(parent, c, r, width, art.plank, !spinning);
        platform.name = spinning ? "SpinningBar" : "MovingPlatform";
        var so = new SerializedObject(platform.AddComponent<MovingPlatform>()); // adds the kinematic body
        so.FindProperty("travel").vector2Value = motion.travel;
        so.FindProperty("period").floatValue = motion.period;
        so.FindProperty("phase").floatValue = motion.phase;
        so.FindProperty("spinSpeed").floatValue = motion.spin;
        so.ApplyModifiedPropertiesWithoutUndo();
        platform.GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Kinematic;
    }

    // Deadly from a little under its wavy surface.
    private static void AddLava(Transform parent, int index, int c, int r, int width, int height, ThemeArt art)
    {
        var lava = new GameObject("Lava_" + index);
        lava.transform.SetParent(parent, false);
        lava.transform.localPosition = new Vector3(CellX(c) + width * 0.5f, CellTop(r) - height * 0.5f, 0f);
        var box = lava.AddComponent<BoxCollider2D>();
        box.size = new Vector2(width, height - 0.3f);
        box.offset = new Vector2(0f, -0.15f);
        box.isTrigger = true;
        lava.AddComponent<Hazard>();
        SpriteRenderer surface = Tiled(lava.transform, "Surface", art.lavaTop, LavaOrder, new Vector2(width, 1f));
        surface.transform.localPosition = new Vector3(0f, height * 0.5f - 0.5f, 0f);
        if (height > 1)
        {
            SpriteRenderer body = Tiled(lava.transform, "Body", art.lava, LavaOrder, new Vector2(width, height - 1));
            body.transform.localPosition = new Vector3(0f, -0.5f, 0f);
        }
    }

    // Half a cell tall, on the bottom of its cell.
    private static void AddSpikes(Transform parent, int c, int r, int width, ThemeArt art)
    {
        var spikes = new GameObject("Spikes");
        spikes.transform.SetParent(parent, false);
        spikes.transform.localPosition = new Vector3(CellX(c) + width * 0.5f, CellBottom(r) + 0.25f, 0f);
        var box = spikes.AddComponent<BoxCollider2D>();
        box.size = new Vector2(width - 0.2f, 0.35f);
        box.offset = new Vector2(0f, -0.05f);
        box.isTrigger = true;
        spikes.AddComponent<Hazard>();
        Tiled(spikes.transform, "Body", art.spikes, TopOrder, new Vector2(width, 0.5f));
    }

    // The sky, the skyline and the land under it reach far past the screen (seen when the camera flies in).
    private static void AddBackground(Transform root, Theme theme, ThemeArt art)
    {
        Transform background = Child(root, "Background");
        SpriteRenderer sky = Sprite(background, "Sky", art.sky, SkyOrder);
        sky.transform.localScale = new Vector3(SkylineWidth * 2f / (art.sky.rect.width / Ppu), SkylineWidth / (art.sky.rect.height / Ppu), 1f);
        Sprite(background, "Skyline", art.skyline, SkylineOrder);
        SpriteRenderer land = Sprite(background, "Land", UiKit.PixelSprite, SkylineOrder);
        land.color = theme.skylineColor;
        const float landHeight = 120f;
        Vector2 pixelSize = land.sprite.bounds.size;
        land.transform.localScale = new Vector3(SkylineWidth / pixelSize.x, landHeight / pixelSize.y, 1f);
        land.transform.localPosition = new Vector3(0f, -10f - landHeight * 0.5f, 0f); // under the skyline's bottom
    }

    // ---------- Pixel art ----------

    private class ThemeArt
    {
        public Sprite body, top, plank, ice, iceTop, lava, lavaTop, spikes, sky, skyline, crate;
        public Sprite[] decor, conveyor;
    }

    private static ThemeArt PaintTheme(Theme theme)
    {
        string folder = ArtRoot + "/" + theme.name;
        Vector2 middle = new Vector2(0.5f, 0.5f);
        var art = new ThemeArt
        {
            body = Save(folder, "Ground", PaintBody(theme.ground, 1), middle),
            top = Save(folder, "GroundTop", PaintTop(theme.topStyle, theme.top), middle),
            plank = Save(folder, "Platform", PaintPlank(theme.plank), middle),
            ice = Save(folder, "Ice", PaintBody(IceGround, 3), middle),
            iceTop = Save(folder, "IceTop", PaintTop(TopStyle.Plain, IceTop), middle),
            lava = Save(folder, "Lava", PaintLava(false), middle),
            lavaTop = Save(folder, "LavaTop", PaintLava(true), middle),
            spikes = Save(folder, "Spikes", PaintSpikes(Metal), middle),
            sky = Save(folder, "Sky", PaintSky(theme), middle),
            skyline = Save(folder, "Skyline", PaintSkyline(theme), middle),
            crate = Save(folder, "Crate", PaintCrate(theme == Frost ? IceGround : theme.plank), middle),
            decor = new Sprite[theme.decor.Length],
            conveyor = new Sprite[4],
        };
        for (int i = 0; i < art.conveyor.Length; i++)
            art.conveyor[i] = Save(folder, "Conveyor" + i, PaintConveyor(i * 2), middle);
        for (int i = 0; i < theme.decor.Length; i++)
            art.decor[i] = Save(folder, "Decor_" + theme.decor[i], PaintDecor(theme.decor[i], theme.decorColors), new Vector2(0.5f, 0f));
        return art;
    }

    private static Sprite Save(string folder, string name, Pix pix, Vector2 pivot) =>
        Save(folder, name, pix.ToTexture(), pivot, FilterMode.Point);

    private static Sprite Save(string folder, string name, Texture2D texture, Vector2 pivot, FilterMode filter)
    {
        Directory.CreateDirectory(folder);
        string path = folder + "/" + name + ".png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = filter;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spritePixelsPerUnit = Ppu;
        importer.alphaIsTransparency = true;
        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect; // needed for tiled drawing
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        importer.SetTextureSettings(settings);
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // A small pixel canvas, (0, 0) = bottom left, clear to start.
    private class Pix
    {
        public readonly int w, h;
        private readonly Color[] pixels;

        public Pix(int width, int height)
        {
            w = width;
            h = height;
            pixels = new Color[w * h];
        }

        public void Set(int x, int y, Color color)
        {
            if (x >= 0 && x < w && y >= 0 && y < h)
                pixels[y * w + x] = color;
        }

        public Color Get(int x, int y) => x >= 0 && x < w && y >= 0 && y < h ? pixels[y * w + x] : Color.clear;

        public void Rect(int x, int y, int width, int height, Color color)
        {
            for (int j = y; j < y + height; j++)
                for (int i = x; i < x + width; i++)
                    Set(i, j, color);
        }

        public void Disc(float cx, float cy, float radius, Color color)
        {
            for (int j = Mathf.FloorToInt(cy - radius); j <= Mathf.CeilToInt(cy + radius); j++)
                for (int i = Mathf.FloorToInt(cx - radius); i <= Mathf.CeilToInt(cx + radius); i++)
                    if ((i + 0.5f - cx) * (i + 0.5f - cx) + (j + 0.5f - cy) * (j + 0.5f - cy) <= radius * radius)
                        Set(i, j, color);
        }

        public void Line(int x0, int y0, int x1, int y1, Color color)
        {
            int dx = Mathf.Abs(x1 - x0), dy = -Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1, sy = y0 < y1 ? 1 : -1;
            int error = dx + dy;
            while (true)
            {
                Set(x0, y0, color);
                if (x0 == x1 && y0 == y1)
                    return;
                int e2 = 2 * error;
                if (e2 >= dy) { error += dy; x0 += sx; }
                if (e2 <= dx) { error += dx; y0 += sy; }
            }
        }

        // A dark outline around everything drawn so far.
        public void Outline(Color color)
        {
            var edge = new List<Vector2Int>();
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    if (Get(x, y).a == 0f && (Get(x - 1, y).a > 0f || Get(x + 1, y).a > 0f || Get(x, y - 1).a > 0f || Get(x, y + 1).a > 0f))
                        edge.Add(new Vector2Int(x, y));
            foreach (Vector2Int p in edge)
                Set(p.x, p.y, color);
        }

        public Texture2D ToTexture()
        {
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }

    // The same "random" number for the same inputs, 0..1.
    private static float Noise(int x, int y, int seed)
    {
        unchecked
        {
            uint n = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
            n = (n ^ (n >> 13)) * 1274126177u;
            n ^= n >> 16;
            return (n & 0xffff) / 65535f;
        }
    }

    // 16 x 16, tiles seamlessly: the game's stone tile (a light top-left edge, a dark bottom-right edge, a few
    // specks), so the blocks show as a grid of stones. g = light edge, fill, dark edge, speck.
    private static Pix PaintBody(Color[] g, int seed)
    {
        var pix = new Pix(16, 16);
        pix.Rect(0, 0, 16, 16, g[1]);
        pix.Rect(1, 0, 15, 1, g[2]);
        pix.Rect(15, 0, 1, 15, g[2]);
        pix.Rect(0, 15, 16, 1, g[0]);
        pix.Rect(0, 0, 1, 16, g[0]);
        for (int i = 0; i < 5; i++)
            pix.Set(2 + (int)(Noise(i, 0, seed) * 12f), 2 + (int)(Noise(i, 1, seed) * 12f), g[3]);
        return pix;
    }

    // 16 x 4: the light edge on top of the ground (its top row is the ground's top). t = light, mid, dark.
    private static Pix PaintTop(TopStyle style, Color[] t)
    {
        var pix = new Pix(16, 4);
        for (int x = 0; x < 16; x++)
        {
            pix.Set(x, 3, t[0]);
            if (style == TopStyle.Stripes)
            {
                for (int y = 1; y <= 2; y++)
                    pix.Set(x, y, ((x + y) / 2) % 2 == 0 ? t[1] : t[2]);
                pix.Set(x, 0, t[2]);
            }
            else
            {
                pix.Set(x, 2, t[1]);
                pix.Set(x, 1, t[2]);
                if (x % 3 == 0)
                    pix.Set(x, 0, t[2]); // little drips
            }
        }
        return pix;
    }

    // 16 x 8: half a unit tall, a light top and a dark bottom. p = light, mid, dark.
    private static Pix PaintPlank(Color[] p)
    {
        var pix = new Pix(16, 8);
        pix.Rect(0, 0, 16, 8, p[1]);
        pix.Rect(0, 7, 16, 1, p[0]);
        pix.Rect(0, 0, 16, 1, p[2]);
        pix.Rect(0, 1, 1, 6, p[0]);
        pix.Rect(15, 1, 1, 6, p[2]);
        return pix;
    }

    // 16 x 16: a frame with a cross brace. p = light, mid, dark.
    private static Pix PaintCrate(Color[] p)
    {
        var pix = new Pix(16, 16);
        pix.Rect(0, 0, 16, 16, p[1]);
        for (int i = 2; i < 14; i++)
        {
            pix.Set(i, i, p[0]);
            pix.Set(i, 15 - i, p[0]);
        }
        pix.Rect(0, 14, 16, 2, p[0]);
        pix.Rect(0, 0, 2, 16, p[0]);
        pix.Rect(2, 0, 14, 2, p[2]);
        pix.Rect(14, 2, 2, 12, p[2]);
        return pix;
    }

    // 16 x 16 conveyor: rollers in a dark casing, the tread on top shifted Shift pixels (one animation frame).
    private static Pix PaintConveyor(int shift)
    {
        var pix = new Pix(16, 16);
        pix.Rect(0, 0, 16, 12, Metal[0]);
        foreach (int x in new[] { 4, 12 })
        {
            pix.Disc(x, 6f, 3f, Metal[1]);
            pix.Set(x - 1 + (shift / 2) % 2, 6, Metal[0]); // the rollers turn too
        }
        for (int x = 0; x < 16; x++)
        {
            bool yellow = ((x - shift) % 8 + 8) % 8 < 4;
            pix.Rect(x, 12, 1, 3, yellow ? H("#f0c020") : H("#22242a"));
            pix.Set(x, 15, Metal[2]);
        }
        return pix;
    }

    // 16 x 16 of lava; the top one has a stepped surface 3 pixels under its top.
    private static Pix PaintLava(bool surface)
    {
        var pix = new Pix(16, 16);
        pix.Rect(0, 0, 16, 16, Lava[1]);
        for (int i = 0; i < 6; i++)
            pix.Set((int)(Noise(i, 0, 11) * 16f), (int)(Noise(i, 1, 11) * 12f), i % 2 == 0 ? Lava[0] : Lava[2]);
        if (surface)
            for (int x = 0; x < 16; x++)
            {
                int level = 12 + (x / 4) % 2;
                pix.Rect(x, level + 1, 1, 15 - level, Color.clear);
                pix.Set(x, level, Lava[3]);
                pix.Set(x, level - 1, Lava[2]);
            }
        return pix;
    }

    // 16 x 8: two spikes, lit from the left.
    private static Pix PaintSpikes(Color[] c)
    {
        var pix = new Pix(16, 8);
        for (int s = 0; s < 2; s++)
            for (int x = 0; x < 8; x++)
            {
                int height = 8 - Mathf.RoundToInt(Mathf.Abs(x - 3.5f) * 2f);
                for (int y = 0; y < height; y++)
                    pix.Set(s * 8 + x, y, x < 4 ? c[2] : c[1]);
            }
        pix.Rect(0, 0, 16, 1, c[0]);
        return pix;
    }

    // One flat color, stretched over the screen.
    private static Pix PaintSky(Theme theme)
    {
        var pix = new Pix(4, 4);
        pix.Rect(0, 0, 4, 4, theme.background);
        return pix;
    }

    // 34 x 20 units: hills, trees, buildings... in one flat color just lighter than the background, a few stars and
    // the moon above them.
    // Much wider than the screen, so the map sits in a whole landscape when the camera flies in from far out
    // (MapRoulette). The screen's own 34 units are in the middle.
    private static Pix PaintSkyline(Theme theme)
    {
        const int width = SkylineWidth * Ppu, height = 20 * Ppu;
        const int screenLeft = (SkylineWidth - 34) / 2 * Ppu; // where the screen's part starts
        var pix = new Pix(width, height);
        if (theme.stars)
            for (int y = height / 2; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (Noise(x, y, 5) > 0.998f)
                        pix.Set(x, y, Color.Lerp(theme.background, Color.white, 0.5f));
        if (theme.moon.a > 0f)
            pix.Disc(screenLeft + 34 * Ppu * 0.78f, height * 0.78f, 14f, theme.moon);
        for (int x = 0; x < width; x++)
            pix.Rect(x, 0, 1, SkylineHeight(theme.skyline, x - screenLeft, 21), theme.skylineColor);
        return pix;
    }

    private static int SkylineHeight(Skyline skyline, int x, int seed)
    {
        const float baseHeight = 110f;
        int p = x + 40040; // never negative, for the repeating patterns (x is negative left of the screen)
        switch (skyline)
        {
            case Skyline.Pines:
            {
                const int period = 14;
                int tree = (p + seed) / period;
                float treeHeight = 26f + Noise(tree, 0, seed) * 26f;
                float along = Mathf.Abs((p + seed) % period - period * 0.5f) / (period * 0.5f);
                return Mathf.RoundToInt(baseHeight + 10f * Mathf.Sin(x * 0.01f + seed) + treeHeight * (1f - along));
            }
            case Skyline.Dunes:
                return Mathf.RoundToInt(baseHeight - 20f + 26f * Mathf.Sin(x * 0.012f + seed) + 12f * Mathf.Sin(x * 0.031f + seed * 2));
            case Skyline.City:
            {
                int building = (p + seed * 3) / 26;
                float rise = Noise(building, 1, seed) * 110f;
                bool chimney = (p + seed * 3) % 26 >= 5 && (p + seed * 3) % 26 < 9 && Noise(building, 2, seed) > 0.5f;
                return Mathf.RoundToInt(baseHeight - 30f + rise + (chimney ? 34f : 0f));
            }
            case Skyline.Hills:
                return Mathf.RoundToInt(baseHeight - 20f + 22f * Mathf.Sin(x * 0.018f + seed) + 9f * Mathf.Sin(x * 0.05f + seed));
            case Skyline.Volcano:
            {
                float fromTop = Mathf.Abs(x - 34 * Ppu * 0.5f);
                float cone = fromTop < 18f ? 212f : 230f - fromTop * 0.95f; // a crater on top
                return Mathf.RoundToInt(Mathf.Max(baseHeight - 30f + 10f * Mathf.Sin(x * 0.03f), cone));
            }
            default: // Peaks
            {
                const int period = 128;
                int peak = (p + seed * 5) / period;
                float along = Mathf.Abs((p + seed * 5) % period - period * 0.5f) / (period * 0.5f);
                float peakHeight = 90f * (0.6f + Noise(peak, 0, seed) * 0.6f);
                return Mathf.RoundToInt(baseHeight - 20f + peakHeight * (1f - along));
            }
        }
    }

    // Decorations: drawn, then flattened into the theme's 3 dark decoration colors (they stay in the background),
    // except the accent (lights, flames). c = dark, mid, light, accent.
    private static Pix PaintDecor(DecorKind kind, Color[] c)
    {
        Pix pix;
        switch (kind)
        {
            case DecorKind.Tree:
                pix = new Pix(26, 40);
                pix.Rect(11, 0, 4, 22, H("#5c3a20"));
                pix.Rect(14, 0, 1, 22, H("#3b2414"));
                foreach (Vector3 blob in new[] { new Vector3(13, 27, 11), new Vector3(7, 23, 6), new Vector3(19, 23, 6) })
                    for (int y = 0; y < 40; y++)
                        for (int x = 0; x < 26; x++)
                        {
                            float dx = x + 0.5f - blob.x, dy = y + 0.5f - blob.y;
                            if (dx * dx + dy * dy > blob.z * blob.z)
                                continue;
                            float light = -dx * 0.5f + dy * 0.7f;
                            pix.Set(x, y, light > 3f ? c[2] : light < -3f ? c[0] : c[1]);
                        }
                break;

            case DecorKind.Pine:
                pix = new Pix(22, 36);
                pix.Rect(10, 0, 2, 5, H("#4a3020"));
                for (int tier = 0; tier < 3; tier++)
                {
                    int bottom = 4 + tier * 9, tall = 13;
                    float half = 10f - tier * 2.5f;
                    for (int y = bottom; y < bottom + tall; y++)
                        for (int x = 0; x < 22; x++)
                        {
                            float reach = half * (1f - (y - bottom) / (float)tall);
                            if (Mathf.Abs(x + 0.5f - 11f) <= reach)
                                pix.Set(x, y, x < 11 ? H("#2e6a50") : H("#1e4a3a"));
                        }
                }
                // Snow on every tier's upper edge.
                for (int x = 0; x < 22; x++)
                    for (int y = 35; y > 0; y--)
                        if (pix.Get(x, y).a > 0f && pix.Get(x, y + 1).a == 0f)
                            pix.Set(x, y, H("#eef8ff"));
                break;

            case DecorKind.Bush:
                pix = new Pix(18, 10);
                for (int y = 0; y < 10; y++)
                    for (int x = 0; x < 18; x++)
                    {
                        float dx = (x + 0.5f - 9f) / 8.5f, dy = (y + 0.5f - 3f) / 6.5f;
                        if (dx * dx + dy * dy <= 1f)
                            pix.Set(x, y, dy > 0.5f && dx < 0.2f ? c[2] : dx > 0.4f ? c[0] : c[1]);
                    }
                break;

            case DecorKind.Lantern:
                pix = new Pix(8, 26);
                pix.Rect(3, 0, 2, 19, H("#2a2a30"));
                pix.Rect(1, 19, 6, 6, H("#2a2a30"));
                pix.Rect(2, 20, 4, 4, c[3]);
                pix.Rect(2, 25, 4, 1, H("#2a2a30"));
                break;

            case DecorKind.Cactus:
                pix = new Pix(14, 24);
                pix.Rect(5, 0, 4, 22, H("#4c9a3e"));
                pix.Rect(6, 22, 2, 1, H("#4c9a3e"));
                pix.Rect(1, 9, 2, 7, H("#4c9a3e"));
                pix.Rect(3, 9, 2, 2, H("#4c9a3e"));
                pix.Rect(11, 12, 2, 7, H("#4c9a3e"));
                pix.Rect(9, 12, 2, 2, H("#4c9a3e"));
                pix.Rect(6, 1, 1, 20, H("#7ccf52"));
                pix.Rect(8, 0, 1, 22, H("#2f6b36"));
                break;

            case DecorKind.Skull:
                pix = new Pix(10, 8);
                pix.Disc(5f, 4.5f, 3.6f, H("#e8e0d0"));
                pix.Rect(3, 0, 4, 2, H("#e8e0d0"));
                pix.Rect(3, 3, 1, 2, H("#2a2020"));
                pix.Rect(6, 3, 1, 2, H("#2a2020"));
                pix.Set(4, 0, H("#b0a898"));
                break;

            case DecorKind.DeadTree:
                pix = new Pix(20, 28);
                pix.Line(10, 0, 10, 15, c[1]);
                pix.Line(11, 0, 11, 13, c[1]);
                pix.Line(10, 10, 4, 20, c[1]);
                pix.Line(4, 20, 2, 24, c[1]);
                pix.Line(11, 12, 16, 22, c[1]);
                pix.Line(16, 22, 18, 23, c[1]);
                pix.Line(10, 15, 9, 26, c[1]);
                pix.Line(6, 17, 7, 22, c[1]);
                pix.Line(14, 18, 12, 21, c[1]);
                break;

            case DecorKind.Tombstone:
                pix = new Pix(12, 15);
                pix.Rect(1, 0, 10, 10, c[1]);
                pix.Disc(6f, 9.5f, 5f, c[1]);
                pix.Rect(1, 0, 2, 12, c[2]);
                pix.Rect(5, 4, 2, 7, c[0]);
                pix.Rect(3, 8, 6, 2, c[0]);
                pix.Rect(0, 0, 12, 1, c[0]);
                break;

            case DecorKind.Candle:
                pix = new Pix(4, 9);
                pix.Rect(1, 0, 2, 5, H("#e8e0c8"));
                pix.Rect(1, 5, 2, 2, c[3]);
                pix.Set(1, 7, H("#ff8020"));
                break;

            case DecorKind.Pipe:
                pix = new Pix(10, 32);
                pix.Rect(2, 0, 6, 32, c[1]);
                pix.Rect(2, 0, 1, 32, c[2]);
                pix.Rect(7, 0, 1, 32, c[0]);
                pix.Rect(1, 8, 8, 2, c[0]);
                pix.Rect(1, 22, 8, 2, c[0]);
                break;

            case DecorKind.Lamp:
                pix = new Pix(10, 24);
                pix.Rect(4, 0, 2, 19, H("#2e323c"));
                pix.Rect(1, 18, 8, 3, H("#4e5664"));
                pix.Rect(2, 17, 6, 1, c[3]);
                pix.Rect(1, 0, 8, 1, H("#2e323c"));
                break;

            case DecorKind.Sign:
                pix = new Pix(14, 16);
                pix.Rect(6, 0, 2, 7, H("#4e5664"));
                pix.Rect(1, 6, 12, 9, H("#f0c020"));
                pix.Rect(6, 9, 2, 4, H("#22242a"));
                pix.Rect(6, 7, 2, 1, H("#22242a"));
                break;

            case DecorKind.Rock:
                pix = new Pix(16, 9);
                for (int y = 0; y < 9; y++)
                    for (int x = 0; x < 16; x++)
                    {
                        float dx = (x + 0.5f - 8f) / 7.5f, dy = (y + 0.5f) / 8.5f;
                        if (dx * dx + dy * dy <= 1f)
                            pix.Set(x, y, dy > 0.6f && dx < 0f ? H("#4a4048") : dx > 0.3f ? H("#1c181c") : H("#302a30"));
                    }
                pix.Line(5, 1, 8, 5, c[1]);
                break;

            case DecorKind.Crystal:
                pix = new Pix(12, 18);
                foreach (Vector3 shard in new[] { new Vector3(6, 16, 2.5f), new Vector3(3, 10, 1.5f), new Vector3(9, 11, 1.5f) })
                    for (int y = 0; y < (int)shard.y; y++)
                        for (int x = 0; x < 12; x++)
                        {
                            float reach = y < shard.y - shard.z * 2f ? shard.z : shard.z * (shard.y - y) / (shard.z * 2f);
                            if (Mathf.Abs(x + 0.5f - shard.x) <= reach)
                                pix.Set(x, y, x + 0.5f < shard.x ? c[2] : c[1]);
                        }
                pix.Set(5, 12, c[3]);
                break;

            default: // Snowman
                pix = new Pix(12, 19);
                pix.Disc(6f, 5f, 5f, H("#f0f8ff"));
                pix.Disc(6f, 12.5f, 3.6f, H("#f0f8ff"));
                pix.Rect(7, 1, 4, 3, H("#b8cce0"));
                pix.Set(5, 13, H("#22242a"));
                pix.Set(7, 13, H("#22242a"));
                pix.Rect(6, 12, 3, 1, c[3]);
                pix.Rect(3, 16, 6, 1, H("#22242a"));
                pix.Rect(4, 17, 4, 2, H("#22242a"));
                break;
        }
        for (int y = 0; y < pix.h; y++)
            for (int x = 0; x < pix.w; x++)
            {
                Color color = pix.Get(x, y);
                if (color.a == 0f || color == c[3])
                    continue;
                float light = color.grayscale;
                pix.Set(x, y, light < 0.3f ? c[0] : light < 0.6f ? c[1] : c[2]);
            }
        pix.Outline(Color.Lerp(c[0], Color.black, 0.5f));
        return pix;
    }
}
