// The player's controls are off while a menu takes the keyboard: the ESC menu, or typing in the chat.
public static class InputLock
{
    public static bool Blocked => SettingsMenu.IsOpen || ChatBox.Typing;
}
