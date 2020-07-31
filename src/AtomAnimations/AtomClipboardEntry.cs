using System.Collections.Generic;
using SimpleJSON;

namespace VamTimeline
{
    public class AtomClipboard
    {
        public float time;
        public readonly IList<AtomClipboardEntry> entries = new List<AtomClipboardEntry>();

        public void Clear()
        {
            time = 0f;
            entries.Clear();
        }
    }

    public class AtomClipboardEntry
    {
        public float time;
        public List<FreeControllerV3ClipboardEntry> controllers;
        public List<FloatParamValClipboardEntry> floatParams;
        public List<TriggersClipboardEntry> triggers;
    }

    public class FloatParamValClipboardEntry
    {
        public string storableId;
        public string floatParamName;
        public FloatParamSnapshot snapshot;
    }

    public class FreeControllerV3ClipboardEntry
    {
        public FreeControllerV3 controller;
        public FreeControllerV3Snapshot snapshot;
    }

    public class TriggersClipboardEntry : ISnapshot
    {
        public string name;
        public TriggerSnapshot snapshot;
    }

    public interface ISnapshot
    {
    }

    public class FreeControllerV3Snapshot : ISnapshot
    {
        public BezierKeyframe x;
        public BezierKeyframe y;
        public BezierKeyframe z;
        public BezierKeyframe rotX;
        public BezierKeyframe rotY;
        public BezierKeyframe rotZ;
        public BezierKeyframe rotW;
    }

    public class FloatParamSnapshot : ISnapshot
    {
        public BezierKeyframe value;
    }

    public class TriggerSnapshot : ISnapshot
    {
        public JSONClass json;
    }
}
