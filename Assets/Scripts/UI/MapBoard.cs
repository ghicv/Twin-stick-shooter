using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The host picks which maps the rounds are played on (the lobby's MAPS station opens it): one button per round map,
// lit when it is on. At least one map stays on.
public class MapBoard : MonoBehaviour
{
    [SerializeField] private GameObject panel;

    [Tooltip("One button per round map is made from this (kept off itself), under the same parent.")]
    [SerializeField] private Button mapButtonTemplate;

    [SerializeField] private Button closeButton;

    [SerializeField] private Color onColor = new Color(0.4f, 1f, 0.55f);
    [SerializeField] private Color offColor = new Color(1f, 1f, 1f, 0.35f);

    private readonly List<Button> buttons = new List<Button>();
    private readonly List<int> mapIndexes = new List<int>();
    private MapManager maps;

    public bool IsOpen => panel.activeSelf;

    private void Awake()
    {
        panel.SetActive(false);
        mapButtonTemplate.gameObject.SetActive(false);
        closeButton.onClick.AddListener(Close);
        maps = FindAnyObjectByType<MapManager>();
        for (int i = 0; i < maps.Count; i++)
        {
            if (!maps.IsRoundMap(i))
                continue;
            Button button = Instantiate(mapButtonTemplate, mapButtonTemplate.transform.parent);
            button.gameObject.SetActive(true);
            int index = i; // captured by the click
            button.onClick.AddListener(() =>
            {
                if (MatchManager.Instance != null)
                    MatchManager.Instance.ToggleMap(index);
            });
            buttons.Add(button);
            mapIndexes.Add(i);
        }
    }

    public void Toggle()
    {
        if (IsOpen)
            Close();
        else
            panel.SetActive(true);
    }

    public void Close()
    {
        panel.SetActive(false);
    }

    private void Update()
    {
        MatchManager match = MatchManager.Instance;
        if (IsOpen && (match == null || match.InMatch))
            Close();
        if (!IsOpen)
            return;

        for (int i = 0; i < buttons.Count; i++)
        {
            bool on = match.MapEnabled(mapIndexes[i]);
            Text label = buttons[i].GetComponentInChildren<Text>();
            label.text = (on ? "[X]  " : "[  ]  ") + maps.MapName(mapIndexes[i]);
            label.color = on ? onColor : offColor;
        }
    }
}
