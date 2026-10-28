using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Editor helper that builds the game's UI pieces in one look (pixel panels, raised buttons, inset fields,
// one pixel font, VT323, for everything). Used when (re)building menus from editor scripts;
// not part of the game itself.
public static class UiKit
{
    public enum ButtonKind { Normal, Primary, Danger, Subtle }

    public static readonly Color Orange = new Color(1f, 0.42f, 0.21f);
    public static readonly Color Gold = new Color(1f, 0.85f, 0.3f);
    public static readonly Color TextColor = new Color(0.93f, 0.94f, 1f);
    public static readonly Color Muted = new Color(0.74f, 0.77f, 0.9f);
    public static readonly Color Good = new Color(0.4f, 0.9f, 0.5f);
    public static readonly Color Bad = new Color(1f, 0.4f, 0.4f);
    public static readonly Color Backdrop = new Color(0.03f, 0.03f, 0.06f, 0.78f);

    public static Font PixelFont => AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/VT323-Regular.ttf");
    // One pixel font everywhere (titles are just bigger), so every screen looks like the same game.
    public static Font TitleFont => PixelFont;
    public static Sprite ButtonSprite => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/Button.png");
    public static Sprite PanelSprite => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/Panel.png");
    public static Sprite InputSprite => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/Input.png");
    public static Sprite PixelSprite => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/Pixel.png");
    public static Sprite Crown => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Sprites/UI/Crown.png");

    // Every 9-sliced sprite draws one of its pixels as 5 UI pixels (16 px per unit, 100 UI px per unit / 1.25),
    // so all borders in the game have the same pixel size.
    public const float SliceMultiplier = 1.25f;

    // VT323 is small for its size: titles and button labels are scaled up by these, and small text (under
    // SmallTextSize) by SmallTextScale so it stays readable.
    private const int SmallTextSize = 40;
    private const float SmallTextScale = 1.15f;
    private const float TitleScale = 1.3f;
    private const float ButtonTextScale = 1.45f;

    // ---------- Layout ----------

    // position = where the middle of the piece goes, measured from the anchor.
    public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
    {
        return Rect(parent, name, anchor, new Vector2(0.5f, 0.5f), position, size);
    }

