using UnityEngine;

// Everything a player can wear (part of the scene): hats, outfits and skin colors. A player's look is one choice of
// each (Look), picked in the character screen (CharacterPanel), saved on this PC and sent to the others as one number.
public class Wardrobe : MonoBehaviour
{
    [System.Serializable]
    public class Item
    {
        public string english;
        public string vietnamese;

        [Tooltip("Drawn over the body (pivot = the body's middle). Empty = nothing (\"no hat\").")]
        public Sprite sprite;

        public string Name => Lang.T(english, vietnamese);
    }

    [System.Serializable]
    public class SkinColor
    {
        public string english;
        public string vietnamese;
        public Color color = Color.white;

        public string Name => Lang.T(english, vietnamese);
    }

    [Tooltip("The first one is \"no hat\".")]
    [SerializeField] private Item[] hats;

    [Tooltip("The first one is \"no outfit\".")]
    [SerializeField] private Item[] outfits;

    [SerializeField] private SkinColor[] colors;

    public static Wardrobe Instance { get; private set; }

    public int HatCount => hats.Length;
    public int OutfitCount => outfits.Length;
    public int ColorCount => colors.Length;

    private void Awake()
    {
        Instance = this;
    }

    public Item Hat(int index) => hats[Mathf.Clamp(index, 0, hats.Length - 1)];
    public Item Outfit(int index) => outfits[Mathf.Clamp(index, 0, outfits.Length - 1)];
    public SkinColor ColorAt(int index) => colors[Mathf.Clamp(index, 0, colors.Length - 1)];

    public Look Random() => new Look(UnityEngine.Random.Range(0, hats.Length), UnityEngine.Random.Range(0, outfits.Length),
                                     UnityEngine.Random.Range(0, colors.Length));
}

// One player's look: a hat, an outfit and a skin color (indexes into the Wardrobe).
// Sent over the network as one number (Pack / Unpack).
public struct Look
{
    public int hat;
    public int outfit;
    public int color;

    public Look(int hat, int outfit, int color)
    {
        this.hat = hat;
        this.outfit = outfit;
        this.color = color;
    }

    public int Pack() => (hat & 0xFF) << 16 | (outfit & 0xFF) << 8 | (color & 0xFF);

    public static Look Unpack(int packed) => new Look((packed >> 16) & 0xFF, (packed >> 8) & 0xFF, packed & 0xFF);
}
