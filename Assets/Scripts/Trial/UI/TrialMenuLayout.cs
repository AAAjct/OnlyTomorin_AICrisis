using UnityEngine;

namespace TomorinTrial
{
    static class TrialMenuLayout
    {
        // Convert authored top-left coordinates before stretching the panel.
        public static void StretchContents(RectTransform panel)
        {
            Vector2 size = panel.sizeDelta;
            if (size.x <= 0 || size.y <= 0) return;
            var topLeft = new Vector2(0, 1);
            foreach (RectTransform child in panel)
            {
                if (child.anchorMin != topLeft || child.anchorMax != topLeft || child.pivot != topLeft)
                    continue;
                StretchContents(child);
                Vector2 start = child.anchoredPosition;
                Vector2 end = start + new Vector2(child.sizeDelta.x, -child.sizeDelta.y);
                child.anchorMin = new Vector2(start.x / size.x, 1 + end.y / size.y);
                child.anchorMax = new Vector2(end.x / size.x, 1 + start.y / size.y);
                child.offsetMin = child.offsetMax = Vector2.zero;
            }
        }
    }
}
