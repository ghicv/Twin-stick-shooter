// The player's controls are off while a menu takes the keyboard: the ESC menu, or typing in the chat.
public static class InputLock
{
    public static bool Blocked => SettingsMenu.IsOpen || ChatBox.Typing;

    // LAN match, the round is decided (or not started yet, or the results are up): every player stands frozen where
    // they are, no moving, no shooting, no falling off the map.
    public static bool Frozen
    {
        get
        {
            MatchManager match = MatchManager.Instance;
            return match != null && match.InMatch && !match.RoundOn;
        }
    }
}