    public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        return rect;
    }

    // Fills its parent.
    public static RectTransform Stretch(Transform parent, string name)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }

    public static Vector2 Center => new Vector2(0.5f, 0.5f);

    // ---------- Pieces ----------

    // Dark see-through layer over the whole screen (behind menus and boxes).
    public static Image Dim(Transform parent, string name, float alpha)
    {
        Image image = Stretch(parent, name).gameObject.AddComponent<Image>();
        image.sprite = PixelSprite;
        image.color = new Color(Backdrop.r, Backdrop.g, Backdrop.b, alpha);
        return image;
    }

    public static Image Panel(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size)
    {
        Image image = Rect(parent, name, anchor, position, size).gameObject.AddComponent<Image>();
        image.sprite = PanelSprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = SliceMultiplier;
        return image;
    }

    public static Image Box(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size, Color color)
    {
        Image image = Rect(parent, name, anchor, position, size).gameObject.AddComponent<Image>();
        image.sprite = PixelSprite;
        image.color = color;
        return image;
    }

    public static Image Icon(Transform parent, string name, Sprite sprite, Vector2 anchor, Vector2 position, Vector2 size)
    {
        Image image = Rect(parent, name, anchor, position, size).gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;
        return image;
    }

    // vietnamese == null: a text set from code (no LocalizedText).
    public static Text Label(Transform parent, string name, string english, string vietnamese, int fontSize, Color color,
                             TextAnchor align, Vector2 anchor, Vector2 position, Vector2 size, bool title = false)
    {
        Text text = Rect(parent, name, anchor, position, size).gameObject.AddComponent<Text>();
        text.font = PixelFont;
        text.fontSize = title ? Mathf.RoundToInt(fontSize * TitleScale)
                      : fontSize < SmallTextSize ? Mathf.RoundToInt(fontSize * SmallTextScale) : fontSize;
        text.color = color;
        text.alignment = align;
        text.text = english;
        text.supportRichText = true;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        if (vietnamese != null)
            Localize(text, english, vietnamese);
        if (title)
            Shadow(text, new Vector2(5f, -5f));
        return text;
    }

    public static void Localize(Text text, string english, string vietnamese)
    {
        LocalizedText localized = text.GetComponent<LocalizedText>();
        if (localized == null)
            localized = text.gameObject.AddComponent<LocalizedText>();
        var so = new SerializedObject(localized);
        so.FindProperty("english").stringValue = english;
        so.FindProperty("vietnamese").stringValue = vietnamese;
        so.ApplyModifiedPropertiesWithoutUndo();
        text.text = english;
    }

    public static void Shadow(Graphic graphic, Vector2 offset)
    {
        var shadow = graphic.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
        shadow.effectDistance = offset;
    }

    public static Color Tint(ButtonKind kind)
    {
        switch (kind)
        {
            case ButtonKind.Primary: return new Color(1f, 0.5f, 0.26f);
            case ButtonKind.Danger: return new Color(0.85f, 0.3f, 0.32f);
            case ButtonKind.Subtle: return new Color(0.26f, 0.29f, 0.4f);
            default: return new Color(0.36f, 0.42f, 0.62f);
        }
    }

    public static Button Button(Transform parent, string name, string english, string vietnamese, Vector2 anchor,
                                Vector2 position, Vector2 size, ButtonKind kind = ButtonKind.Normal, int fontSize = 30)
    {
        RectTransform rect = Rect(parent, name, anchor, position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = ButtonSprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = SliceMultiplier;
        image.color = Tint(kind);

        Button button = rect.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = new Color(0.9f, 0.9f, 0.9f);
        colors.highlightedColor = Color.white;
        colors.selectedColor = new Color(0.9f, 0.9f, 0.9f);
        colors.pressedColor = new Color(0.7f, 0.7f, 0.7f);
        colors.disabledColor = new Color(0.55f, 0.55f, 0.6f, 0.55f);
        colors.fadeDuration = 0.06f;
        button.colors = colors;
        Navigation navigation = button.navigation;
        navigation.mode = Navigation.Mode.None; // no stuck "selected" look after a click
        button.navigation = navigation;
        rect.gameObject.AddComponent<UiButton>();

        // Raised a little: the bottom of the sprite is the button's thickness.
        Text label = Label(rect, "Label", english, vietnamese, Mathf.RoundToInt(fontSize * ButtonTextScale), Color.white,
                           TextAnchor.MiddleCenter, Center, new Vector2(0f, 6f), size - new Vector2(24f, 16f), false);
        Shadow(label, new Vector2(3f, -3f));
        return button;
    }

    public static InputField Input(Transform parent, string name, string placeholderEnglish, string placeholderVietnamese,
                                   Vector2 anchor, Vector2 position, Vector2 size, int fontSize = 40)
    {
        RectTransform rect = Rect(parent, name, anchor, position, size);
        Image image = rect.gameObject.AddComponent<Image>();
        image.sprite = InputSprite;
        image.type = Image.Type.Sliced;
        image.pixelsPerUnitMultiplier = SliceMultiplier;

        Vector2 inner = size - new Vector2(40f, 12f);
        Text placeholder = Label(rect, "Placeholder", placeholderEnglish, placeholderVietnamese, fontSize,
                                 new Color(Muted.r, Muted.g, Muted.b, 0.55f), TextAnchor.MiddleLeft, Center, Vector2.zero, inner);
        Text text = Label(rect, "Text", "", null, fontSize, TextColor, TextAnchor.MiddleLeft, Center, Vector2.zero, inner);
        text.supportRichText = false;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;

        InputField input = rect.gameObject.AddComponent<InputField>();
        input.textComponent = text;
        input.placeholder = placeholder;
        input.caretColor = Gold;
        input.caretWidth = 3;
        input.customCaretColor = true;
        input.selectionColor = new Color(1f, 0.6f, 0.2f, 0.45f);
        Navigation navigation = input.navigation;
        navigation.mode = Navigation.Mode.None;
        input.navigation = navigation;
        return input;
    }

    // ---------- Wiring ----------

    public static void Wire(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        if (property == null)
            throw new System.Exception(target.GetType().Name + " has no field " + field);
        property.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void WireArray(Object target, string field, Object[] values)
    {
        var so = new SerializedObject(target);
        SerializedProperty property = so.FindProperty(field);
        if (property == null)
            throw new System.Exception(target.GetType().Name + " has no field " + field);
        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // For [Serializable] row classes: sets field "sub" of element i of array "field".
    public static void WireRow(Object target, string field, int index, string sub, Object value)
    {
        var so = new SerializedObject(target);
        SerializedProperty array = so.FindProperty(field);
        if (array.arraySize <= index)
            array.arraySize = index + 1;
        SerializedProperty property = array.GetArrayElementAtIndex(index).FindPropertyRelative(sub);
        if (property == null)
            throw new System.Exception(field + "[" + index + "] has no field " + sub);
        property.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    public static void Clear(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(parent.GetChild(i).gameObject);
    }
}
