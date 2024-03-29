using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using SimpleJSON;
using UnityEngine;

namespace VamTimeline
{
    public class CustomTrigger : Trigger, IDisposable
    {
        public Atom atom;
        public float startTime;
        public float endTime;

        public bool shouldBeActive;

        public JSONClass pendingJSON;

        private readonly Logger _logger;

        public CustomTrigger(Logger logger)
        {
            _logger = logger;
            SuperController.singleton.onAtomUIDRenameHandlers += OnAtomRename;
        }

        public void SyncActive(float clipTime)
        {
            shouldBeActive = IsInsideTimeRange(clipTime);
        }

        public void SyncLeave(bool live)
        {
            if (shouldBeActive || !active) return;
            Leave(live);
            if (_logger.triggersInvoked)
                LogTriggers(discreteActionsStart, "end");
        }

        public void SyncTime(float clipTime, bool live)
        {
            if (!shouldBeActive) return;
            transitionInterpValue = (clipTime - startTime) / (endTime - startTime);
            if (active) return;
            try
            {
                active = true;
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline: External activate trigger crashed, some triggers might not have been called: {exc}");
            }
            if (live)
                SyncAudio(clipTime);
            if (_logger.triggersInvoked)
                LogTriggers(discreteActionsStart, "start");
        }

        private void LogTriggers(IList<TriggerActionDiscrete> actions, string label)
        {
            for (var i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                if (action.receiver == null || action.receiverAtom == null) continue;
                _logger.Log(_logger.triggersCategory, $"Invoking {label} trigger (time {startTime:0.000}) {action.receiverAtom.name}/{action.receiver.name}/{action.receiverTargetName}");
            }
        }

        [MethodImpl(256)]
        public bool IsInsideTimeRange(float clipTime)
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            return clipTime >= startTime && (clipTime < endTime || startTime == endTime);
        }

        public void SyncAudio(float clipTime, bool forcePlay = false)
        {
            if (!active) return;
            for (var i = 0; i < discreteActionsStart.Count; i++)
            {
                var action = discreteActionsStart[i];
                if (action?.audioClip?.sourceClip == null) continue;
                var audioReceiver = action.receiver as AudioSourceControl;
                if (ReferenceEquals(audioReceiver, null)) continue;
                if (ReferenceEquals(audioReceiver.audioSource, null)) continue;
                if (audioReceiver.audioSource.clip != action.audioClip.clipToPlay) continue;
                if (forcePlay && !audioReceiver.audioSource.isPlaying)
                {
                    audioReceiver.PlayNow(action.audioClip);
                }

                audioReceiver.audioSource.time = Mathf.Clamp(clipTime - startTime, 0f, action.audioClip.sourceClip.length);
            }
        }

        public new void Update()
        {
            base.Update();
        }

        public void Leave(bool live)
        {
            if (!active) return;
            if (live)
                ForceStopAudioReceivers();
            try
            {
                active = false;
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline: External deactivate trigger crashed, some triggers might not have been called: {exc}");
            }
        }

        private void ForceStopAudioReceivers()
        {
            for (var i = 0; i < discreteActionsStart.Count; i++)
            {
                var action = discreteActionsStart[i];
                if (ReferenceEquals(action?.audioClip?.sourceClip, null)) continue;
                var audioReceiver = action.receiver as AudioSourceControl;
                if (ReferenceEquals(audioReceiver, null)) continue;
                if (ReferenceEquals(audioReceiver.audioSource, null)) continue;
                if (audioReceiver.audioSource.clip != action.audioClip.clipToPlay) continue;
                audioReceiver.audioSource.Stop();
            }
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

        public int Count()
        {
            return discreteActionsStart.Count + transitionActions.Count + discreteActionsEnd.Count;
        }
    }
}
