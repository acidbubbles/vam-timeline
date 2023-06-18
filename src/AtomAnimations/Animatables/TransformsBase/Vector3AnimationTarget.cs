using System.Collections.Generic;
using UnityEngine;

namespace VamTimeline
{
    public class Vector3AnimationTarget<TAnimatableRef> : CurveAnimationTargetBase<TAnimatableRef> where TAnimatableRef : AnimatableRefBase
    {
        public Vector3AnimationTarget<TAnimatableRef> position => this;
        public QuaternionAnimationTarget<TAnimatableRef> rotation => null;

        public readonly BezierAnimationCurve x = new BezierAnimationCurve();
        public readonly BezierAnimationCurve y = new BezierAnimationCurve();
        public readonly BezierAnimationCurve z = new BezierAnimationCurve();
        public readonly List<BezierAnimationCurve> curves;
        public int length => x.length;

        private float _weight = 1f;
        public float scaledWeight { get; private set; } = 1f;
        public float weight
        {
            get
            {
                return _weight;
            }
            set
            {
                _weight = value;
                scaledWeight = value.ExponentialScale(0.1f, 1f);
            }
        }

        public bool playbackEnabled { get; set; } = true;

        public Vector3AnimationTarget(TAnimatableRef animatableRef)
            : base(animatableRef)
        {
            curves = new List<BezierAnimationCurve>
            {
                x, y, z
            };
        }

        #region Control

        public override BezierAnimationCurve GetLeadCurve()
        {
            return x;
        }

        public override IEnumerable<BezierAnimationCurve> GetCurves()
        {
            return curves;
        }

        public void Validate(float animationLength)
        {
            Validate(GetLeadCurve(), animationLength);
        }

        public void ComputeCurves()
        {
            if (x.length < 2) return;

            foreach (var curve in curves)
            {
                curve.ComputeCurves();
            }
        }

        #endregion

        #region Keyframe control

        public int SetKeyframeByTime(float time, Vector3 localPosition, int curveType = CurveTypeValues.Undefined, bool makeDirty = true)
        {
            curveType = SelectCurveType(time, curveType);
            var key = x.SetKeyframe(time, localPosition.x, curveType);
            y.SetKeyframe(time, localPosition.y, curveType);
            z.SetKeyframe(time, localPosition.z, curveType);
            if (makeDirty) dirty = true;
            return key;
        }

        public int SetKeyframeByKey(int key, Vector3 localPosition)
        {
            var curveType = x.GetKeyframeByKey(key).curveType;
            x.SetKeyframeByKey(key, localPosition.x, curveType);
            y.SetKeyframeByKey(key, localPosition.y, curveType);
            z.SetKeyframeByKey(key, localPosition.z, curveType);
            dirty = true;
            return key;
        }

        public void DeleteFrame(float time)
        {
            var key = GetLeadCurve().KeyframeBinarySearch(time);
            if (key == -1) return;
            foreach (var curve in curves)
            {
                curve.RemoveKey(key);
            }
            dirty = true;
        }

        public void AddEdgeFramesIfMissing(float animationLength)
        {
            var lastCurveType = x.length > 0 ? x.GetLastFrame().curveType : CurveTypeValues.SmoothLocal;

            // We try and keep the unused animations at least valid
            dirty = x.AddEdgeFramesIfMissing(animationLength, lastCurveType);
            y.AddEdgeFramesIfMissing(animationLength, lastCurveType);
            z.AddEdgeFramesIfMissing(animationLength, lastCurveType);

            if (dirty && lastCurveType == CurveTypeValues.CopyPrevious && x.length > 2 && x.keys[x.length - 2].curveType == CurveTypeValues.CopyPrevious)
                DeleteFrame(x.keys[x.length - 2].time);
        }

        public void RecomputeKey(int key)
        {
            if (key == -1) return;
            x.RecomputeKey(key);
            y.RecomputeKey(key);
            z.RecomputeKey(key);
        }

        public float[] GetAllKeyframesTime()
        {
            var curve = x;
            var keyframes = new float[curve.length];
            for (var i = 0; i < curve.length; i++)
                keyframes[i] = curve.GetKeyframeByKey(i).time;
            return keyframes;
        }

        public int[] GetAllKeyframesKeys()
        {
            var curve = x;
            var keyframes = new int[curve.length];
            for (var i = 0; i < curve.length; i++)
                keyframes[i] = i;
            return keyframes;
        }

        public float GetTimeClosestTo(float time)
        {
            return x.GetKeyframeByKey(x.KeyframeBinarySearch(time, true)).time;
        }

        public bool HasKeyframe(float time)
        {
            return x.KeyframeBinarySearch(time) != -1;
        }

        #endregion

        #region Evaluate

        public Vector3 EvaluatePosition(float time)
        {
            return new Vector3(
                x.Evaluate(time),
                y.Evaluate(time),
                z.Evaluate(time)
            );
        }

        public float GetKeyframeTime(int key)
        {
            return x.GetKeyframeByKey(key).time;
        }

        public Vector3 GetKeyframePosition(int key)
        {
            return new Vector3(
                x.GetKeyframeByKey(key).value,
                y.GetKeyframeByKey(key).value,
                z.GetKeyframeByKey(key).value
            );
        }

        #endregion

        #region Snapshots

        public ISnapshot GetSnapshot(float time)
        {
            return GetCurveSnapshot(time);
        }

        public void SetSnapshot(float time, ISnapshot snapshot)
        {
            SetCurveSnapshot(time, (Vector3TargetSnapshot)snapshot);
        }

        public Vector3TargetSnapshot GetCurveSnapshot(float time)
        {
            var key = x.KeyframeBinarySearch(time);
            if (key == -1) return null;
            return new Vector3TargetSnapshot
            {
                x = x.GetKeyframeByKey(key),
                y = y.GetKeyframeByKey(key),
                z = z.GetKeyframeByKey(key),
            };
        }

        public void SetCurveSnapshot(float time, Vector3TargetSnapshot snapshot, bool makeDirty = true)
        {
            x.SetKeySnapshot(time, snapshot.x);
            y.SetKeySnapshot(time, snapshot.y);
            z.SetKeySnapshot(time, snapshot.z);
            if (makeDirty) dirty = true;
        }

        #endregion

    }
}
