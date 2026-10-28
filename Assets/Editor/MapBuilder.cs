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
//   1 2 3 4  player starts (slot order; 1 and 2 far apart)
//   a b c  the theme's decorations, standing on what is below them (no collision)
// Falling off the bottom or the sides kills.
public static class MapBuilder
{
    private const int Columns = 28;
    private const int Rows = 16;
    private const float CameraSize = Rows * 0.5f;
    private const float FallLimit = -10f;
    private const int Ppu = 16;
    private const string ArtRoot = "Assets/Sprites/Maps";
    private const string UnlitMaterial = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/Sprite-Unlit-Default.mat";

    // Draw order, back to front.
    private const int SkyOrder = -30;
    private const int FarOrder = -29;
    private const int NearOrder = -28;
    private const int DecorOrder = -6;
    private const int LavaOrder = -4;
    private const int BodyOrder = -3;
    private const int TopOrder = -2;

    // ---------- Themes ----------

    private enum BodyStyle { Rock, Bricks, Panels }
    private enum TopStyle { Tufts, Smooth, Snow, Stripes }
    private enum PlankStyle { Wood, Grate }
    private enum Skyline { Pines, Dunes, City, Hills, Volcano, Peaks }
    private enum DecorKind { Tree, Pine, Bush, Lantern, Cactus, Skull, DeadTree, Tombstone, Candle, Pipe, Lamp, Sign, Rock, Crystal, Snowman }

    private class Theme
    {
        public string name;
        public Color skyTop, skyBottom, far, near;
        public Skyline skyline;
        public bool stars;
        public Color orb;          // sun or moon (alpha 0 = none)
        public BodyStyle bodyStyle;
        public Color[] body;       // dark, mid, light (+ glowing cracks for Rock)
        public TopStyle topStyle;
        public Color[] top;        // dark, mid, light
        public PlankStyle plankStyle;
        public Color[] plank;      // dark, mid, light
        public Color[] spikes;     // dark, mid, light
        public DecorKind[] decor;  // a, b, c
        public Color[] decorColors; // dark, mid, light, accent (for the decorations that take the theme's colors)
    }

