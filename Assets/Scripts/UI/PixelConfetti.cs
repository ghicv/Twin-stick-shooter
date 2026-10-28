using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Pixel confetti on the UI: little squares burst out of a point, fall, and blink out. Real time, so it plays during
// slow motion too. Pieces are made from Piece Template (kept off itself) and reused.
public class PixelConfetti : MonoBehaviour
{
    [SerializeField] private Image pieceTemplate;

    [SerializeField] private float gravity = 1600f;

    [Tooltip("Seconds a piece lives (it blinks during the last third).")]
    [SerializeField] private float lifetime = 1.6f;

    private class Piece
    {
        public Image image;
        public Vector2 velocity;
        public float life;
    }

    private readonly List<Piece> pieces = new List<Piece>();

    private void Awake()
    {
        pieceTemplate.gameObject.SetActive(false);
    }

    // Position: where they burst from (this object's local space). Speed: how hard they fly out (UI pixels/sec).
    public void Burst(Vector2 position, int count, Color[] colors, float speed)
    {
        for (int i = 0; i < count; i++)
        {
            Piece piece = FreePiece();
            float angle = Random.Range(20f, 160f) * Mathf.Deg2Rad; // mostly upward
            piece.velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed * Random.Range(0.4f, 1f);
            piece.life = lifetime * Random.Range(0.7f, 1f);
            piece.image.color = colors[Random.Range(0, colors.Length)];
            float size = Random.Range(0, 2) == 0 ? 10f : 15f;
            piece.image.rectTransform.sizeDelta = new Vector2(size, size);
            piece.image.rectTransform.anchoredPosition = position;
            piece.image.gameObject.SetActive(true);
        }
    }

    private Piece FreePiece()
    {
        foreach (Piece piece in pieces)
            if (!piece.image.gameObject.activeSelf)
                return piece;
        var created = new Piece { image = Instantiate(pieceTemplate, pieceTemplate.transform.parent) };
        pieces.Add(created);
        return created;
    }

    private void Update()
    {
        float step = Time.unscaledDeltaTime;
        foreach (Piece piece in pieces)
        {
            if (!piece.image.gameObject.activeSelf)
                continue;
            piece.life -= step;
            if (piece.life <= 0f)
            {
                piece.image.gameObject.SetActive(false);
                continue;
            }
            piece.velocity.y -= gravity * step;
            piece.velocity.x *= 1f - 1.5f * step; // air slows them sideways
            piece.image.rectTransform.anchoredPosition += piece.velocity * step;
            piece.image.enabled = piece.life > lifetime / 3f || Mathf.Repeat(piece.life * 12f, 1f) < 0.5f;
        }
    }
}
