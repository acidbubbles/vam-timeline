using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class TriggersAnimationTarget : AnimationTargetBase, IAtomAnimationTarget
    {
        public readonly SortedDictionary<int, AtomAnimationTrigger> triggersMap = new SortedDictionary<int, AtomAnimationTrigger>();
        public float[] keyframes { get; private set; } = new float[0];
        private readonly List<AtomAnimationTrigger> _triggers = new List<AtomAnimationTrigger>();

        public string name { get; set; }

        public TriggersAnimationTarget()
        {
        }

        public string GetShortName()
        {
            return name;
        }

        public void Sample(float previousClipTime)
        {
            foreach (var trigger in _triggers)
                trigger.Update(false, previousClipTime);
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
                return;
            }
        }

        public void RebuildKeyframes(AnimationTimelineTriggerHandler timelineHandler)
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
                trigger.timeLineHandler = timelineHandler;
                trigger.triggerStartTime = time;
                trigger.triggerEndTime = i == keyframes.Length - 1 ? timelineHandler.GetTotalTime() : keyframes[i + 1];
            }
        }

        public void SetKeyframe(float time, AtomAnimationTrigger value)
        {
            SetKeyframe(time.ToMilliseconds(), value);
        }

        public void SetKeyframe(int ms, AtomAnimationTrigger value)
        {
            if (value == null)
                triggersMap.Remove(ms);
            else
                triggersMap[ms] = value;
            dirty = true;
        }

        public void DeleteFrame(float time)
        {
            DeleteFrame(time.ToMilliseconds());
        }

        public void DeleteFrame(int ms)
        {
            triggersMap.Remove(ms);
            dirty = true;
        }

        public void AddEdgeFramesIfMissing(float animationLength)
        {
            if (!triggersMap.ContainsKey(0))
                SetKeyframe(0, new AtomAnimationTrigger());
            if (!triggersMap.ContainsKey(animationLength.ToMilliseconds()))
                SetKeyframe(animationLength.ToMilliseconds(), new AtomAnimationTrigger());
        }

        public float[] GetAllKeyframesTime()
        {
            return keyframes;
        }

        public float GetTimeClosestTo(float time)
        {
            var lower = 0f;
            var higher = keyframes
                .SkipWhile(t => { if (t < time) { lower = t; return true; } else { return false; } })
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
            SetCurveSnapshot(time, (TriggerSnapshot)snapshot);
        }

        public TriggerSnapshot GetCurveSnapshot(float time)
        {
            AtomAnimationTrigger trigger;
            if (!triggersMap.TryGetValue(time.ToMilliseconds(), out trigger)) return null;
            return new TriggerSnapshot { json = trigger.GetJSON() };
        }

        public void SetCurveSnapshot(float time, TriggerSnapshot snapshot)
        {
            AtomAnimationTrigger trigger;
            var ms = time.ToMilliseconds();
            if (!triggersMap.TryGetValue(ms, out trigger))
            {
                trigger = new AtomAnimationTrigger();
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

        public class Comparer : IComparer<TriggersAnimationTarget>
        {
            public int Compare(TriggersAnimationTarget t1, TriggersAnimationTarget t2)
            {
                return t1.name.CompareTo(t2.name);

            }
        }
    }

    public class AtomAnimationTrigger : AnimationTimelineTrigger, IDisposable
    {
        public Atom atom;

        public AtomAnimationTrigger()
        {
            SuperController.singleton.onAtomUIDRenameHandlers += OnAtomRename;
        }

        public override TriggerActionDiscrete CreateDiscreteActionStartInternal(int index = -1)
        {
            var discrete = base.CreateDiscreteActionStartInternal(index);
            if (discrete.receiverAtom == null) discrete.receiverAtom = atom;
            return discrete;
        }

        public override TriggerActionTransition CreateTransitionActionInternal(int index = -1)
        {
            var transition = base.CreateTransitionActionInternal(index);
            if (transition.receiverAtom == null) transition.receiverAtom = atom;
            return transition;
        }

        public override TriggerActionDiscrete CreateDiscreteActionEndInternal(int index = -1)
        {
            var discrete = base.CreateDiscreteActionEndInternal(index);
            if (discrete.receiverAtom == null) discrete.receiverAtom = atom;
            return discrete;
        }

        private void OnAtomRename(string oldName, string newName)
        {
            SyncAtomNames();
        }

        public void Dispose()
        {
            SuperController.singleton.onAtomUIDRenameHandlers -= OnAtomRename;
        }
    }
}
