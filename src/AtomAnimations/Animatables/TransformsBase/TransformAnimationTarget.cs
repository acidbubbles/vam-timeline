using System.Collections.Generic;
using UnityEngine;

namespace VamTimeline
{
    public abstract class TransformAnimationTargetBase<TAnimatableRef> : CurveAnimationTargetBase<TAnimatableRef> where TAnimatableRef : AnimatableRefBase
    {
        public readonly BezierAnimationCurve x = new BezierAnimationCurve();
        public readonly BezierAnimationCurve y = new BezierAnimationCurve();
        public readonly BezierAnimationCurve z = new BezierAnimationCurve();
        public readonly BezierAnimationCurve rotX = new BezierAnimationCurve();
        public readonly BezierAnimationCurve rotY = new BezierAnimationCurve();
        public readonly BezierAnimationCurve rotZ = new BezierAnimationCurve();
        public readonly BezierAnimationCurve rotW = new BezierAnimationCurve();
        public readonly List<BezierAnimationCurve> curves;

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

        protected TransformAnimationTargetBase(TAnimatableRef animatableRef)
            : base(animatableRef)
        {
            curves = new List<BezierAnimationCurve>
            {
                x, y, z, rotX, rotY, rotZ, rotW
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
            if (x.length != rotW.length)
            {
                SuperController.LogError($"Mismatched rotation and position data on controller {name}. {x.length} position keys and {rotW.length} rotation keys found. Missing data will be created.");
                RepairMismatchedCurves();
            }
        }

        private void RepairMismatchedCurves()
        {
            if (x.length > rotW.length)
            {
                for (var i = 0; i < x.length; i++)
                {
                    var time = x.keys[i].time;
                    SetKeyframe(time, EvaluatePosition(time), EvaluateRotation(time), x.keys[i].curveType, false);
                }
            }
            if (x.length < rotW.length)
            {
                for (var i = 0; i < rotW.length; i++)
                {
                    var time = rotW.keys[i].time;
                    SetKeyframe(time, EvaluatePosition(time), EvaluateRotation(time), rotW.keys[i].curveType, false);
                }
            }
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

        public int SetKeyframe(float time, Vector3 localPosition, Quaternion locationRotation, int curveType = CurveTypeValues.Undefined, bool makeDirty = true)
        {
            curveType = SelectCurveType(time, curveType);
            var key = x.SetKeyframe(time, localPosition.x, curveType);
            y.SetKeyframe(time, localPosition.y, curveType);
            z.SetKeyframe(time, localPosition.z, curveType);
            rotX.SetKeyframe(time, locationRotation.x, curveType);
            rotY.SetKeyframe(time, locationRotation.y, curveType);
            rotZ.SetKeyframe(time, locationRotation.z, curveType);
            rotW.SetKeyframe(time, locationRotation.w, curveType);
            if (makeDirty) dirty = true;
            return key;
        }

        public int SetKeyframeByKey(int key, Vector3 localPosition, Quaternion locationRotation)
        {
            var curveType = x.GetKeyframeByKey(key).curveType;
            x.SetKeyframeByKey(key, localPosition.x, curveType);
            y.SetKeyframeByKey(key, localPosition.y, curveType);
            z.SetKeyframeByKey(key, localPosition.z, curveType);
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
            var lastCurveType = x.length > 0 ? x.GetLastFrame().curveType : CurveTypeValues.SmoothLocal;

            dirty = x.AddEdgeFramesIfMissing(animationLength, lastCurveType);
            y.AddEdgeFramesIfMissing(animationLength, lastCurveType);
            z.AddEdgeFramesIfMissing(animationLength, lastCurveType);
            rotX.AddEdgeFramesIfMissing(animationLength, lastCurveType);
            rotY.AddEdgeFramesIfMissing(animationLength, lastCurveType);
            rotZ.AddEdgeFramesIfMissing(animationLength, lastCurveType);
            rotW.AddEdgeFramesIfMissing(animationLength, lastCurveType);

            if (dirty && lastCurveType == CurveTypeValues.CopyPrevious && x.length > 2 && x.keys[x.length - 2].curveType == CurveTypeValues.CopyPrevious)
                DeleteFrame(x.keys[x.length - 2].time);
        }

        public void RecomputeKey(int key)
        {
            if (key == -1) return;
            x.RecomputeKey(key);
            y.RecomputeKey(key);
            z.RecomputeKey(key);
            rotX.RecomputeKey(key);
            rotY.RecomputeKey(key);
            rotZ.RecomputeKey(key);
            rotW.RecomputeKey(key);
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
            SetCurveSnapshot(time, (TransformTargetSnapshot)snapshot);
        }

        public TransformTargetSnapshot GetCurveSnapshot(float time)
        {
            var key = x.KeyframeBinarySearch(time);
            if (key == -1) return null;
            return new TransformTargetSnapshot
            {
                x = x.GetKeyframeByKey(key),
                y = y.GetKeyframeByKey(key),
                z = z.GetKeyframeByKey(key),
                rotX = rotX.GetKeyframeByKey(key),
                rotY = rotY.GetKeyframeByKey(key),
                rotZ = rotZ.GetKeyframeByKey(key),
                rotW = rotW.GetKeyframeByKey(key)
            };
        }

        public void SetCurveSnapshot(float time, TransformTargetSnapshot snapshot, bool makeDirty = true)
        {
            x.SetKeySnapshot(time, snapshot.x);
            y.SetKeySnapshot(time, snapshot.y);
            z.SetKeySnapshot(time, snapshot.z);
            rotX.SetKeySnapshot(time, snapshot.rotX);
            rotY.SetKeySnapshot(time, snapshot.rotY);
            rotZ.SetKeySnapshot(time, snapshot.rotZ);
            rotW.SetKeySnapshot(time, snapshot.rotW);
            if (makeDirty) dirty = true;
        }

        #endregion
    }
}
