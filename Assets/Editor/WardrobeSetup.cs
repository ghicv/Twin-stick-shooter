using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Tools > Setup Wardrobe: draws the outfits (pixel clothes cut to the player's body shape, so nothing sticks out),
// fills the scene's Wardrobe (hats, outfits, skin colors with English and Vietnamese names) and gives the Player
// prefab its Outfit and Hat layers (body, then outfit, then hat, then the gun).
public static class WardrobeSetup
{
    private const string OutfitFolder = "Assets/Sprites/Pixel/Outfits";

    [MenuItem("Tools/Setup Wardrobe")]
    public static void Run()
    {
        List<Sprite> outfits = DrawOutfits();
        SetupScene(outfits);
        SetupPlayerPrefab();
    }

    // ---------- Outfits ----------

    private static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color color);
        return color;
    }

    // One outfit: its name, and the color it puts on body pixel (x, y from the top), or null to leave the skin.
    private class Outfit
    {
        public string english;
        public string vietnamese;
        public System.Func<int, int, Color?> paint;
    }

    private static List<Outfit> Outfits()
    {
        Color red = Hex("D9413B"), blue = Hex("3B6FD9"), yellow = Hex("F2C14E"), black = Hex("2A2A35"), white = Hex("EDEDED");
        Color green = Hex("4CAF6A"), ninja = Hex("2B2140"), silver = Hex("AEB6C8"), brown = Hex("6B4423"), gold = Hex("FFD84D");
        Color denim = Hex("3D5A99");
        return new List<Outfit>
        {
            new Outfit { english = "T-SHIRT", vietnamese = "ÁO THUN", paint = (x, y) => y >= 9 && y <= 14 ? (y == 9 && x >= 4 && x <= 7 ? Darker(red) : red) : (Color?)null },
            new Outfit { english = "OVERALLS", vietnamese = "QUẦN YẾM", paint = (x, y) =>
                y >= 13 ? blue
                : y >= 9 && (x == 3 || x == 8) ? (y == 12 ? yellow : blue)
                : (Color?)null },
            new Outfit { english = "SUIT", vietnamese = "COM-LÊ", paint = (x, y) =>
                y < 9 ? (Color?)null
                : (x == 5 || x == 6) && y <= 13 ? (y == 9 ? white : Hex("C0392B"))
                : (x == 4 || x == 7) && y <= 11 ? white
                : black },
            new Outfit { english = "HOODIE", vietnamese = "ÁO HOODIE", paint = (x, y) =>
                y == 8 ? Darker(green)
                : y > 8 && y <= 18 ? (y >= 15 && y <= 16 && x >= 4 && x <= 7 ? Darker(green) : green)
                : (Color?)null },
            new Outfit { english = "NINJA", vietnamese = "NINJA", paint = (x, y) =>
                y >= 5 && y <= 7 ? (y == 6 && (x == 4 || x == 7) ? white : ninja)
                : y == 14 ? Hex("C0392B")
                : y >= 9 ? ninja
                : (Color?)null },
            new Outfit { english = "SCARF", vietnamese = "KHĂN QUÀNG", paint = (x, y) =>
                y == 8 || y == 9 ? (x % 3 == 0 ? Darker(yellow) : yellow)
                : (x == 8 || x == 9) && y >= 10 && y <= 14 ? yellow
                : (Color?)null },
            new Outfit { english = "ARMOR", vietnamese = "GIÁP", paint = (x, y) =>
                y >= 8 && y <= 15 ? ((y == 9 || y == 14) && x % 2 == 1 ? Darker(silver) : x <= 3 ? Color.Lerp(silver, Color.white, 0.4f) : silver)
                : y == 16 ? brown
                : (Color?)null },
            new Outfit { english = "JEANS", vietnamese = "QUẦN JEAN", paint = (x, y) =>
                y == 13 ? ((x == 5 || x == 6) ? gold : brown)
                : y >= 14 ? (x == 5 || x == 6 ? Darker(denim) : denim)
                : (Color?)null },
        };
    }

    private static Color Darker(Color color) => Color.Lerp(color, Color.black, 0.3f);

    // Draws every outfit over the body's shape: the body's dark edge and shading stay on the clothes too.
    private static List<Sprite> DrawOutfits()
    {
        System.IO.Directory.CreateDirectory(OutfitFolder);
        var body = new Texture2D(2, 2);
        body.LoadImage(System.IO.File.ReadAllBytes("Assets/Sprites/Pixel/Capsule.png"));
        int w = body.width, h = body.height;

        var sprites = new List<Sprite>();
        foreach (Outfit outfit in Outfits())
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    Color skin = body.GetPixel(x, y);
                    Color? cloth = skin.a > 0.1f ? outfit.paint(x, h - 1 - y) : null;
                    if (cloth == null)
                    {
                        tex.SetPixel(x, y, new Color(0, 0, 0, 0));
                        continue;
                    }
                    // The body sprite is white with grey edges and shading: keep that light on the cloth.
                    float light = skin.r;
                    tex.SetPixel(x, y, new Color(cloth.Value.r * light, cloth.Value.g * light, cloth.Value.b * light, 1f));
                }
            tex.Apply();
            string path = OutfitFolder + "/Outfit_" + outfit.english.Replace(" ", "").Replace("-", "") + ".png";
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = 16;
            importer.spritePivot = new Vector2(0.5f, 0.5f); // same as the body
            importer.SaveAndReimport();
            sprites.Add(AssetDatabase.LoadAssetAtPath<Sprite>(path));
        }
        return sprites;
    }

    // ---------- Scene ----------

    private static void SetupScene(List<Sprite> outfitSprites)
    {
        GameObject old = GameObject.Find("Skins");
        if (old != null)
            Object.DestroyImmediate(old);
        GameObject go = GameObject.Find("Wardrobe");
        if (go == null)
            go = new GameObject("Wardrobe");
        Wardrobe wardrobe = go.GetComponent<Wardrobe>();
        if (wardrobe == null)
            wardrobe = go.AddComponent<Wardrobe>();
        var so = new SerializedObject(wardrobe);

        string[,] hats =
        {
            { "NO HAT", "KHÔNG ĐỘI", "" },
            { "CAP", "MŨ LƯỠI TRAI", "Skin_Cap" },
            { "BEANIE", "MŨ LEN", "Skin_Beanie" },
            { "CROWN", "VƯƠNG MIỆN", "Skin_Crown" },
            { "HORNS", "SỪNG", "Skin_Horns" },
            { "HALO", "HÀO QUANG", "Skin_Halo" },
            { "HEADBAND", "BĂNG ĐÔ", "Skin_Headband" },
            { "BOW", "NƠ", "Skin_Bow" },
            { "SHADES", "KÍNH RÂM", "Skin_Shades" },
        };
        SerializedProperty hatArray = so.FindProperty("hats");
        hatArray.arraySize = hats.GetLength(0);
        for (int i = 0; i < hats.GetLength(0); i++)
        {
            SerializedProperty item = hatArray.GetArrayElementAtIndex(i);
            item.FindPropertyRelative("english").stringValue = hats[i, 0];
            item.FindPropertyRelative("vietnamese").stringValue = hats[i, 1];
            item.FindPropertyRelative("sprite").objectReferenceValue = hats[i, 2].Length == 0 ? null
                : AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/Pixel/Skins/" + hats[i, 2] + ".png");
        }

        List<Outfit> outfits = Outfits();
        SerializedProperty outfitArray = so.FindProperty("outfits");
        outfitArray.arraySize = outfits.Count + 1;
        SerializedProperty none = outfitArray.GetArrayElementAtIndex(0);
        none.FindPropertyRelative("english").stringValue = "NO OUTFIT";
        none.FindPropertyRelative("vietnamese").stringValue = "KHÔNG MẶC";
        none.FindPropertyRelative("sprite").objectReferenceValue = null;
        for (int i = 0; i < outfits.Count; i++)
        {
            SerializedProperty item = outfitArray.GetArrayElementAtIndex(i + 1);
            item.FindPropertyRelative("english").stringValue = outfits[i].english;
            item.FindPropertyRelative("vietnamese").stringValue = outfits[i].vietnamese;
            item.FindPropertyRelative("sprite").objectReferenceValue = outfitSprites[i];
        }

        string[,] colors =
        {
            { "ORANGE", "CAM", "FF6B35" }, { "TEAL", "XANH NGỌC", "4ECDC4" }, { "YELLOW", "VÀNG", "FFE66D" },
            { "PURPLE", "TÍM", "C77DFF" }, { "MINT", "XANH BẠC HÀ", "7DDC8F" }, { "PINK", "HỒNG", "FF85B3" },
            { "SKY", "XANH TRỜI", "6FB7FF" }, { "GRAY", "XÁM", "B8BCC8" }, { "RED", "ĐỎ", "FF4D4D" },
            { "BROWN", "NÂU", "B07A4F" }, { "WHITE", "TRẮNG", "F2F2F2" }, { "LIME", "XANH CHANH", "C6F04A" },
        };
        SerializedProperty colorArray = so.FindProperty("colors");
        colorArray.arraySize = colors.GetLength(0);
        for (int i = 0; i < colors.GetLength(0); i++)
        {
            SerializedProperty item = colorArray.GetArrayElementAtIndex(i);
            item.FindPropertyRelative("english").stringValue = colors[i, 0];
            item.FindPropertyRelative("vietnamese").stringValue = colors[i, 1];
            item.FindPropertyRelative("color").colorValue = Hex(colors[i, 2]);
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(go.scene);
    }

    // ---------- Player prefab ----------

    private static void SetupPlayerPrefab()
    {
        const string path = "Assets/Prefabs/Player.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        Transform body = root.transform.Find("Body");

        // The gun and everything drawn over the body move up one, to make room: body 10, outfit 11, hat 12.
        foreach (SpriteRenderer renderer in root.GetComponentsInChildren<SpriteRenderer>(true))
            if (renderer.transform != body && renderer.sortingOrder >= 11 && renderer.name != "Outfit" && renderer.name != "Hat" &&
                renderer.name != "Accessory")
                renderer.sortingOrder = Mathf.Max(renderer.sortingOrder, 13);

        Transform hat = body.Find("Accessory");
        if (hat == null)
            hat = body.Find("Hat");
        hat.name = "Hat";
        hat.GetComponent<SpriteRenderer>().sortingOrder = 12;

        Transform outfit = body.Find("Outfit");
        if (outfit == null)
        {
            outfit = new GameObject("Outfit").transform;
            outfit.SetParent(body, false);
            outfit.gameObject.AddComponent<SpriteRenderer>();
        }
        outfit.SetSiblingIndex(0);
        outfit.GetComponent<SpriteRenderer>().sortingOrder = 11;

        var net = root.GetComponent<PlayerNetwork>();
        var so = new SerializedObject(net);
        so.FindProperty("outfit").objectReferenceValue = outfit.GetComponent<SpriteRenderer>();
        so.FindProperty("hat").objectReferenceValue = hat.GetComponent<SpriteRenderer>();
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, path);
        PrefabUtility.UnloadPrefabContents(root);
    }
}
