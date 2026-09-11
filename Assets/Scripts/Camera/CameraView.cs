using UnityEngine;

// Helpers about what the camera sees.
public static class CameraView
{
    // The point moved inside the view of an orthographic camera, at least Margin units from the edges.
    // Used to show an effect where something left the screen (e.g. fell off the map).
    public static Vector2 ClampInside(Camera camera, Vector2 point, float margin)
    {
        float halfHeight = camera.orthographicSize;
        float halfWidth = halfHeight * camera.aspect;
        Vector3 center = camera.transform.position;
        return new Vector2(
            Mathf.Clamp(point.x, center.x - halfWidth + margin, center.x + halfWidth - margin),
            Mathf.Clamp(point.y, center.y - halfHeight + margin, center.y + halfHeight - margin));
    }
}
