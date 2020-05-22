using UnityEngine;

namespace VamTimeline
{
    public class DopeSheetStyle
    {
        public static DopeSheetStyle Default()
        {
            return new DopeSheetStyle();
        }

        public Font Font { get; }
        public Color FontColor { get; set; } = new Color(0, 0, 0);
        public Color BackgroundColor { get; set; } = new Color(0.721f, 0.682f, 0.741f);
        public Color GroupBackgroundColor { get; set; } = new Color(0.421f, 0.582f, 0.441f);
        public Color LabelBackgroundColor { get; set; } = new Color(0.521f, 0.482f, 0.541f);
        public float RowHeight { get; set; } = 40f;
        public float RowSpacing { get; set; } = 2f;
        public float LabelWidth { get; set; } = 150f;

        public DopeSheetStyle()
        {
            Font = (Font)Resources.GetBuiltinResource(typeof(Font), "Arial.ttf");
        }
    }
}
