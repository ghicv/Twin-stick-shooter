using UnityEngine;

// The skins LAN players choose from (part of the scene): a body color and a small pixel accessory on the head.
// Each skin can only be worn by one player at a time, so everybody stays easy to tell apart.
public class SkinCatalog : MonoBehaviour
{
    [System.Serializable]
    public class Skin
    {
        public string name;
        public Color color = Color.white;

        [Tooltip("Drawn over the body (pivot at the bottom center = the body's center). Optional.")]
        public Sprite accessory;
    }

    [SerializeField] private Skin[] skins;

    public static SkinCatalog Instance { get; private set; }

    public int Count => skins.Length;

    private void Awake()
    {
        Instance = this;
    }

    public Skin Get(int index) => index >= 0 && index < skins.Length ? skins[index] : null;
}
