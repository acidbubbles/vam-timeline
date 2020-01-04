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
    public class JSONStorableFloatClipboardEntry : IClipboardEntry
    {
        public List<JSONStorableFloatValClipboardEntry> Entries;
    }

    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class JSONStorableFloatValClipboardEntry
    {
        public JSONStorable Storable;
        public JSONStorableFloat FloatParam;
        public Keyframe Snapshot;
    }
}
