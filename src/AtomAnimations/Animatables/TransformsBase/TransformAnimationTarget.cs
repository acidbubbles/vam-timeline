using System.Collections.Generic;
using UnityEngine;

namespace VamTimeline
{
    public abstract class TransformAnimationTargetBase<TAnimatableRef> : CurveAnimationTargetBase<TAnimatableRef> where TAnimatableRef : AnimatableRefBase, IAnimatableRefWithTransform
    {
        public bool targetsPosition;
        public bool targetsRotation;

        public Vector3AnimationTarget<TAnimatableRef> position { get; }
        public QuaternionAnimationTarget<TAnimatableRef> rotation { get; }
        public readonly List<BezierAnimationCurve> curves;
        public int length => targetsPosition ? position.length : rotation.length;

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

        public override bool selected
        {
            get
            {
                if(!animatableRef.selected) return false;
                return (!targetsPosition || targetsPosition && animatableRef.selectedPosition) && (!targetsRotation || targetsRotation && animatableRef.selectedRotation);
            }
            set
            {
                var wasSelected = animatableRef.selected;
                var wasSelectedPosition = animatableRef.selectedPosition;
                var wasSelectedRotation = animatableRef.selectedRotation;
                if (value)
                {
                    animatableRef.selected = true;
                    if (targetsPosition) animatableRef.selectedPosition = true;
                    if (targetsRotation) animatableRef.selectedRotation = true;
                }
                else
                {
                    if (targetsPosition) animatableRef.selectedPosition = false;
                    if (targetsRotation) animatableRef.selectedRotation = false;
                    if (!animatableRef.selectedPosition && !animatableRef.selectedRotation) animatableRef.selected = false;
                }

                if (wasSelected != animatableRef.selected || wasSelectedPosition != animatableRef.selectedPosition || wasSelectedRotation != animatableRef.selectedRotation)
                    animatableRef.onSelectedChanged.Invoke();
            }
        }

        protected TransformAnimationTargetBase(TAnimatableRef animatableRef, bool targetsPos, Vector3AnimationTarget<TAnimatableRef> pos, bool targetsRot, QuaternionAnimationTarget<TAnimatableRef> rot)
            : base(animatableRef)
        {
            targetsPosition = targetsPos;
            targetsRotation = targetsRot;
            position = pos;
            rotation = rot;
            curves = new List<BezierAnimationCurve>();
            if (targetsPosition) curves.AddRange(pos.GetCurves());
            if (targetsRotation) curves.AddRange(rot.GetCurves());
        }

        #region Display

        public override string GetShortName()
        {
            if (!targetsPosition)
                return animatableRef.GetShortName() + " (Pos)";
            else if(!targetsRotation)
                return animatableRef.GetShortName() + " (Rot)";
            else
                return animatableRef.GetShortName();
        }

        public override string GetFullName()
        {
            if (!targetsPosition)
                return animatableRef.GetFullName() + " (Position)";
            else if(!targetsRotation)
                return animatableRef.GetFullName() + " (Rotation)";
            else
                return animatableRef.GetFullName();
        }

        #endregion

        #region Control

        public override BezierAnimationCurve GetLeadCurve()
        {
            return targetsPosition ? position.GetLeadCurve() : rotation.GetLeadCurve();
        }

        public override IEnumerable<BezierAnimationCurve> GetCurves()
        {
            return curves;
        }

        public void Validate(float animationLength)
        {
            if(targetsPosition) position.Validate(animationLength);
            if (targetsRotation) rotation.Validate(animationLength);
            if (targetsPosition && targetsRotation && position.length != rotation.length)
            {
                SuperController.LogError($"Mismatched rotation and position data on controller {name}. {position.length} position keys and {rotation.length} rotation keys found. Missing data will be created.");
                RepairMismatchedCurves();
            }
        }

        private void RepairMismatchedCurves()
        {
            if (position.length != rotation.length)
            {
                for (var i = 0; i < position.length; i++)
                {
                    var time = position.x.keys[i].time;
                    SetKeyframeByTime(time, EvaluatePosition(time), EvaluateRotation(time), position.x.keys[i].curveType, false);
                }
            }
            if (position.length != rotation.length)
            {
                for (var i = 0; i < rotation.length; i++)
                {
                    var time = rotation.rotW.keys[i].time;
                    SetKeyframeByTime(time, EvaluatePosition(time), EvaluateRotation(time), rotation.rotW.keys[i].curveType, false);
                }
            }
        }

        public void ComputeCurves()
        {
            if(targetsPosition) position.ComputeCurves();
            if(targetsRotation) rotation.ComputeCurves();
        }

        #endregion

        #region Keyframe control

