using UnityEngine;

namespace VamTimeline
{
    public static class RectTransformExtensions
    {
        public static void StretchParent(this RectTransform rect, float width, float height)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = new Vector2(0, 1);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        }

        public static void StretchTop(this RectTransform rect, float width)
        {
            rect.anchorMin = new Vector2(0, 0);
            rect.anchorMax = new Vector2(1, 0);
            rect.anchoredPosition = new Vector2(0, 1);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        }
    }
}
