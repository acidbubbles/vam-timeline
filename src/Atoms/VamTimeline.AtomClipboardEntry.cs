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
    public class AtomClipboardEntry : IClipboardEntry
    {
        public List<FreeControllerV3ClipboardEntry> Entries;
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
    }
}
