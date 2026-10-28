using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// LAN, before every round: a strip of map cards races past a marker and slows down until it stops on the next map,
// like opening a case. The host picks the map and a seed (MatchManager), so every machine shows the same spin and
// the card stops at the same spot. A tick plays for every card that passes the marker.
// The cards are only pictures. Once the strip has stopped, the real map is put in place (MatchManager.Travel) and
// Enter Map lines the game camera up with the chosen card's picture (the picture is that map seen by this camera).
// The picture turns see-through, showing the real map in its place, and the camera flies into the map while the
// whole roulette grows with it around that card: it feels like diving through the card into the map.
[DefaultExecutionOrder(100)] // after CameraFollow, so the roulette grows with this frame's camera
public class MapRoulette : MonoBehaviour
{
    [SerializeField] private GameObject panel;

    [Tooltip("The dark backdrop: fades away as the camera flies in (the map's landscape shows around the cards).")]
    [SerializeField] private Image backdrop;

    [Tooltip("Title, window, marker and result: grow with the camera around the chosen card.")]
    [SerializeField] private RectTransform content;

    [Tooltip("The window's own dark background (fades away too, so the map shows through).")]
    [SerializeField] private Image windowBackground;

    [Tooltip("Faded away with the chosen card's picture (the marker, the title, the name).")]
    [SerializeField] private Graphic[] fadeOnEnter;

    [Tooltip("The cards' parent. It slides sideways inside a masked window whose middle is the marker.")]
    [SerializeField] private RectTransform strip;

    [Tooltip("One card per slot is made from this (kept off itself): children Thumbnail (Image), Name (Text) and " +
             "Bar (Image, the theme's color), and a CanvasGroup.")]
    [SerializeField] private RectTransform cardTemplate;

    [Tooltip("The chosen map's name, typed in when the strip stops.")]
    [SerializeField] private Text resultText;

    [Header("Show")]
    [Tooltip("The window the strip runs in: it opens sideways at the start and shakes at the stop.")]
    [SerializeField] private RectTransform window;

    [Tooltip("A white flash over the window at the stop.")]
    [SerializeField] private Image flash;

    [Tooltip("A glow in the chosen map's color behind its card (a child of the strip).")]
    [SerializeField] private Image glow;

    [SerializeField] private float openTime = 0.3f;

    [Tooltip("Cards passing the marker grow by this much (like under a lens).")]
    [SerializeField] private float lensScale = 0.1f;

    [SerializeField] private float shakeTime = 0.25f;
    [SerializeField] private float shakeStrength = 10f;

    [Tooltip("Seconds per letter of the chosen map's name.")]
    [SerializeField] private float typeTime = 0.05f;

    [Min(8)]
    [SerializeField] private int cardCount = 46;

    [Tooltip("The slot the strip stops on (the cards before it race past).")]
    [SerializeField] private int winnerSlot = 40;

    [Tooltip("Distance between two cards' middles (UI pixels).")]
    [SerializeField] private float cardSpacing = 300f;

    [Tooltip("Seconds from the start to the stop.")]
    [SerializeField] private float spinTime = 4.5f;

    [Tooltip("Seconds the chosen card is shown before going into the map.")]
    [SerializeField] private float holdTime = 1f;

    [Tooltip("Seconds the chosen card's picture takes to turn into the real map.")]
    [SerializeField] private float fadeTime = 0.25f;

    [Tooltip("Seconds the camera takes to fly from the card into the map.")]
    [SerializeField] private float flyTime = 1.8f;

    [Tooltip("Pixel streaks from the middle of the screen, strongest halfway through the dive.")]
    [SerializeField] private Image speedLines;

    [Tooltip("A white flash over the whole screen when the camera lands in the map.")]
    [SerializeField] private Image landFlash;

    [SerializeField] private float landFlashTime = 0.3f;
    [SerializeField] private float landShakeStrength = 0.15f;

    [Tooltip("Played as the camera dives into the map. Optional.")]
    [SerializeField] private AudioClip flySound;
    [Range(0f, 1f)]
    [SerializeField] private float flyVolume = 0.4f;

    [Tooltip("If the map never comes, the roulette closes by itself after this many seconds of waiting.")]
    [SerializeField] private float maxWaitTime = 4f;

    [Tooltip("How sharply it slows down (higher = a faster start, a longer crawl at the end).")]
    [SerializeField] private float slowDown = 4f;

