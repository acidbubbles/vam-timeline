using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class TriggersTrackAnimationTarget : AnimationTargetBase<TriggersTrackRef>, IAtomAnimationTarget
    {
        public readonly SortedDictionary<int, CustomTrigger> triggersMap = new SortedDictionary<int, CustomTrigger>();
        private float[] keyframes { get; set; } = new float[0];
        private readonly List<CustomTrigger> _triggers = new List<CustomTrigger>();

        public TriggersTrackAnimationTarget(TriggersTrackRef triggersTrackRef)
            : base(triggersTrackRef)
        {
        }

        public void Sample(float time)
        {
            foreach (var trigger in _triggers)
                trigger.Update(time);
        }

        public void Refresh()
        {
            foreach (var trigger in triggersMap.Select(kvp => kvp.Value))
            {
                // Only way to refresh a missing atom (self)
                trigger.RestoreFromJSON(trigger.GetJSON());
            }
        }

        public void Validate(float animationLength)
        {
            foreach (var trigger in _triggers)
            {
                trigger.Validate();
            }
            if (triggersMap.Count < 2)
            {
                SuperController.LogError($"Target {name} has {triggersMap.Count} frames");
                return;
            }
            if (!triggersMap.ContainsKey(0))
            {
                SuperController.LogError($"Target {name} has no start frame");
                return;
            }
            if (!triggersMap.ContainsKey(animationLength.ToMilliseconds()))
            {
                SuperController.LogError($"Target {name} ends with frame {triggersMap.Keys.OrderBy(k => k).Last()} instead of expected {animationLength.ToMilliseconds()}");
            }
        }

        public void RebuildKeyframes(float animationLength)
        {
            keyframes = new float[triggersMap.Count];
            _triggers.Clear();

            var i = 0;
            foreach (var kvp in triggersMap.OrderBy(x => x.Key))
            {
                keyframes[i++] = (kvp.Key / 1000f).Snap();
                _triggers.Add(kvp.Value);
            }

            for (i = 0; i < keyframes.Length; i++)
            {
                var time = keyframes[i];
                var trigger = _triggers[i];
                trigger.startTime = time;
                trigger.endTime = i == keyframes.Length - 1 ? animationLength : keyframes[i + 1];
            }
        }

        public void SetKeyframe(float time, CustomTrigger value)
        {
            SetKeyframe(time.ToMilliseconds(), value);
        }

        public void SetKeyframe(int ms, CustomTrigger value)
        {
            if (value == null)
                DeleteFrameByMs(ms);
            else
                triggersMap[ms] = value;
            dirty = true;
        }

        public void DeleteFrame(float time)
        {
            DeleteFrameByMs(time.ToMilliseconds());
        }

        public void DeleteFrameByMs(int ms)
        {
            CustomTrigger trigger;
            if (triggersMap.TryGetValue(ms, out trigger))
                trigger.Dispose();
            triggersMap.Remove(ms);
            dirty = true;
        }

        public void AddEdgeFramesIfMissing(float animationLength)
        {
            if (!triggersMap.ContainsKey(0))
                SetKeyframe(0, new CustomTrigger());
            if (!triggersMap.ContainsKey(animationLength.ToMilliseconds()))
                SetKeyframe(animationLength.ToMilliseconds(), new CustomTrigger());
        }

        public float[] GetAllKeyframesTime()
        {
            return keyframes;
        }

        public float GetTimeClosestTo(float time)
        {
            var lower = 0f;
            var higher = keyframes
                .SkipWhile(t =>
                {
                    if (t < time) { lower = t; return true; }

                    return false;
                })
                .FirstOrDefault();
            return Mathf.Abs(time - lower) < Mathf.Abs(higher - time) ? lower : higher;
        }

        public bool HasKeyframe(float time)
        {
            return triggersMap.ContainsKey(time.ToMilliseconds());
        }

        public bool TargetsSameAs(IAtomAnimationTarget target)
        {
            return target.name == name;
        }

        #region Snapshots

        ISnapshot IAtomAnimationTarget.GetSnapshot(float time)
        {
            return GetCurveSnapshot(time);
        }
        void IAtomAnimationTarget.SetSnapshot(float time, ISnapshot snapshot)
        {
            SetCurveSnapshot(time, (TriggerTargetSnapshot)snapshot);
        }

        public TriggerTargetSnapshot GetCurveSnapshot(float time)
        {
            CustomTrigger trigger;
            if (!triggersMap.TryGetValue(time.ToMilliseconds(), out trigger)) return null;
            return new TriggerTargetSnapshot { json = trigger.GetJSON() };
        }

        public void SetCurveSnapshot(float time, TriggerTargetSnapshot snapshot)
        {
            CustomTrigger trigger;
            var ms = time.ToMilliseconds();
            if (!triggersMap.TryGetValue(ms, out trigger))
            {
                trigger = new CustomTrigger();
                SetKeyframe(ms, trigger);
            }
            trigger.RestoreFromJSON(snapshot.json);
            dirty = true;
        }

        #endregion

        public override void Dispose()
        {
            base.Dispose();
            foreach (var target in triggersMap.Select(kvp => kvp.Value))
                target.Dispose();
        }

        public override string ToString()
        {
            return $"[Trigger Target: {name}]";
        }

        public class Comparer : IComparer<TriggersTrackAnimationTarget>
        {
            public int Compare(TriggersTrackAnimationTarget t1, TriggersTrackAnimationTarget t2)
            {
                return string.Compare(t1.name, t2.name, StringComparison.Ordinal);

            }
        }
    }
}