    private static Color H(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color color);
        return color;
    }

    private static readonly Color[] Ice = { H("#3a70a8"), H("#5a9ad0"), H("#a0d0f0") };
    private static readonly Color[] IceTop = { H("#4aa0d0"), H("#a8e8ff"), H("#f0fcff") };
    private static readonly Color[] Lava = { H("#a01808"), H("#e8400c"), H("#ff9a20"), H("#ffe070") };
    private static readonly Color[] Metal = { H("#3a3e48"), H("#9aa2b0"), H("#d8dee8") };

    private static readonly Theme Woods = new Theme
    {
        name = "Woods", skyTop = H("#1b3046"), skyBottom = H("#4f8f78"), far = H("#2b5a4c"), near = H("#1f4239"),
        skyline = Skyline.Pines, orb = new Color(0, 0, 0, 0),
        bodyStyle = BodyStyle.Rock, body = new[] { H("#3b2a22"), H("#5a3f2e"), H("#7a5638") },
        topStyle = TopStyle.Tufts, top = new[] { H("#24502a"), H("#3f8a3a"), H("#7ccf52") },
        plankStyle = PlankStyle.Wood, plank = new[] { H("#4a2e18"), H("#8a5a2b"), H("#b88044") },
        spikes = Metal,
        decor = new[] { DecorKind.Tree, DecorKind.Bush, DecorKind.Lantern },
        decorColors = new[] { H("#1f4a2a"), H("#2f6b36"), H("#4c9a3e"), H("#ffd060") },
    };

    private static readonly Theme Desert = new Theme
    {
        name = "Desert", skyTop = H("#e8823c"), skyBottom = H("#ffe2a0"), far = H("#e0a060"), near = H("#c8783e"),
        skyline = Skyline.Dunes, orb = H("#fff4c8"),
        bodyStyle = BodyStyle.Bricks, body = new[] { H("#8a4e2c"), H("#c0844c"), H("#e8b070") },
        topStyle = TopStyle.Smooth, top = new[] { H("#c89850"), H("#ecc878"), H("#fff0b8") },
        plankStyle = PlankStyle.Wood, plank = new[] { H("#5a3620"), H("#9a6236"), H("#c88e52") },
        spikes = new[] { H("#6a5040"), H("#e8dcc8"), H("#fffaf0") },
        decor = new[] { DecorKind.Cactus, DecorKind.Skull, DecorKind.DeadTree },
        decorColors = new[] { H("#5a3a24"), H("#8a5a38"), H("#b07a4c"), H("#4c9a3e") },
    };

    private static readonly Theme Factory = new Theme
    {
        name = "Factory", skyTop = H("#141720"), skyBottom = H("#3a3f52"), far = H("#2a2e3c"), near = H("#1d2029"),
        skyline = Skyline.City, stars = true, orb = new Color(0, 0, 0, 0),
        bodyStyle = BodyStyle.Panels, body = new[] { H("#2e323c"), H("#4e5664"), H("#7a8496") },
        topStyle = TopStyle.Stripes, top = new[] { H("#22242a"), H("#f0c020"), H("#ffe070") },
        plankStyle = PlankStyle.Grate, plank = new[] { H("#262a32"), H("#6a7282"), H("#a8b0c0") },
        spikes = Metal,
        decor = new[] { DecorKind.Pipe, DecorKind.Lamp, DecorKind.Sign },
        decorColors = new[] { H("#2e3a36"), H("#4a6a5a"), H("#7aa08a"), H("#fff0a0") },
    };

    private static readonly Theme Ghost = new Theme
    {
        name = "Ghost", skyTop = H("#0e0a1e"), skyBottom = H("#3a2558"), far = H("#2a1c40"), near = H("#1a1230"),
        skyline = Skyline.Hills, stars = true, orb = H("#e8e4ff"),
        bodyStyle = BodyStyle.Bricks, body = new[] { H("#2e2a3c"), H("#4a4460"), H("#6e6890") },
        topStyle = TopStyle.Tufts, top = new[] { H("#2a3428"), H("#4a5a3c"), H("#7a8a55") },
        plankStyle = PlankStyle.Wood, plank = new[] { H("#3a2e30"), H("#6a5450"), H("#988070") },
        spikes = new[] { H("#3a3440"), H("#c8c0d8"), H("#f4f0ff") },
        decor = new[] { DecorKind.Tombstone, DecorKind.DeadTree, DecorKind.Candle },
        decorColors = new[] { H("#2a2438"), H("#5a5470"), H("#8a84a6"), H("#ffd040") },
    };

    private static readonly Theme Volcano = new Theme
    {
        name = "Volcano", skyTop = H("#1e0808"), skyBottom = H("#8a2a10"), far = H("#3a1410"), near = H("#220a0a"),
        skyline = Skyline.Volcano, stars = false, orb = new Color(0, 0, 0, 0),
        bodyStyle = BodyStyle.Rock, body = new[] { H("#1c181c"), H("#302a30"), H("#4a4048"), H("#ff7a20") },
        topStyle = TopStyle.Smooth, top = new[] { H("#2a2428"), H("#4a4248"), H("#6e6470") },
        plankStyle = PlankStyle.Wood, plank = new[] { H("#181220"), H("#2e2438"), H("#4e3e5e") },
        spikes = new[] { H("#1a1418"), H("#5a4e58"), H("#9a8c96") },
        decor = new[] { DecorKind.Rock, DecorKind.Crystal, DecorKind.Skull },
        decorColors = new[] { H("#a01808"), H("#ff6a10"), H("#ffc040"), H("#fff0a0") },
    };

    private static readonly Theme Frost = new Theme
    {
        name = "Ice", skyTop = H("#141e44"), skyBottom = H("#6a9ed0"), far = H("#3a5a8a"), near = H("#26406a"),
        skyline = Skyline.Peaks, stars = true, orb = H("#f0f8ff"),
        bodyStyle = BodyStyle.Bricks, body = new[] { H("#2c5a8c"), H("#4a82b8"), H("#8ab8e0") },
        topStyle = TopStyle.Snow, top = new[] { H("#9ab8d8"), H("#dceaf8"), H("#ffffff") },
        plankStyle = PlankStyle.Wood, plank = new[] { H("#3a4a60"), H("#5e7290"), H("#90a8c4") },
        spikes = new[] { H("#3a70a8"), H("#a8e0ff"), H("#f0fcff") },
        decor = new[] { DecorKind.Pine, DecorKind.Crystal, DecorKind.Snowman },
        decorColors = new[] { H("#3a80c0"), H("#80d0f8"), H("#d8f6ff"), H("#ff8a30") },
    };

    private static readonly Theme[] Themes = { Woods, Desert, Factory, Ghost, Volcano, Frost };

    // ---------- Maps ----------

    private class Layout
    {
        public string name;
        public Theme theme;
        public string[] rows;
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
            "=====..................=====",
            "............####............",
            "..........########..........",
            "..........########..........",
            "........############........",
            "...a....############...a....",
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
            ".1.#..c..............c..#.2.",
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
            "..#...======....======...#..",
            "..#......................#..",
            "..#......................#..",
            "..#.1......^^^^^^.....2..#..",
            "..########################..",
            "...######################...",
            "....####################...." } },

        new Layout { name = "FURNACE", theme = Factory, rows = new[] {
            "............................",
            "............................",
            "............................",
            "..3......................4..",
            ".=====................=====.",
            "............................",
            "..........#......#..........",
            "....1.....#..==..#.....2....",
            "..======..#......#..======..",
            "..........#......#..........",
            "..........#......#..........",
            "..........########..........",
            "..........########..........",
            "^^^^..a..##########..a..^^^^",
            "############################",
            "############################" } },

        new Layout { name = "GRAVEYARD", theme = Ghost, rows = new[] {
            "............................",
            "............................",
            "............................",
            "............................",
            "...........======...........",
            "............................",
            "......3..............4......",
            "....=====..........=====....",
            "............................",
            "............................",
            "...1..a..b........b..a..2...",
            ".##########======##########.",
            ".##########......##########.",
            "..#########......#########..",
            "...########......########...",
            "....#######......#######...." } },

        new Layout { name = "CRYPT", theme = Ghost, rows = new[] {
            "............................",
            "............................",
            "............................",
            "...3....................4...",
            "..=====..............=====..",
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
            "...........======...........",
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
            "LLLLLLLLLLLLLLLLLLLLLLLLLLLL" } },

        new Layout { name = "CALDERA", theme = Volcano, rows = new[] {
            "............................",
            "............................",
            "............................",
            "....3..................4....",
            "...=====............=====...",
            "............................",
            "............................",
            "..........========..........",
            "............................",
            "..1.a................a..2...",
            "#######..............#######",
            "#######..............#######",
            "#########....##....#########",
            "#########LLLLLLLLLL#########",
            "#########LLLLLLLLLL#########",
            "#########LLLLLLLLLL#########" } },

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
            "..1..a................a..2..",
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
            "..1...c..............c...2..",
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

        // Ground and ice: as few boxes as possible (each a run of cells, as tall as it stays the same).
        bool[,] used = new bool[Columns, Rows];
        int count = 0;
        for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Columns; c++)
            {
                char kind = cells[c, r];
                if ((kind != '#' && kind != '%' && kind != 'L') || used[c, r])
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
                if (solid && openAbove)
                    AddTop(tops, c, r, width, kind == '%' ? art.iceTop : art.top);
                else if (kind == '=')
                    AddPlatform(blocks, c, r, width, art);
                else if (kind == '^')
                    AddSpikes(hazards, c, r, width, art);
                c += width;
            }
        }

        // Starts and decorations.
        var spawnPoints = new Transform[4];
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

        AddBackground(root, art);

        var mapSo = new SerializedObject(map);
        mapSo.FindProperty("displayName").stringValue = layout.name;
        mapSo.FindProperty("pvp").boolValue = true;
        mapSo.FindProperty("cameraSize").floatValue = CameraSize;
        mapSo.FindProperty("fallLimitY").floatValue = FallLimit;
        SerializedProperty points = mapSo.FindProperty("spawnPoints");
        points.arraySize = 4;
        for (int i = 0; i < 4; i++)
            points.GetArrayElementAtIndex(i).objectReferenceValue = spawnPoints[i];
        mapSo.ApplyModifiedPropertiesWithoutUndo();

        root.gameObject.SetActive(false);
        return map;
    }

    private static bool IsSolid(char kind) => kind == '#' || kind == '%';

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
        block.AddComponent<BoxCollider2D>().size = new Vector2(width, height);
        var so = new SerializedObject(block.AddComponent<LevelBlock>());
        so.FindProperty("slippery").boolValue = ice;
        so.ApplyModifiedPropertiesWithoutUndo();
        Tiled(block.transform, "Body", ice ? art.ice : art.body, BodyOrder, new Vector2(width, height));
    }

    // The ground's top edge (grass, sand, snow...): 6 pixels, sticking 2 above the ground.
    private static void AddTop(Transform parent, int c, int r, int width, Sprite sprite)
    {
        SpriteRenderer top = Tiled(parent, "Top", sprite, TopOrder, new Vector2(width, 6f / Ppu));
        top.transform.localPosition = new Vector3(CellX(c) + width * 0.5f, CellTop(r) - 1f / Ppu, 0f);
    }

    // Half a cell tall, at the top of its cell.
    private static void AddPlatform(Transform parent, int c, int r, int width, ThemeArt art)
    {
        var platform = new GameObject("Platform");
        platform.transform.SetParent(parent, false);
        platform.transform.localPosition = new Vector3(CellX(c) + width * 0.5f, CellTop(r) - 0.25f, 0f);
        var box = platform.AddComponent<BoxCollider2D>();
        box.size = new Vector2(width, 0.5f);
        box.usedByEffector = true;
        var effector = platform.AddComponent<PlatformEffector2D>();
        effector.useOneWay = true;
        effector.surfaceArc = 180f;
        var so = new SerializedObject(platform.AddComponent<LevelBlock>());
        so.FindProperty("bulletsPassThrough").boolValue = true;
        so.ApplyModifiedPropertiesWithoutUndo();
        Tiled(platform.transform, "Body", art.plank, TopOrder, new Vector2(width, 0.5f));
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

    private static void AddBackground(Transform root, ThemeArt art)
    {
        Transform background = Child(root, "Background");
        SpriteRenderer sky = Sprite(background, "Sky", art.sky, SkyOrder);
        sky.transform.localScale = new Vector3(34f / (art.sky.rect.width / Ppu), 20f / (art.sky.rect.height / Ppu), 1f);
        Sprite(background, "Far", art.far, FarOrder);
        Sprite(background, "Near", art.near, NearOrder);
    }

    // ---------- Pixel art ----------

    private class ThemeArt
    {
        public Sprite body, top, plank, ice, iceTop, lava, lavaTop, spikes, sky, far, near;
        public Sprite[] decor;
    }

    private static ThemeArt PaintTheme(Theme theme)
    {
        string folder = ArtRoot + "/" + theme.name;
        var art = new ThemeArt
        {
            body = Save(folder, "Ground", PaintBody(theme.bodyStyle, theme.body, 1), new Vector2(0.5f, 0.5f)),
            top = Save(folder, "GroundTop", PaintTop(theme.topStyle, theme.top, 2), new Vector2(0.5f, 0.5f)),
            plank = Save(folder, "Platform", PaintPlank(theme.plankStyle, theme.plank), new Vector2(0.5f, 0.5f)),
            ice = Save(folder, "Ice", PaintBody(BodyStyle.Bricks, Ice, 3), new Vector2(0.5f, 0.5f)),
            iceTop = Save(folder, "IceTop", PaintTop(TopStyle.Smooth, IceTop, 4), new Vector2(0.5f, 0.5f)),
            lava = Save(folder, "Lava", PaintLava(false), new Vector2(0.5f, 0.5f)),
            lavaTop = Save(folder, "LavaTop", PaintLava(true), new Vector2(0.5f, 0.5f)),
            spikes = Save(folder, "Spikes", PaintSpikes(theme.spikes), new Vector2(0.5f, 0.5f)),
            sky = Save(folder, "Sky", PaintSky(theme), new Vector2(0.5f, 0.5f)),
            far = Save(folder, "Far", PaintSkyline(theme, true), new Vector2(0.5f, 0.5f)),
            near = Save(folder, "Near", PaintSkyline(theme, false), new Vector2(0.5f, 0.5f)),
            decor = new Sprite[theme.decor.Length],
        };
        for (int i = 0; i < theme.decor.Length; i++)
            art.decor[i] = Save(folder, "Decor_" + theme.decor[i], PaintDecor(theme.decor[i], theme.decorColors), new Vector2(0.5f, 0f));
        return art;
    }

    private static Sprite Save(string folder, string name, Pix pix, Vector2 pivot)
    {
        Directory.CreateDirectory(folder);
        string path = folder + "/" + name + ".png";
        File.WriteAllBytes(path, pix.ToTexture().EncodeToPNG());
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
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

    // 16 x 16, tiles seamlessly.
    private static Pix PaintBody(BodyStyle style, Color[] c, int seed)
    {
        var pix = new Pix(16, 16);
        switch (style)
        {
            case BodyStyle.Rock:
                for (int y = 0; y < 16; y++)
                    for (int x = 0; x < 16; x++)
                    {
                        float n = Noise(x, y, seed);
                        pix.Set(x, y, n < 0.1f ? c[0] : n > 0.92f ? c[2] : c[1]);
                    }
                // Two pebbles: light on top, dark under.
                foreach (Vector2Int stone in new[] { new Vector2Int(3, 9), new Vector2Int(10, 3) })
                {
                    pix.Rect(stone.x, stone.y + 1, 3, 1, c[2]);
                    pix.Rect(stone.x, stone.y, 3, 1, c[0]);
                }
                if (c.Length > 3) // glowing cracks
                {
                    pix.Line(1, 15, 4, 11, c[3]);
                    pix.Line(4, 11, 3, 7, c[3]);
                    pix.Line(12, 9, 14, 5, c[3]);
                }
                break;

            case BodyStyle.Bricks:
                for (int y = 0; y < 16; y++)
                    for (int x = 0; x < 16; x++)
                    {
                        int course = y / 4;
                        int shifted = (x + (course % 2) * 4) % 16;
                        int brick = shifted / 8;
                        float tint = Noise(brick, course, seed) * 0.35f - 0.12f;
                        Color face = tint > 0 ? Color.Lerp(c[1], c[2], tint) : Color.Lerp(c[1], c[0], -tint);
                        if (y % 4 == 0 || shifted % 8 == 0)
                            face = c[0]; // mortar
                        else if (y % 4 == 3)
                            face = Color.Lerp(face, c[2], 0.5f);
                        pix.Set(x, y, face);
                    }
                break;

            case BodyStyle.Panels:
                pix.Rect(0, 0, 16, 16, c[1]);
                pix.Rect(0, 0, 16, 1, c[0]);
                pix.Rect(0, 0, 1, 16, c[0]);
                pix.Rect(1, 14, 15, 1, Color.Lerp(c[1], c[2], 0.5f));
                pix.Rect(1, 1, 1, 14, Color.Lerp(c[1], c[2], 0.5f));
                foreach (Vector2Int rivet in new[] { new Vector2Int(3, 3), new Vector2Int(12, 3), new Vector2Int(3, 12), new Vector2Int(12, 12) })
                {
                    pix.Set(rivet.x, rivet.y, c[2]);
                    pix.Set(rivet.x, rivet.y - 1, c[0]);
                }
                break;
        }
        return pix;
    }

    // 16 x 6: the edge drawn on top of the ground (its lowest 4 pixels cover the ground's top).
    private static Pix PaintTop(TopStyle style, Color[] c, int seed)
    {
        var pix = new Pix(16, 6);
        for (int x = 0; x < 16; x++)
        {
            float n = Noise(x, 0, seed);
            switch (style)
            {
                case TopStyle.Tufts:
                    int blade = 3 + (n > 0.45f ? 1 : 0) + (n > 0.8f ? 1 : 0);
                    pix.Set(x, 0, n > 0.5f ? c[0] : Color.clear); // ragged roots
                    pix.Rect(x, 1, 1, blade - 1, c[1]);
                    pix.Set(x, blade, c[2]);
                    break;
                case TopStyle.Smooth:
                    pix.Set(x, 0, n > 0.5f ? c[0] : Color.clear);
                    pix.Rect(x, 1, 1, 3, c[1]);
                    if (Noise(x, 2, seed) > 0.8f)
                        pix.Set(x, 2, c[0]);
                    pix.Set(x, 4, c[2]);
                    break;
                case TopStyle.Snow:
                    int drip = n > 0.7f ? 0 : n > 0.4f ? 1 : 2; // icicles and lumps hanging down
                    pix.Rect(x, drip, 1, 2 - drip, c[0]);
                    pix.Rect(x, 2, 1, 2, c[1]);
                    pix.Set(x, 4, c[2]);
                    if (n > 0.85f)
                        pix.Set(x, 5, c[2]);
                    break;
                case TopStyle.Stripes:
                    pix.Set(x, 0, c[0]);
                    for (int y = 1; y <= 3; y++)
                        pix.Set(x, y, ((x + y) / 3) % 2 == 0 ? c[1] : c[0]);
                    pix.Set(x, 4, c[2]);
                    break;
            }
        }
        return pix;
    }

    // 16 x 8: half a unit tall.
    private static Pix PaintPlank(PlankStyle style, Color[] c)
    {
        var pix = new Pix(16, 8);
        if (style == PlankStyle.Wood)
        {
            pix.Rect(0, 1, 16, 6, c[1]);
            pix.Rect(0, 7, 16, 1, c[2]);
            pix.Rect(0, 0, 16, 1, c[0]);
            pix.Rect(0, 1, 1, 6, c[0]); // board seam
            pix.Rect(8, 1, 1, 3, c[0]);
            pix.Set(3, 5, c[0]); // nails
            pix.Set(12, 5, c[0]);
            for (int x = 1; x < 16; x++)
                if (Noise(x, 3, 7) > 0.8f)
                    pix.Set(x, 3, Color.Lerp(c[1], c[0], 0.5f));
        }
        else
        {
            pix.Rect(0, 6, 16, 2, c[1]);
            pix.Rect(0, 7, 16, 1, c[2]);
            pix.Rect(0, 0, 16, 2, c[1]);
            pix.Rect(0, 0, 16, 1, c[0]);
            for (int x = 0; x < 16; x++)
                for (int y = 2; y < 6; y++)
                    pix.Set(x, y, x % 4 == 0 || y == 4 ? c[1] : new Color(c[0].r, c[0].g, c[0].b, 0.6f)); // see-through mesh
        }
        return pix;
    }

    // 16 x 16 of lava; the top one has a wavy surface 3 pixels under its top.
    private static Pix PaintLava(bool surface)
    {
        var pix = new Pix(16, 16);
        for (int y = 0; y < 16; y++)
            for (int x = 0; x < 16; x++)
            {
                float n = Noise(x, y, 11);
                Color color = n < 0.15f ? Lava[0] : n > 0.9f ? Lava[2] : Lava[1];
                pix.Set(x, y, color);
            }
        if (surface)
        {
            for (int x = 0; x < 16; x++)
            {
                int level = 12 + Mathf.RoundToInt(Mathf.Sin(x / 16f * Mathf.PI * 2f));
                for (int y = level + 1; y < 16; y++)
                    pix.Set(x, y, Color.clear);
                pix.Set(x, level, Lava[3]);
                pix.Set(x, level - 1, Lava[2]);
                pix.Set(x, level - 2, Lava[2]);
            }
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
                pix.Set(s * 8 + x, 0, c[0]);
            }
        pix.Rect(0, 0, 16, 1, c[0]);
        return pix;
    }

    // A tall strip, stretched over the screen: the sky in 8 bands.
    private static Pix PaintSky(Theme theme)
    {
        var pix = new Pix(4, 64);
        for (int y = 0; y < 64; y++)
        {
            float band = Mathf.Floor(y / 64f * 8f) / 7f;
            pix.Rect(0, y, 4, 1, Color.Lerp(theme.skyBottom, theme.skyTop, band));
        }
        return pix;
    }

    // 34 x 20 units: hills, trees, buildings... in one flat color; the far one also has the stars and the sun or moon.
    private static Pix PaintSkyline(Theme theme, bool far)
    {
        const int width = 34 * Ppu, height = 20 * Ppu;
        var pix = new Pix(width, height);
        int seed = far ? 21 : 37;
        if (far)
        {
            if (theme.stars)
                for (int y = height / 2; y < height; y++)
                    for (int x = 0; x < width; x++)
                        if (Noise(x, y, 5) > 0.997f)
                            pix.Set(x, y, new Color(1f, 1f, 1f, Noise(x, y, 6) * 0.5f + 0.3f));
            if (theme.orb.a > 0f)
            {
                pix.Disc(width * 0.78f, height * 0.78f, 22f, new Color(theme.orb.r, theme.orb.g, theme.orb.b, 0.25f));
                pix.Disc(width * 0.78f, height * 0.78f, 16f, theme.orb);
            }
        }
        Color color = far ? theme.far : theme.near;
        for (int x = 0; x < width; x++)
        {
            int top = SkylineHeight(theme.skyline, x, seed, far);
            for (int y = 0; y < top; y++)
                pix.Set(x, y, color);
            if (theme.skyline == Skyline.Peaks && far) // snowy tips
                for (int y = Mathf.Max(0, top - 6); y < top; y++)
                    if (top > 150)
                        pix.Set(x, y, Color.Lerp(color, Color.white, 0.55f));
        }
        return pix;
    }

    private static int SkylineHeight(Skyline skyline, int x, int seed, bool far)
    {
        float baseHeight = far ? 110f : 60f;
        float scale = far ? 1f : 0.8f;
        switch (skyline)
        {
            case Skyline.Pines:
            {
                int period = far ? 14 : 20;
                int tree = (x + seed) / period;
                float treeHeight = (far ? 26f : 40f) + Noise(tree, 0, seed) * 26f;
                float along = Mathf.Abs((x + seed) % period - period * 0.5f) / (period * 0.5f);
                return Mathf.RoundToInt(baseHeight + 10f * Mathf.Sin(x * 0.01f + seed) + treeHeight * (1f - along));
            }
            case Skyline.Dunes:
                return Mathf.RoundToInt(baseHeight - 20f + 26f * scale * Mathf.Sin(x * 0.012f + seed) + 12f * Mathf.Sin(x * 0.031f + seed * 2));
            case Skyline.City:
            {
                int building = (x + seed * 3) / 26;
                float rise = Noise(building, 1, seed) * (far ? 110f : 70f);
                bool chimney = (x + seed * 3) % 26 >= 5 && (x + seed * 3) % 26 < 9 && Noise(building, 2, seed) > 0.5f;
                return Mathf.RoundToInt(baseHeight - 30f + rise + (chimney ? 34f : 0f));
            }
            case Skyline.Hills:
                return Mathf.RoundToInt(baseHeight - 20f + 22f * Mathf.Sin(x * 0.018f + seed) + 9f * Mathf.Sin(x * 0.05f + seed));
            case Skyline.Volcano:
            {
                if (!far)
                    return Mathf.RoundToInt(baseHeight + 14f * Mathf.Sin(x * 0.04f + seed) + Noise(x / 6, 0, seed) * 10f);
                float fromTop = Mathf.Abs(x - 34 * Ppu * 0.5f);
                float cone = 230f - fromTop * 0.95f;
                if (fromTop < 18f)
                    cone = 212f; // the crater
                return Mathf.RoundToInt(Mathf.Max(baseHeight - 30f + 10f * Mathf.Sin(x * 0.03f), cone));
            }
            default: // Peaks
            {
                int period = far ? 128 : 90;
                int peak = (x + seed * 5) / period;
                float along = Mathf.Abs((x + seed * 5) % period - period * 0.5f) / (period * 0.5f);
                float peakHeight = (far ? 90f : 50f) * (0.6f + Noise(peak, 0, seed) * 0.6f);
                return Mathf.RoundToInt(baseHeight - 20f + peakHeight * (1f - along));
            }
        }
    }

    // Decorations: c = the theme's dark, mid, light and accent colors.
    private static Pix PaintDecor(DecorKind kind, Color[] c)
    {
        Color outline = H("#14121c");
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
                pix.Rect(5, 0, 4, 22, c[3]);
                pix.Rect(6, 22, 2, 1, c[3]);
                pix.Rect(1, 9, 2, 7, c[3]);
                pix.Rect(3, 9, 2, 2, c[3]);
                pix.Rect(11, 12, 2, 7, c[3]);
                pix.Rect(9, 12, 2, 2, c[3]);
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
        pix.Outline(outline);
        return pix;
    }
}
