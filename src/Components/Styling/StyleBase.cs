using UnityEngine;

namespace VamTimeline
{
    public abstract class StyleBase
    {
        // Global
        public Font Font { get; }
        public Color FontColor { get; set; } = new Color(0, 0, 0);
        public Color BackgroundColor { get; set; } = new Color(0.721f, 0.682f, 0.741f);

        public StyleBase()
        {
            Font = (Font)Resources.GetBuiltinResource(typeof(Font), "Arial.ttf");
        }
    }
}
