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
    public class AtomClipboard
    {
        public float Time;
        public readonly IList<AtomClipboardEntry> Entries = new List<AtomClipboardEntry>();

        public void Clear()
        {
            Time = 0f;
            Entries.Clear();
        }
    }

    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class AtomClipboardEntry
    {
        public float Time;
        public List<FreeControllerV3ClipboardEntry> Controllers;
        public List<FloatParamValClipboardEntry> FloatParams;
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

    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class FreeControllerV3ClipboardEntry
    {
        public FreeControllerV3 Controller;
        public FreeControllerV3Snapshot Snapshot;
    }

    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class FreeControllerV3Snapshot
    {
        public Keyframe X;
        public Keyframe Y;
        public Keyframe Z;
        public Keyframe RotX;
        public Keyframe RotY;
        public Keyframe RotZ;
        public Keyframe RotW;

        public string CurveType;
    }
}