    [SerializeField] private float winnerScale = 1.12f;

    [Tooltip("How see-through the other cards get once the strip stops.")]
    [Range(0f, 1f)]
    [SerializeField] private float loserAlpha = 0.35f;

    [SerializeField] private AudioClip tickSound;
    [Range(0f, 1f)]
    [SerializeField] private float tickVolume = 0.3f;

    [SerializeField] private AudioClip winSound;
    [Range(0f, 1f)]
    [SerializeField] private float winVolume = 0.5f;

    private readonly List<RectTransform> cards = new List<RectTransform>();
    private MapManager maps;
    private CameraFollow cameraRig;
    private float timer = -1f; // < 0: not playing
    private float startX, endX;
    private int lastSlot;
    private bool stopped;
    private float enterTimer = -1f; // >= 0: going into the map
    private string winnerName;
    private Color winnerColor;
    private Vector2 windowPosition;
    private float backdropAlpha, windowAlpha;
    private Camera gameCamera;
    private float flyFromSize;
    private Vector2 flyFromOffset; // the chosen picture's middle, from the screen's middle (canvas units)

    // How long the host waits before the travel: the spin and the hold.
    public float Duration => spinTime + holdTime;

    public bool Playing => timer >= 0f;

    // Stopped and shown: the map can be put in place behind it now (then Enter Map).
    public bool WaitingForMap => Playing && timer >= Duration && enterTimer < 0f;

    private void Awake()
    {
        cardTemplate.gameObject.SetActive(false);
        panel.SetActive(false);
        windowPosition = window.anchoredPosition;
        backdropAlpha = backdrop.color.a;
        windowAlpha = windowBackground.color.a;
    }

    // Every machine: spin to Winner (a map index). Choices = the maps the other cards show.
    public void Play(int winner, int seed, List<int> choices)
    {
        if (maps == null)
            maps = FindAnyObjectByType<MapManager>();
        while (cards.Count < cardCount)
        {
            RectTransform card = Instantiate(cardTemplate, strip);
            card.gameObject.SetActive(true);
            cards.Add(card);
        }

        var random = new System.Random(seed);
        List<int> order = CardOrder(winner, choices, random);
        for (int i = 0; i < cards.Count; i++)
        {
            LevelMap level = maps.Map(order[i]);
            RectTransform card = cards[i];
            card.anchoredPosition = new Vector2(i * cardSpacing, 0f);
            card.localScale = Vector3.one;
            card.GetComponent<CanvasGroup>().alpha = 1f;
            SetAlpha(card.GetComponent<Image>(), 1f);
            SetAlpha(card.Find("Thumbnail").GetComponent<Image>(), 1f);
            card.Find("Thumbnail").GetComponent<Image>().sprite = level.Thumbnail;
            card.Find("Name").GetComponent<Text>().text = level.DisplayName;
            card.Find("Bar").GetComponent<Image>().color = level.ThemeColor;
        }

        // Start with the third card under the marker; stop somewhere on the winner (not always its middle).
        startX = -2f * cardSpacing;
        float landing = ((float)random.NextDouble() - 0.5f) * cardSpacing * 0.7f;
        endX = -(winnerSlot * cardSpacing + landing);
        strip.anchoredPosition = new Vector2(startX, 0f);
        lastSlot = 2;
        stopped = false;
        resultText.text = "";
        winnerName = maps.Map(winner).DisplayName;
        winnerColor = maps.Map(winner).ThemeColor;
        resultText.color = winnerColor;
        glow.rectTransform.anchoredPosition = new Vector2(winnerSlot * cardSpacing, 0f);
        glow.rectTransform.SetAsFirstSibling(); // behind the cards
        glow.gameObject.SetActive(false);
        flash.gameObject.SetActive(false);
        window.anchoredPosition = windowPosition;
        content.localScale = Vector3.one;
        content.anchoredPosition = Vector2.zero;
        SetAlpha(backdrop, backdropAlpha);
        SetAlpha(windowBackground, windowAlpha);
        SetAlpha(speedLines, 0f);
        SetAlpha(landFlash, 0f);
        foreach (Graphic graphic in fadeOnEnter)
            SetAlpha(graphic, 1f);
        enterTimer = -1f;
        timer = 0f;
        panel.SetActive(true);
    }

