using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// Top right during a LAN match: "NAME took down NAME", "NAME fell off"... The newest line is on top;
// each one stays a few seconds, then fades. MatchManager adds the lines (on every machine).
public class KillFeed : MonoBehaviour
{
    [SerializeField] private Text text;

    [SerializeField] private int maxLines = 5;

    [Tooltip("A line stays this long (seconds), then fades out.")]
    [SerializeField] private float lineTime = 5f;

    [SerializeField] private float fadeTime = 0.6f;

    private class Line
    {
        public string text;
        public float time;
    }

    private readonly List<Line> lines = new List<Line>();

    public void Add(string line)
    {
        lines.Insert(0, new Line { text = line, time = Time.unscaledTime });
        if (lines.Count > maxLines)
            lines.RemoveAt(lines.Count - 1);
    }

    private void Update()
    {
        var shown = new System.Text.StringBuilder();
        foreach (Line line in lines)
        {
            float alpha = Mathf.Clamp01(1f - (Time.unscaledTime - line.time - lineTime) / fadeTime);
            if (alpha <= 0f)
                continue;
            string a = Mathf.RoundToInt(alpha * 255f).ToString("X2");
            string faded = System.Text.RegularExpressions.Regex.Replace(line.text, "<color=#([0-9A-Fa-f]{6})>", "<color=#$1" + a + ">");
            shown.Append("<color=#FFFFFF" + a + ">" + faded + "</color>\n");
        }
        text.text = shown.ToString();
    }
}
