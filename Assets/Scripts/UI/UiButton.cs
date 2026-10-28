using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Makes a button feel alive: it grows a little under the mouse (with a soft tick) and clicks when pressed.
// Put it next to a Button. The sounds come from the scene's UiAudio.
[RequireComponent(typeof(Button))]
public class UiButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] private float hoverScale = 1.05f;

    [Tooltip("How fast it grows and shrinks back (per second).")]
    [SerializeField] private float speed = 14f;

    private Button button;
    private Vector3 baseScale;
    private bool hovered;

    private void Awake()
    {
        button = GetComponent<Button>();
        baseScale = transform.localScale;
        button.onClick.AddListener(() => UiAudio.Play(UiAudio.Sound.Click));
    }

    private void OnDisable()
    {
        hovered = false;
        transform.localScale = baseScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        if (button.interactable)
            UiAudio.Play(UiAudio.Sound.Hover);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
    }

    private void Update()
    {
        Vector3 target = hovered && button.interactable ? baseScale * hoverScale : baseScale;
        transform.localScale = Vector3.Lerp(transform.localScale, target, 1f - Mathf.Exp(-speed * Time.unscaledDeltaTime));
    }
}