    // The maps on the cards: every map once in a shuffled order, then again in a new order, and so on, never the same
    // map twice side by side; the winner on Winner Slot.
    private List<int> CardOrder(int winner, List<int> choices, System.Random random)
    {
        var order = new List<int>();
        var pool = choices.Count > 0 ? new List<int>(choices) : new List<int> { winner };
        while (order.Count < cards.Count)
        {
            for (int i = pool.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (pool[i], pool[j]) = (pool[j], pool[i]);
            }
            if (order.Count > 0 && pool.Count > 1 && pool[0] == order[order.Count - 1])
                (pool[0], pool[pool.Count - 1]) = (pool[pool.Count - 1], pool[0]);
            order.AddRange(pool);
        }
        order.RemoveRange(cards.Count, order.Count - cards.Count);

        // The winner on its slot (swapped with the nearest card already showing it), then any card that ended up
        // like its neighbour gets another map.
        int found = order.IndexOf(winner, Mathf.Max(0, winnerSlot - pool.Count));
        if (found >= 0)
            (order[found], order[winnerSlot]) = (order[winnerSlot], order[found]);
        else
            order[winnerSlot] = winner;
        for (int i = 1; i < order.Count && pool.Count > 2; i++)
        {
            if (order[i] != order[i - 1])
                continue;
            int change = i == winnerSlot ? i - 1 : i; // never the winner
            int before = change > 0 ? order[change - 1] : -1;
            int after = change + 1 < order.Count ? order[change + 1] : -1;
            foreach (int other in pool)
                if (other != before && other != after)
                {
                    order[change] = other;
                    break;
                }
        }
        return order;
    }

    // The chosen map is in place: line the camera up with the card's picture, fade the picture into the real map,
    // and fly in.
    public void EnterMap()
    {
        if (!WaitingForMap)
            return;
        var picture = (RectTransform)cards[winnerSlot].Find("Thumbnail");
        var corners = new Vector3[4];
        picture.GetWorldCorners(corners);
        Canvas canvas = picture.GetComponentInParent<Canvas>().rootCanvas;
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[0]);
        Vector2 topRight = RectTransformUtility.WorldToScreenPoint(uiCamera, corners[2]);

