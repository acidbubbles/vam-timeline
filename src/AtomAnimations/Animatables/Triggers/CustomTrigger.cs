using System;
using SimpleJSON;
using UnityEngine;

namespace VamTimeline
{
    public class CustomTrigger : Trigger, IDisposable
    {
        public Atom atom;
        public float startTime;
        public float endTime;

        public CustomTrigger()
        {
            SuperController.singleton.onAtomUIDRenameHandlers += OnAtomRename;
        }

        public void Update(float clipTime)
        {
            if (clipTime >= startTime && (clipTime < endTime || startTime == endTime))
            {
                active = true;
                transitionInterpValue = (clipTime - startTime) / (endTime - startTime);
                Update();
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

        public override JSONClass GetJSON(string subScenePrefix = null)
        {
            var jc = base.GetJSON(subScenePrefix);
            jc["startTime"].AsFloat = startTime;
            jc["endTime"].AsFloat = endTime;
            return jc;
        }

        public override void RestoreFromJSON(JSONClass jc, string subScenePrefix, bool isMerge)
        {
            base.RestoreFromJSON(jc, subScenePrefix, isMerge);
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

        public void SetPanelParent(Transform parent)
        {
            if (triggerActionsPanel != null)
            {
                triggerActionsPanel.SetParent(parent, false);
                triggerActionsPanel.SetAsLastSibling();
            }

            if (discreteActionsStart != null)
            {
                foreach (var start in discreteActionsStart)
                {
                    if (start.triggerActionPanel != null)
                    {
                        start.triggerActionPanel.SetParent(parent, false);
                        start.triggerActionPanel.SetAsLastSibling();
                    }
                }
            }

            if (transitionActions != null)
            {
                foreach (var transition in transitionActions)
                {
                    if (transition.triggerActionPanel != null)
                    {
                        transition.triggerActionPanel.SetParent(parent, false);
                        transition.triggerActionPanel.SetAsLastSibling();
                    }
                }
            }

            if (discreteActionsEnd != null)
            {
                foreach (var end in discreteActionsEnd)
                {
                    if (end.triggerActionPanel != null)
                    {
                        end.triggerActionPanel.SetParent(parent, false);
                        end.triggerActionPanel.SetAsLastSibling();
                    }
                }
            }
        }

        public void ClosePanel()
        {
            CloseTriggerActionsPanel();

            if (discreteActionsStart != null)
            {
                foreach (var start in discreteActionsStart)
                {
                    if (start.triggerActionPanel != null)
                    {
                        start.triggerActionPanel.gameObject.SetActive(false);
                    }
                }
            }

            if (transitionActions != null)
            {
                foreach (var transition in transitionActions)
                {
                    if (transition.triggerActionPanel != null)
                    {
                        transition.triggerActionPanel.gameObject.SetActive(false);
                    }
                }
            }

            if (discreteActionsEnd != null)
            {
                foreach (var end in discreteActionsEnd)
                {
                    if (end.triggerActionPanel != null)
                    {
                        end.triggerActionPanel.gameObject.SetActive(false);
                    }
                }
            }
        }
    }
}
