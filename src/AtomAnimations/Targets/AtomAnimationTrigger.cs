using System;
using SimpleJSON;

namespace VamTimeline
{
    public class AtomAnimationTrigger : Trigger, IDisposable
    {
        public Atom atom;
        public float startTime;
        public float endTime;

        public AtomAnimationTrigger()
        {
            SuperController.singleton.onAtomUIDRenameHandlers += OnAtomRename;
        }

        public void Update(float clipTime)
        {
            if (clipTime >= startTime && (clipTime < endTime || startTime == endTime))
            {
                active = true;
                transitionInterpValue = (clipTime - startTime) / (endTime - startTime);
            }
            else if (active)
            {
                transitionInterpValue = clipTime < startTime ? 0f : 1f;
                active = false;
            }
        }

        public void Leave()
        {
            active = false;
        }

        #region JSON

        public override JSONClass GetJSON()
        {
            JSONClass jSON = base.GetJSON();
            jSON["startTime"].AsFloat = startTime;
            jSON["endTime"].AsFloat = endTime;
            return jSON;
        }

        public override void RestoreFromJSON(JSONClass jc)
        {
            base.RestoreFromJSON(jc);
            if (jc["startTime"] != null)
                startTime = jc["startTime"].AsFloat;
            if (jc["endTime"] != null)
                endTime = jc["endTime"].AsFloat;
        }

        #endregion

        #region UI

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

        #endregion

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
