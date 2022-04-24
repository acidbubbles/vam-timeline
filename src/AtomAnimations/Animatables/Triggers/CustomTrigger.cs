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

        public void Sync(bool playbackEnabled, float clipTime)
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (playbackEnabled && clipTime >= startTime && (clipTime < endTime || startTime == endTime))
            {
                active = true;
                transitionInterpValue = (clipTime - startTime) / (endTime - startTime);
            }
            else if (active)
            {
                active = false;
            }
        }

        public void SyncAudio(float time)
        {
            if (!active) return;
            foreach (var action in discreteActionsStart)
            {
                var audioReceiver = action.receiver as AudioSourceControl;
                if (audioReceiver == null) continue;
                audioReceiver.audioSource.time = Mathf.Clamp(time - startTime, 0f, action.audioClip.sourceClip.length);
                // TODO: If not playing, auto-stop audio in 0.5s
                // TODO: When stopping the clip, stop all audio (or when exiting the trigger, see Leave and Sync)
            }
        }

        public new void Update()
        {
            base.Update();
        }

        public void Leave()
        {
            active = false;
        }

        #region JSON

        #if(VAM_GT_1_20)
        public override JSONClass GetJSON(string subScenePrefix)
        {
            var jc = base.GetJSON(subScenePrefix);
        #else
        public override JSONClass GetJSON()
        {
            var jc = base.GetJSON();
        #endif
            jc["startTime"].AsFloat = startTime;
            jc["endTime"].AsFloat = endTime;
            return jc;
        }

        #if(VAM_GT_1_20)
        public override void RestoreFromJSON(JSONClass jc, string subScenePrefix, bool isMerge)
        {
            base.RestoreFromJSON(jc, subScenePrefix, isMerge);
        #else
        public override void RestoreFromJSON(JSONClass jc)
        {
            base.RestoreFromJSON(jc);
        #endif
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
