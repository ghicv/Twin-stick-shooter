using UnityEngine;
using UnityEngine.UI;

// The big sign on the lobby's back wall: the room's name, and the address friends can type to join
// (the room also shows up by itself in their LAN list).
public class RoomSign : MonoBehaviour
{
    [SerializeField] private Text nameText;
    [SerializeField] private Text addressText;

    private void Update()
    {
        MatchManager match = MatchManager.Instance;
        nameText.text = match != null ? match.RoomName : "";
        addressText.text = match != null && GameMode.RoomAddress.Length > 0 ? "IP " + GameMode.RoomAddress : "";
    }
}
