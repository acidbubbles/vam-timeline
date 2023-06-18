using System.Collections.Generic;
using SimpleJSON;
using UnityEngine;

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

		public bool empty =>
            controllers.Count == 0 &&
            floatParams.Count == 0 &&
            triggers.Count == 0;
    }

    public interface IClipboardEntry<out TRef, out TSnapshot>
        where TRef : AnimatableRefBase
        where TSnapshot : ISnapshot
    {
        TRef animatableRef { get; }
        TSnapshot snapshot { get; }
    }

    public class FloatParamValClipboardEntry : IClipboardEntry<JSONStorableFloatRef, FloatParamTargetSnapshot>
    {
        public JSONStorableFloatRef animatableRef { get; set; }

        public FloatParamTargetSnapshot snapshot { get; set; }
    }

    public class FreeControllerV3ClipboardEntry : IClipboardEntry<FreeControllerV3Ref, TransformTargetSnapshot>
    {
        public FreeControllerV3Ref animatableRef { get; set; }

        public TransformTargetSnapshot snapshot { get; set; }
    }

    public class TriggersClipboardEntry : IClipboardEntry<TriggersTrackRef, TriggerTargetSnapshot>
    {
        public TriggersTrackRef animatableRef { get; set; }

        public TriggerTargetSnapshot snapshot { get; set; }
    }

    public interface ISnapshot
    {
    }

    public class TransformTargetSnapshot : ISnapshot
    {
        public Vector3TargetSnapshot position;
        public QuaternionTargetSnapshot rotation;
    }

    public class Vector3TargetSnapshot : ISnapshot
    {
        public BezierKeyframe x;
        public BezierKeyframe y;
        public BezierKeyframe z;

        public Vector3 AsVector3() => new Vector3(x.value, y.value, z.value);
    }

    public class QuaternionTargetSnapshot : ISnapshot
    {
        public BezierKeyframe rotX;
        public BezierKeyframe rotY;
        public BezierKeyframe rotZ;
        public BezierKeyframe rotW;

        public Quaternion AsQuaternion() => new Quaternion(rotX.value, rotY.value, rotZ.value, rotW.value);
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
