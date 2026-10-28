using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Draws the menu's pixel art (Tools > Generate Menu Art) into Assets/Sprites/UI/Menu:
// - Logo: "PIXEL FIGHT" in chunky block letters with a gradient, a highlight, a 3D side and a dark outline.
// Shown with point filtering, so every pixel stays crisp.
public static class MenuArtGenerator
{
    private const string Folder = "Assets/Sprites/UI/Menu";

    [MenuItem("Tools/Generate Menu Art")]
    public static void GenerateAll()
    {
        System.IO.Directory.CreateDirectory(Folder);
        Save("Logo", Logo(), false);
        AssetDatabase.Refresh();
    }

    private static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out Color color);
        return color;
    }

    // ---------- Logo ----------

    private static readonly Dictionary<char, string[]> Glyphs = new Dictionary<char, string[]>
    {
        { 'P', new[] { "####.", "#...#", "#...#", "####.", "#....", "#....", "#...." } },
        { 'I', new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "#####" } },
        { 'X', new[] { "#...#", "#...#", ".#.#.", "..#..", ".#.#.", "#...#", "#...#" } },
        { 'E', new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#####" } },
        { 'L', new[] { "#....", "#....", "#....", "#....", "#....", "#....", "#####" } },
        { 'F', new[] { "#####", "#....", "#....", "####.", "#....", "#....", "#...." } },
        { 'G', new[] { ".####", "#....", "#....", "#.###", "#...#", "#...#", ".####" } },
        { 'H', new[] { "#...#", "#...#", "#...#", "#####", "#...#", "#...#", "#...#" } },
        { 'T', new[] { "#####", "..#..", "..#..", "..#..", "..#..", "..#..", "..#.." } },
    };

    private static Texture2D Logo()
    {
        const int block = 3;   // one glyph dot = 3 x 3 pixels
        const int gap = 3;     // between letters
        const int lineGap = 7;
        const int depth = 4;   // the 3D side
        const int outline = 2;
        string[] lines = { "PIXEL", "FIGHT" };
        int glyphW = 5 * block, glyphH = 7 * block;
        int textW = 5 * glyphW + 4 * gap;
        int textH = lines.Length * glyphH + (lines.Length - 1) * lineGap;
        int w = textW + depth + outline * 2 + 2, h = textH + depth + outline * 2 + 2;

        // Which pixels are letter (face).
        var face = new bool[w, h];
        for (int line = 0; line < lines.Length; line++)
            for (int c = 0; c < lines[line].Length; c++)
            {
                string[] glyph = Glyphs[lines[line][c]];
                int ox = outline + 1 + c * (glyphW + gap);
                int oyTop = outline + 1 + line * (glyphH + lineGap); // from the top
                for (int gy = 0; gy < 7; gy++)
                    for (int gx = 0; gx < 5; gx++)
                        if (glyph[gy][gx] == '#')
                            for (int by = 0; by < block; by++)
                                for (int bx = 0; bx < block; bx++)
                                    face[ox + gx * block + bx, h - 1 - (oyTop + gy * block + by)] = true;
            }

        // The 3D side: the face pushed down-right.
        var side = new bool[w, h];
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                if (face[x, y])
                    for (int d = 1; d <= depth; d++)
                        if (x + d < w && y - d >= 0)
                            side[x + d, y - d] = true;

        Color outlineColor = Hex("1A0E22");
        Color[] gradient = { Hex("FFF1A8"), Hex("FFD84D"), Hex("FFB23F"), Hex("FF8A3D"), Hex("F2603A"), Hex("D9432F") };
        Color sideTop = Hex("9C2B2B"), sideBottom = Hex("5E1630");
        var tex = NewTexture(w, h);
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                if (face[x, y])
                {
                    // Gradient by height inside its own line of text (6 bands); the top row of each dot is lighter.
                    int fromTop = h - 1 - y - (outline + 1);
                    int lineIndex = fromTop / (glyphH + lineGap);
                    int inLine = fromTop - lineIndex * (glyphH + lineGap);
                    int band = Mathf.Clamp(inLine * gradient.Length / glyphH, 0, gradient.Length - 1);
                    Color color = gradient[band];
                    if (y + 1 < h && !face[x, y + 1])
                        color = Color.Lerp(color, Color.white, 0.55f);
                    tex.SetPixel(x, y, color);
                }
                else if (side[x, y])
                {
                    tex.SetPixel(x, y, Color.Lerp(sideTop, sideBottom, 1f - (float)y / h));
                }
            }
        // Outline around face + side.
        var filled = new bool[w, h];
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
                filled[x, y] = face[x, y] || side[x, y];
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                if (filled[x, y])
                    continue;
                bool near = false;
                for (int dx = -outline; dx <= outline && !near; dx++)
                    for (int dy = -outline; dy <= outline && !near; dy++)
                        if (x + dx >= 0 && x + dx < w && y + dy >= 0 && y + dy < h && filled[x + dx, y + dy] &&
                            Mathf.Abs(dx) + Mathf.Abs(dy) <= outline + 1)
                            near = true;
                if (near)
                    tex.SetPixel(x, y, outlineColor);
            }
        tex.Apply();
        return tex;
    }

    // ---------- Files ----------

    private static Texture2D NewTexture(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var clear = new Color[w * h];
        tex.SetPixels(clear);
        return tex;
    }

    private static void Save(string name, Texture2D tex, bool repeat)
    {
        string path = Folder + "/" + name + ".png";
        System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = repeat ? TextureWrapMode.Repeat : TextureWrapMode.Clamp;
        importer.spritePixelsPerUnit = 16;
        importer.SaveAndReimport();
    }
}
