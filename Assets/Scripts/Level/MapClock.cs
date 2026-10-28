using Unity.Netcode;

// The time moving map parts (MovingPlatform, GhostPlatform, SpriteFrames) follow. In a LAN game it is the host's
// time, so every machine sees them in the same place without sending anything. Smooth inside FixedUpdate too:
// the local time plus the gap to the host's time, which is only corrected when it drifts.
public static class MapClock
{
    private const double MaxDrift = 0.1;
    private static double offset;

    public static float Time
    {
        get
        {
            double local = UnityEngine.Time.timeAsDouble; // the physics step's time inside FixedUpdate
            NetworkManager network = NetworkManager.Singleton;
            if (network != null && network.IsListening)
            {
                double gap = network.ServerTime.Time - local;
                if (System.Math.Abs(gap - offset) > MaxDrift)
                    offset = gap;
            }
            else
            {
                offset = 0;
            }
            return (float)(local + offset);
        }
    }
}