        public int SetKeyframeByTime(float time, Vector3 localPosition, Quaternion locationRotation, int curveType = CurveTypeValues.Undefined, bool makeDirty = true)
        {
            curveType = SelectCurveType(time, curveType);
            var key = 0;
            if(targetsPosition) key = position.SetKeyframeByTime(time, localPosition, curveType, makeDirty);
            if(targetsRotation) key = rotation.SetKeyframeByTime(time, locationRotation, curveType, makeDirty);
            if (makeDirty) dirty = true;
            return key;
        }

        public int SetKeyframeByKey(int key, Vector3 localPosition, Quaternion locationRotation)
        {
            if(targetsPosition) position.SetKeyframeByKey(key, localPosition);
            if(targetsRotation) rotation.SetKeyframeByKey(key, locationRotation);
            dirty = true;
            return key;
        }

        public void DeleteFrame(float time)
        {
            if(targetsPosition) position.DeleteFrame(time);
            if(targetsRotation) rotation.DeleteFrame(time);
            dirty = true;
        }

        public void AddEdgeFramesIfMissing(float animationLength)
        {
            if(targetsPosition) position.AddEdgeFramesIfMissing(animationLength);
            if(targetsRotation) rotation.AddEdgeFramesIfMissing(animationLength);
        }

        public void RecomputeKey(int key)
        {
            if (key == -1) return;
            if(targetsPosition) position.RecomputeKey(key);
            if(targetsRotation) rotation.RecomputeKey(key);
        }

        public float[] GetAllKeyframesTime()
        {
            return targetsPosition ? position.GetAllKeyframesTime() : rotation.GetAllKeyframesTime();
        }

        public int[] GetAllKeyframesKeys()
        {
            return targetsPosition ? position.GetAllKeyframesKeys() : rotation.GetAllKeyframesKeys();
        }

        public float GetTimeClosestTo(float time)
        {
            return targetsPosition ? position.GetTimeClosestTo(time) : rotation.GetTimeClosestTo(time);
        }

        public bool HasKeyframe(float time)
        {
            return targetsPosition ? position.HasKeyframe(time) : rotation.HasKeyframe(time);
        }

        #endregion

        #region Evaluate

        public Vector3 EvaluatePosition(float time)
        {
            return position.EvaluatePosition(time);
        }

        public Quaternion EvaluateRotation(float time)
        {
            return rotation.EvaluateRotation(time);
        }

        public Quaternion GetRotationAtKeyframe(int key)
        {
            return rotation.EvaluateRotation(key);
        }

        public float GetKeyframeTime(int key)
        {
            return position.GetKeyframeTime(key);
        }

        public Vector3 GetKeyframePosition(int key)
        {
            return position.GetKeyframePosition(key);
        }

        public Quaternion GetKeyframeRotation(int key)
        {
            return rotation.GetKeyframeRotation(key);
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
            var positionSnapshot = targetsPosition ? position.GetCurveSnapshot(time) : null;
            var rotationSnapshot = targetsRotation ? rotation.GetCurveSnapshot(time) : null;
            if (positionSnapshot == null && rotationSnapshot == null) return null;
            return new TransformTargetSnapshot
            {
                position = positionSnapshot, rotation = rotationSnapshot
            };
        }

        public void SetCurveSnapshot(float time, TransformTargetSnapshot snapshot, bool makeDirty = true)
        {
            if(targetsPosition && snapshot.position != null) position.SetCurveSnapshot(time, snapshot.position, makeDirty);
            if(targetsRotation && snapshot.rotation != null) rotation.SetCurveSnapshot(time, snapshot.rotation, makeDirty);
            if (makeDirty) dirty = true;
        }

        #endregion

        #region Conversion

        public TransformStruct[] ToTransformArray()
        {
            var keyframes = new TransformStruct[position.x.length];
            for (var i = 0; i < position.x.length; i++)
            {
                keyframes[i] = new TransformStruct
                {
                    time = position.x.keys[i].time,
                    position = GetKeyframePosition(i),
                    rotation = GetKeyframeRotation(i),
                    curveType = position.x.keys[i].curveType
                };
            }
            return keyframes;
        }

        public void SetTransformArray(TransformStruct[] transforms)
        {
            StartBulkUpdates();
            position.x.keys.Clear();
            position.y.keys.Clear();
            position.z.keys.Clear();
            rotation.rotX.keys.Clear();
            rotation.rotY.keys.Clear();
            rotation.rotZ.keys.Clear();
            rotation.rotW.keys.Clear();
            dirty = true;
            try
            {
                for (var i = 0; i < transforms.Length; i++)
                {
                    var transform = transforms[i];
                    SetKeyframeByTime(transform.time, transform.position, transform.rotation, transform.curveType);
                }
            }
            finally
            {
                EndBulkUpdates();
            }
        }

        #endregion
    }

    public struct TransformStruct
    {
        public float time;
        public Vector3 position;
        public Quaternion rotation;
        public int curveType;
    }
}
