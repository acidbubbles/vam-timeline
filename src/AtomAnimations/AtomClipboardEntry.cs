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
        public FloatParamTargetSnapshot snapshot;
    }

    public class FreeControllerV3ClipboardEntry
    {
        public FreeControllerV3 controller;
        public TransformTargetSnapshot snapshot;
    }

    public class TriggersClipboardEntry : ISnapshot
    {
        public string name;
        public TriggerTargetSnapshot snapshot;
    }

    public interface ISnapshot
    {
    }

    public class TransformTargetSnapshot : ISnapshot
    {
        public BezierKeyframe x;
        public BezierKeyframe y;
        public BezierKeyframe z;
        public BezierKeyframe rotX;
        public BezierKeyframe rotY;
        public BezierKeyframe rotZ;
        public BezierKeyframe rotW;
    }

    public class FloatParamTargetSnapshot : ISnapshot
    {
        public BezierKeyframe value;
    }

    public class TriggerTargetSnapshot : ISnapshot
    {
        public JSONClass json;
    }
}
