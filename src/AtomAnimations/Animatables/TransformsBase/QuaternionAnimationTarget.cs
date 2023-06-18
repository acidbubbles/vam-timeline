using System.Collections.Generic;
using UnityEngine;

namespace VamTimeline
{
    public class QuaternionAnimationTarget<TAnimatableRef> : CurveAnimationTargetBase<TAnimatableRef> where TAnimatableRef : AnimatableRefBase
    {
        public Vector3AnimationTarget<TAnimatableRef> position => null;
        public QuaternionAnimationTarget<TAnimatableRef> rotation => this;

        public readonly BezierAnimationCurve rotX = new BezierAnimationCurve();
        public readonly BezierAnimationCurve rotY = new BezierAnimationCurve();
        public readonly BezierAnimationCurve rotZ = new BezierAnimationCurve();
        public readonly BezierAnimationCurve rotW = new BezierAnimationCurve();
        public readonly List<BezierAnimationCurve> curves;
        public int length => rotX.length;

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

        public QuaternionAnimationTarget(TAnimatableRef animatableRef)
            : base(animatableRef)
        {
            curves = new List<BezierAnimationCurve>
            {
                rotX, rotY, rotZ, rotW
            };
        }

        #region Control

        public override BezierAnimationCurve GetLeadCurve()
        {
            return rotX;
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
            if (rotW.length < 2) return;

            foreach (var curve in curves)
            {
                curve.ComputeCurves();
            }
        }

        #endregion

        #region Keyframe control

        public int SetKeyframeByTime(float time, Quaternion locationRotation, int curveType = CurveTypeValues.Undefined, bool makeDirty = true)
        {
            curveType = SelectCurveType(time, curveType);
            var key = rotX.SetKeyframe(time, locationRotation.x, curveType);
            rotY.SetKeyframe(time, locationRotation.y, curveType);
            rotZ.SetKeyframe(time, locationRotation.z, curveType);
            rotW.SetKeyframe(time, locationRotation.w, curveType);
            if (makeDirty) dirty = true;
            return key;
        }

        public int SetKeyframeByKey(int key, Quaternion locationRotation)
        {
            var curveType = rotX.GetKeyframeByKey(key).curveType;
            rotX.SetKeyframeByKey(key, locationRotation.x, curveType);
            rotY.SetKeyframeByKey(key, locationRotation.y, curveType);
            rotZ.SetKeyframeByKey(key, locationRotation.z, curveType);
            rotW.SetKeyframeByKey(key, locationRotation.w, curveType);
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
            var lastCurveType = rotX.length > 0 ? rotX.GetLastFrame().curveType : CurveTypeValues.SmoothLocal;

            // We try and keep the unused animations at least valid
            dirty = rotX.AddEdgeFramesIfMissing(animationLength, lastCurveType);
            rotY.AddEdgeFramesIfMissing(animationLength, lastCurveType);
            rotZ.AddEdgeFramesIfMissing(animationLength, lastCurveType);
            rotW.AddEdgeFramesIfMissing(animationLength, lastCurveType);

            if (dirty && lastCurveType == CurveTypeValues.CopyPrevious && rotX.length > 2 && rotX.keys[rotX.length - 2].curveType == CurveTypeValues.CopyPrevious)
                DeleteFrame(rotX.keys[rotX.length - 2].time);
        }

        public void RecomputeKey(int key)
        {
            if (key == -1) return;
            rotX.RecomputeKey(key);
            rotY.RecomputeKey(key);
            rotZ.RecomputeKey(key);
            rotW.RecomputeKey(key);
        }

        public float[] GetAllKeyframesTime()
        {
            var curve = rotX;
            var keyframes = new float[curve.length];
            for (var i = 0; i < curve.length; i++)
                keyframes[i] = curve.GetKeyframeByKey(i).time;
            return keyframes;
        }

        public int[] GetAllKeyframesKeys()
        {
            var curve = rotX;
            var keyframes = new int[curve.length];
            for (var i = 0; i < curve.length; i++)
                keyframes[i] = i;
            return keyframes;
        }

        public float GetTimeClosestTo(float time)
        {
            return rotX.GetKeyframeByKey(rotX.KeyframeBinarySearch(time, true)).time;
        }

        public bool HasKeyframe(float time)
        {
            return rotX.KeyframeBinarySearch(time) != -1;
        }

        #endregion

        #region Evaluate

        public Quaternion EvaluateRotation(float time)
        {
            return new Quaternion(
                rotX.Evaluate(time),
                rotY.Evaluate(time),
                rotZ.Evaluate(time),
                rotW.Evaluate(time)
            );
        }

        public Quaternion GetRotationAtKeyframe(int key)
        {
            return new Quaternion(
                rotX.GetKeyframeByKey(key).value,
                rotY.GetKeyframeByKey(key).value,
                rotZ.GetKeyframeByKey(key).value,
                rotW.GetKeyframeByKey(key).value
            );
        }

        public float GetKeyframeTime(int key)
        {
            return rotW.GetKeyframeByKey(key).time;
        }

        public Quaternion GetKeyframeRotation(int key)
        {
            return new Quaternion(
                rotX.GetKeyframeByKey(key).value,
                rotY.GetKeyframeByKey(key).value,
                rotZ.GetKeyframeByKey(key).value,
                rotW.GetKeyframeByKey(key).value
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
            SetCurveSnapshot(time, (QuaternionTargetSnapshot)snapshot);
        }

        public QuaternionTargetSnapshot GetCurveSnapshot(float time)
        {
            var key = rotX.KeyframeBinarySearch(time);
            if (key == -1) return null;
            return new QuaternionTargetSnapshot
            {
                rotX = rotX.GetKeyframeByKey(key),
                rotY = rotY.GetKeyframeByKey(key),
                rotZ = rotZ.GetKeyframeByKey(key),
                rotW = rotW.GetKeyframeByKey(key),
            };
        }

        public void SetCurveSnapshot(float time, QuaternionTargetSnapshot snapshot, bool makeDirty = true)
        {
            rotX.SetKeySnapshot(time, snapshot.rotX);
            rotY.SetKeySnapshot(time, snapshot.rotY);
            rotZ.SetKeySnapshot(time, snapshot.rotZ);
            rotW.SetKeySnapshot(time, snapshot.rotW);
            if (makeDirty) dirty = true;
        }

        #endregion

    }
}
