using System;
using System.Collections.Generic;
using System.Linq;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class TriggersAnimationTarget : AnimationTargetBase, IAtomAnimationTarget
    {
        public readonly Dictionary<int, AtomAnimationTrigger> triggersMap = new Dictionary<int, AtomAnimationTrigger>();
        private float[] _keyframes = new float[0];
        private readonly List<AtomAnimationTrigger> _triggers = new List<AtomAnimationTrigger>();

        public string name => "Triggers";

        public TriggersAnimationTarget()
        {
        }

        public string GetShortName()
        {
            return "Triggers";
        }

        public void Sample(float previousClipTime)
        {
            foreach (var trigger in _triggers)
                trigger.Update(false, previousClipTime);
        }

        public void Validate()
        {
            foreach (var trigger in _triggers)
            {
                trigger.Validate();
            }
        }

        public void RebuildKeyframes(AnimationTimelineTriggerHandler timelineHandler)
        {
            _keyframes = new float[triggersMap.Count];
            _triggers.Clear();

            var i = 0;
            foreach (var kvp in triggersMap.OrderBy(x => x.Key))
            {
                _keyframes[i++] = kvp.Key / 1000f;
                _triggers.Add(kvp.Value);
            }

            for (i = 0; i < _keyframes.Length; i++)
            {
                var time = _keyframes[i];
                var trigger = _triggers[i];
                trigger.timeLineHandler = timelineHandler;
                trigger.triggerStartTime = time;
                trigger.triggerEndTime = i == _keyframes.Length - 1 ? timelineHandler.GetTotalTime() : _keyframes[i + 1];
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

        public float[] GetAllKeyframesTime()
        {
            return _keyframes;
        }

        public bool HasKeyframe(float time)
        {
            return triggersMap.ContainsKey(time.ToMilliseconds());
        }

        // TODO: Makes sense?
        public bool TargetsSameAs(IAtomAnimationTarget target)
        {
            return false;
        }

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
