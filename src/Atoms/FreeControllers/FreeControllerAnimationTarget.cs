using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class FreeControllerAnimationTarget : AnimationTargetBase, IAnimationTargetWithCurves
    {
        public readonly FreeControllerV3 controller;
        public readonly SortedDictionary<int, KeyframeSettings> settings = new SortedDictionary<int, KeyframeSettings>();
        public readonly AnimationCurve x = new AnimationCurve();
        public readonly AnimationCurve y = new AnimationCurve();
        public readonly AnimationCurve z = new AnimationCurve();
        public readonly AnimationCurve rotX = new AnimationCurve();
        public readonly AnimationCurve rotY = new AnimationCurve();
        public readonly AnimationCurve rotZ = new AnimationCurve();
        public readonly AnimationCurve rotW = new AnimationCurve();
        public readonly List<AnimationCurve> curves;

        public string name => controller.name;

        public FreeControllerAnimationTarget(FreeControllerV3 controller)
        {
            curves = new List<AnimationCurve> {
                x, y, z, rotX, rotY, rotZ, rotW
            };
            this.controller = controller;
        }

        public string GetShortName()
        {
            if (name.EndsWith("Control"))
                return name.Substring(0, name.Length - "Control".Length);
            return name;
        }

        #region Control

        public AnimationCurve GetLeadCurve()
        {
            return x;
        }

        public IEnumerable<AnimationCurve> GetCurves()
        {
            return curves;
        }

        public void Validate()
        {
            var leadCurve = GetLeadCurve();
            if (leadCurve.length < 2)
            {
                SuperController.LogError($"Target {name} has {leadCurve.length} frames");
                return;
            }
            if (this.settings.Count > leadCurve.length)
            {
                var curveKeys = leadCurve.keys.Select(k => k.time.ToMilliseconds()).ToList();
                var extraneousKeys = this.settings.Keys.Except(curveKeys);
                SuperController.LogError($"Target {name} has {leadCurve.length} frames but {this.settings.Count} settings. Attempting auto-repair.");
                foreach (var extraneousKey in extraneousKeys)
                    this.settings.Remove(extraneousKey);
            }
            if (this.settings.Count != leadCurve.length)
            {
                SuperController.LogError($"Target {name} has {leadCurve.length} frames but {this.settings.Count} settings");
                SuperController.LogError($"  Target  : {string.Join(", ", leadCurve.keys.Select(k => k.time.ToString()).ToArray())}");
                SuperController.LogError($"  Settings: {string.Join(", ", this.settings.Select(k => (k.Key / 1000f).ToString()).ToArray())}");
                return;
            }
            var settings = this.settings.Select(s => s.Key);
            var keys = leadCurve.keys.Select(k => k.time.ToMilliseconds()).ToArray();
            if (!settings.SequenceEqual(keys))
            {
                SuperController.LogError($"Target {name} has different times for settings and keyframes");
                SuperController.LogError($"Settings: {string.Join(", ", settings.Select(s => s.ToString()).ToArray())}");
                SuperController.LogError($"Keyframes: {string.Join(", ", keys.Select(k => k.ToString()).ToArray())}");
                return;
            }
        }

        public void ReapplyCurveTypes()
        {
            if (x.length < 2) return;

            foreach (var setting in settings)
            {
                if (setting.Value.curveType == CurveTypeValues.LeaveAsIs)
                    continue;

                var time = (setting.Key / 1000f).Snap();
                foreach (var curve in curves)
                {
                    ApplyCurve(curve, time, setting.Value.curveType);
                }
            }
        }

        public static void ApplyCurve(AnimationCurve curve, float time, string curveType)
        {
            var key = curve.KeyframeBinarySearch(time);
            if (key == -1) return;
            var keyframe = curve[key];
            var before = key > 0 ? (Keyframe?)curve[key - 1] : null;
            var next = key < curve.length - 1 ? (Keyframe?)curve[key + 1] : null;

            switch (curveType)
            {
                case null:
                case "":
                    return;
                case CurveTypeValues.Flat:
                    keyframe.inTangent = 0f;
                    keyframe.outTangent = 0f;
                    curve.MoveKey(key, keyframe);
                    break;
                case CurveTypeValues.Linear:
                    keyframe.inTangent = AnimationCurveExtensions.CalculateLinearTangent(before, keyframe);
                    keyframe.outTangent = AnimationCurveExtensions.CalculateLinearTangent(keyframe, next);
                    curve.MoveKey(key, keyframe);
                    break;
                case CurveTypeValues.Bounce:
                    // Increasing kinetic energy
                    keyframe.inTangent = AnimationCurveExtensions.CalculateLinearTangent(before, keyframe) * 2f;
                    // Lower coefficient of restitution
                    keyframe.outTangent = -keyframe.inTangent * 0.8f;
                    curve.MoveKey(key, keyframe);
                    break;
                case CurveTypeValues.Smooth:
                    curve.SmoothTangents(key, 0f);
                    break;
                case CurveTypeValues.LinearFlat:
                    keyframe.inTangent = AnimationCurveExtensions.CalculateLinearTangent(before, keyframe);
                    keyframe.outTangent = 0f;
                    curve.MoveKey(key, keyframe);
                    break;
                case CurveTypeValues.FlatLinear:
                    keyframe.inTangent = 0f;
                    keyframe.outTangent = AnimationCurveExtensions.CalculateLinearTangent(keyframe, next);
                    curve.MoveKey(key, keyframe);
                    break;
                case CurveTypeValues.CopyPrevious:
                    if (before != null)
                    {
                        keyframe.value = before.Value.value;
                        keyframe.inTangent = 0f;
                        keyframe.outTangent = 0f;
                        curve.MoveKey(key, keyframe);
                    }
                    break;
                default:
                    throw new NotSupportedException($"Curve type {curveType} is not supported");
            }
        }

        public void ReapplyCurvesToClip(AnimationClip clip)
        {
            var path = GetRelativePath();
            clip.SetCurve(path, typeof(Transform), "localPosition.x", x);
            clip.SetCurve(path, typeof(Transform), "localPosition.y", y);
            clip.SetCurve(path, typeof(Transform), "localPosition.z", z);
            clip.SetCurve(path, typeof(Transform), "localRotation.x", rotX);
            clip.SetCurve(path, typeof(Transform), "localRotation.y", rotY);
            clip.SetCurve(path, typeof(Transform), "localRotation.z", rotZ);
            clip.SetCurve(path, typeof(Transform), "localRotation.w", rotW);
        }

        public void SmoothLoop()
        {
            foreach (var curve in curves)
            {
                curve.SmoothLoop();
            }
        }

        private string GetRelativePath()
        {
            // TODO: This is probably what breaks animations with parenting
            var root = controller.containingAtom.transform;
            var target = controller.transform;
            var parts = new List<string>();
            Transform t = target;
            while (t != root && t != t.root)
            {
                parts.Add(t.name);
                t = t.parent;
            }
            parts.Reverse();
            return string.Join("/", parts.ToArray());
        }

        #endregion

        #region Keyframes control

        public int SetKeyframeToCurrentTransform(float time)
        {
            return SetKeyframe(time, controller.transform.localPosition, controller.transform.localRotation);
        }

        public int SetKeyframe(float time, Vector3 localPosition, Quaternion locationRotation)
        {
            var key = x.SetKeyframe(time, localPosition.x);
            y.SetKeyframe(time, localPosition.y);
            z.SetKeyframe(time, localPosition.z);
            rotX.SetKeyframe(time, locationRotation.x);
            rotY.SetKeyframe(time, locationRotation.y);
            rotZ.SetKeyframe(time, locationRotation.z);
            rotW.SetKeyframe(time, locationRotation.w);
            var ms = time.ToMilliseconds();
            if (!settings.ContainsKey(ms))
                settings[ms] = new KeyframeSettings { curveType = CurveTypeValues.Smooth };
            dirty = true;
            return key;
        }

        public void DeleteFrame(float time)
        {
            var key = GetLeadCurve().KeyframeBinarySearch(time);
            if (key != -1) DeleteFrameByKey(key);
        }

        public void DeleteFrameByKey(int key)
        {
            var settingIndex = settings.Remove(GetLeadCurve()[key].time.ToMilliseconds());
            foreach (var curve in curves)
            {
                curve.RemoveKey(key);
            }
            dirty = true;
        }

        public float[] GetAllKeyframesTime()
        {
            var curve = x;
            var keyframes = new float[curve.length];
            for (var i = 0; i < curve.length; i++)
                keyframes[i] = curve[i].time;
            return keyframes;
        }

        #endregion

        #region Curves

        public void ChangeCurve(float time, string curveType)
        {
            if (string.IsNullOrEmpty(curveType)) return;

            UpdateSetting(time, curveType, false);
            dirty = true;
        }

        #endregion

        #region Snapshots

        public FreeControllerV3Snapshot GetCurveSnapshot(float time)
        {
            if (x.KeyframeBinarySearch(time) == -1) return null;
            KeyframeSettings setting;
            return new FreeControllerV3Snapshot
            {
                x = x[x.KeyframeBinarySearch(time)],
                y = y[y.KeyframeBinarySearch(time)],
                z = z[z.KeyframeBinarySearch(time)],
                rotX = rotX[rotX.KeyframeBinarySearch(time)],
                rotY = rotY[rotY.KeyframeBinarySearch(time)],
                rotZ = rotZ[rotZ.KeyframeBinarySearch(time)],
                rotW = rotW[rotW.KeyframeBinarySearch(time)],
                curveType = settings.TryGetValue(time.ToMilliseconds(), out setting) ? setting.curveType : CurveTypeValues.LeaveAsIs
            };
        }

        public void SetCurveSnapshot(float time, FreeControllerV3Snapshot snapshot, bool dirty = true)
        {
            x.SetKeySnapshot(time, snapshot.x);
            y.SetKeySnapshot(time, snapshot.y);
            z.SetKeySnapshot(time, snapshot.z);
            rotX.SetKeySnapshot(time, snapshot.rotX);
            rotY.SetKeySnapshot(time, snapshot.rotY);
            rotZ.SetKeySnapshot(time, snapshot.rotZ);
            rotW.SetKeySnapshot(time, snapshot.rotW);
            UpdateSetting(time, snapshot.curveType, true);
            if (dirty) base.dirty = true;
        }

        private void UpdateSetting(float time, string curveType, bool create)
        {
            var ms = time.ToMilliseconds();
            if (settings.ContainsKey(ms))
                settings[ms].curveType = curveType;
            else if (create)
                settings.Add(ms, new KeyframeSettings { curveType = curveType });
        }

        #endregion

        #region Interpolation


        public bool Interpolate(float playTime, float maxDistanceDelta, float maxRadiansDelta)
        {
            var targetLocalPosition = new Vector3
            {
                x = x.Evaluate(playTime),
                y = y.Evaluate(playTime),
                z = z.Evaluate(playTime)
            };

            var targetLocalRotation = new Quaternion
            {
                x = rotX.Evaluate(playTime),
                y = rotY.Evaluate(playTime),
                z = rotZ.Evaluate(playTime),
                w = rotW.Evaluate(playTime)
            };

            controller.transform.localPosition = Vector3.MoveTowards(controller.transform.localPosition, targetLocalPosition, maxDistanceDelta);
            controller.transform.localRotation = Quaternion.RotateTowards(controller.transform.localRotation, targetLocalRotation, maxRadiansDelta);

            var posDistance = Vector3.Distance(controller.transform.localPosition, targetLocalPosition);
            // NOTE: We skip checking for rotation reached because in some cases we just never get even near the target rotation.
            // var rotDistance = Quaternion.Dot(Controller.transform.localRotation, targetLocalRotation);
            return posDistance < 0.01f;
        }

        #endregion

        public bool TargetsSameAs(IAnimationTargetWithCurves target)
        {
            var t = target as FreeControllerAnimationTarget;
            if (t == null) return false;
            return t.controller == controller;
        }

        public class Comparer : IComparer<FreeControllerAnimationTarget>
        {
            public int Compare(FreeControllerAnimationTarget t1, FreeControllerAnimationTarget t2)
            {
                return t1.controller.name.CompareTo(t2.controller.name);

            }
        }

        internal void SmoothNeighbors(int key)
        {
            if (key == -1) return;
            x.SmoothTangents(key, 1f);
            if (key > 0) x.SmoothTangents(key - 1, 1f);
            if (key < x.length - 1) x.SmoothTangents(key + 1, 1f);

            y.SmoothTangents(key, 1f);
            if (key > 0) y.SmoothTangents(key - 1, 1f);
            if (key < y.length - 1) y.SmoothTangents(key + 1, 1f);

            z.SmoothTangents(key, 1f);
            if (key > 0) z.SmoothTangents(key - 1, 1f);
            if (key < z.length - 1) z.SmoothTangents(key + 1, 1f);
        }
    }
}
