using System.Collections.Generic;
using UnityEngine;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class FloatParamClipboardEntry : IClipboardEntry
    {
        public List<FloatParamValClipboardEntry> Entries;
    }

    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class FloatParamValClipboardEntry
    {
        public JSONStorable Storable;
        public JSONStorableFloat FloatParam;
        public Keyframe Snapshot;
    }
}
