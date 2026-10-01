using System.Collections.Generic;
using UnityEngine;

// The cores the player owns this run, plus the catalog of every core with its card info (name, text, icon).
// A new run loads the scene again, so the player always starts with no cores.
public class CoreInventory : MonoBehaviour
{
    [System.Serializable]
    public class CoreInfo
    {
        public CoreType type;
        public string title;

        [Tooltip("Small line under the title, e.g. \"BULLET · FLY\" or \"SINGLE · PLAYER\".")]
        public string tag;

        [TextArea(2, 4)]
        public string description;

        public Sprite icon;

        [Tooltip("Bullet cores stack with each other. Every card offer has at least one (while any is left).")]
        public bool bulletCore;
    }

    [Tooltip("Every core in the game. A core missing from this list is never offered.")]
    [SerializeField] private CoreInfo[] catalog;

    private readonly List<CoreType> owned = new List<CoreType>();

    public IReadOnlyList<CoreType> Owned => owned;
    public CoreInfo[] Catalog => catalog;

    // A core was just added (e.g. Thick Skin raises max health once, right then).
    public event System.Action<CoreType> Added;

    public bool Has(CoreType type) => owned.Contains(type);

    public void Add(CoreType type)
    {
        if (owned.Contains(type))
            return;
        owned.Add(type);
        Added?.Invoke(type);
    }

    public CoreInfo Info(CoreType type)
    {
        foreach (CoreInfo info in catalog)
            if (info.type == type)
                return info;
        return null;
    }
}