        // The picture is the map's whole camera view (its height = the map camera's height), so the camera must be
        // that many times further out, centered so the map's middle sits on the picture's middle.
        float mapSize = maps.CurrentMap.CameraSize;
        flyFromSize = mapSize * Screen.height / (topRight.y - bottomLeft.y);
        Vector2 offset = (bottomLeft + topRight) * 0.5f - new Vector2(Screen.width, Screen.height) * 0.5f;
        flyFromOffset = offset / canvas.scaleFactor;
        if (cameraRig == null)
            cameraRig = FindAnyObjectByType<CameraFollow>();
        gameCamera = cameraRig.GetComponentInChildren<Camera>();
        cameraRig.FlyIn(offset, flyFromSize, 0f, flyTime);
        GameAudio.Play(flySound, flyVolume);
        enterTimer = 0f;
    }

    // Real time: the show goes on during slow motion too.
    private void Update()
    {
        if (timer < 0f)
            return;
        timer += Time.unscaledDeltaTime;
        Open();
        Spin();
        if (stopped)
            ShowWinner(timer - spinTime);

        if (WaitingForMap && timer > Duration + maxWaitTime)
            Close();
    }

    // Going into the map: the roulette grows exactly as much as the camera zooms, around the chosen card, which
    // stays over the real map; the card's picture, the window and the backdrop fade away so the map shows.
    private void LateUpdate()
    {
        if (enterTimer < 0f)
            return;
        enterTimer += Time.unscaledDeltaTime;
        float zoom = flyFromSize / gameCamera.orthographicSize;
        float progress = cameraRig.FlightProgress;
        content.localScale = new Vector3(zoom, zoom, 1f);
        content.anchoredPosition = flyFromOffset * (1f - progress) - flyFromOffset * zoom;

        float shown = 1f - Mathf.Clamp01(enterTimer / fadeTime);
        RectTransform card = cards[winnerSlot];
        SetAlpha(card.GetComponent<Image>(), shown);
        SetAlpha(card.Find("Thumbnail").GetComponent<Image>(), shown);
        SetAlpha(windowBackground, windowAlpha * shown);
        foreach (Graphic graphic in fadeOnEnter)
            SetAlpha(graphic, shown);
        glow.gameObject.SetActive(false);
        SetAlpha(backdrop, backdropAlpha * (1f - Mathf.Clamp01(progress * 2f)));

        // Speed lines while diving, growing outward.
        float rush = Mathf.Sin(Mathf.Clamp01(enterTimer / flyTime) * Mathf.PI);
        SetAlpha(speedLines, 0.35f * rush);
        speedLines.rectTransform.localScale = Vector3.one * (1f + enterTimer / flyTime * 0.6f);

        // Landing: a white flash fading out in steps, and a little shake.
        float landed = enterTimer - flyTime;
        if (landed >= 0f)
        {
            if (landed - Time.unscaledDeltaTime < 0f)
            {
                CameraShake shake = gameCamera.GetComponent<CameraShake>();
                if (shake != null)
                    shake.Shake(landShakeStrength, 0.2f);
                CRTScreen.Blur(gameCamera.transform.position, 0.6f);
            }
            float left = 1f - Mathf.Clamp01(landed / landFlashTime);
            SetAlpha(landFlash, Mathf.Ceil(left * 4f) / 4f * 0.7f);
            if (landed >= landFlashTime)
                Close();
        }
    }

    private static void SetAlpha(Graphic graphic, float alpha)
    {
        Color color = graphic.color;
        color.a = alpha;
        graphic.color = color;
    }

    private void Close()
    {
        timer = -1f;
        enterTimer = -1f;
        panel.SetActive(false);
    }

    // The window opens sideways in a few steps (8-bit style), the rest appears as it does.
    private void Open()
    {
        float open = Mathf.Clamp01(timer / openTime);
        float stepped = Mathf.Ceil(open * 6f) / 6f;
        window.localScale = new Vector3(Mathf.Max(0.02f, stepped), 1f, 1f);
    }

    private void Spin()
    {
        float progress = Mathf.Clamp01(timer / spinTime);
        float eased = 1f - Mathf.Pow(1f - progress, slowDown);
        float x = Mathf.Lerp(startX, endX, eased);
        strip.anchoredPosition = new Vector2(x, 0f);

        int slot = Mathf.FloorToInt(-x / cardSpacing + 0.5f); // the card under the marker
        if (slot != lastSlot)
        {
            lastSlot = slot;
            GameAudio.Play(tickSound, tickVolume, 0.05f);
        }

        if (!stopped)
        {
            // The lens: the card under the marker is the biggest.
            for (int i = Mathf.Max(0, slot - 2); i <= Mathf.Min(cards.Count - 1, slot + 2); i++)
            {
                float distance = Mathf.Abs(i * cardSpacing + x) / cardSpacing;
                cards[i].localScale = Vector3.one * (1f + lensScale * Mathf.Max(0f, 1f - distance));
            }
        }

        if (progress >= 1f && !stopped)
        {
            stopped = true;
            GameAudio.Play(winSound, winVolume);
            for (int i = 0; i < cards.Count; i++)
                if (i != winnerSlot)
                {
                    cards[i].GetComponent<CanvasGroup>().alpha = loserAlpha;
                    cards[i].localScale = Vector3.one;
                }
            glow.gameObject.SetActive(true);
            flash.gameObject.SetActive(true);
        }
    }

    // After the stop (Since = seconds since): a white flash fading out in steps, the window shaking, the card
    // popping up on a glow in its map's color, and the name typed in letter by letter.
    private void ShowWinner(float since)
    {
        float flashLeft = 1f - Mathf.Clamp01(since / 0.25f);
        Color white = flash.color;
        white.a = Mathf.Ceil(flashLeft * 4f) / 4f * 0.8f;
        flash.color = white;

        float shake = Mathf.Clamp01(1f - since / shakeTime) * shakeStrength;
        window.anchoredPosition = windowPosition + new Vector2(Random.Range(-shake, shake), Random.Range(-shake, shake));

        float pop = Mathf.Min(1f, since * 6f);
        cards[winnerSlot].localScale = Vector3.one * Mathf.Lerp(1f + lensScale, winnerScale, pop);

        float pulse = 0.55f + 0.2f * Mathf.Sin(since * 6f);
        glow.color = new Color(winnerColor.r, winnerColor.g, winnerColor.b, pulse);
        glow.rectTransform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(since * 3f));

        int letters = Mathf.Min(winnerName.Length, Mathf.FloorToInt(since / typeTime));
        resultText.text = winnerName.Substring(0, letters);
    }
}
