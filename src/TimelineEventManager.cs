using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class TimelineEventManager
    {
        public AtomAnimation animation;

        private readonly List<JSONStorable> _otherTimelines = new List<JSONStorable>();
        private readonly Atom _containingAtom;
        private readonly JSONStorable _plugin;
        private bool _syncing = false;

        public TimelineEventManager(Atom containingAtom, JSONStorable plugin)
        {
            _containingAtom = containingAtom;
            _plugin = plugin;
        }

        #region Unity integration

        public void Ready()
        {
            ScanForAtoms();
            BroadcastToTimelines(nameof(ITimelineListener.OnTimelineAnimationReady));
        }

        public void Unready()
        {
            BroadcastToTimelines(nameof(ITimelineListener.OnTimelineAnimationDisabled));
        }

        public void OnTimelineAnimationReady(JSONStorable storable)
        {
            if (storable == _plugin || _otherTimelines.Contains(storable)) return;
            _otherTimelines.Add(storable);
        }

        public void OnTimelineAnimationDisabled(JSONStorable storable)
        {
            _otherTimelines.Remove(storable);
        }

        public void OnTimelineEvent(Dictionary<string, object> e)
        {
            if (_syncing)
                throw new InvalidOperationException("Already syncing, infinite loop avoided!");

            _syncing = true;
            try
            {
                switch ((string)e["name"])
                {
                    case TimelineEventNames.PlaybackState:
                        var clip = animation.GetClip((string)e[nameof(AtomAnimationClip.animationName)]);
                        if (clip == null) return;
                        clip.clipTime = (float)e[nameof(clip.clipTime)];
                        if ((bool)e[nameof(clip.playbackEnabled)])
                            animation.PlayClip(clip, (bool)e[nameof(animation.sequencing)]);
                        else
                            animation.StopClip(clip);
                        break;
                    default:
                        SuperController.LogError($"Received message name {e["name"]} but no handler exists for that event");
                        break;
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(TimelineEventManager)}.{nameof(OnTimelineEvent)}: {exc}");
            }
            finally
            {
                _syncing = false;
            }
        }

        private void BroadcastToTimelines(string methodName)
        {
            foreach (var atom in SuperController.singleton.GetAtoms())
            {
                if (atom == _containingAtom) continue;
                var pluginId = atom.GetStorableIDs().FirstOrDefault(id => id.EndsWith("VamTimeline.AtomPlugin"));
                if (pluginId != null)
                {
                    var plugin = atom.GetStorableByID(pluginId);
                    if (plugin == _plugin) continue;
                    plugin.SendMessage(methodName, _plugin, SendMessageOptions.RequireReceiver);
                }
            }
        }

        private void ScanForAtoms()
        {
            foreach (var atom in SuperController.singleton.GetAtoms())
            {
                if (atom == null) continue;
                var storableId = atom.GetStorableIDs().FirstOrDefault(id => id.EndsWith("VamTimeline.AtomPlugin"));
                if (storableId == null) continue;
                var storable = atom.GetStorableByID(storableId);
                if (storable == _plugin) continue;
                if (!storable.enabled) continue;
                OnTimelineAnimationReady(storable);
            }
        }

        #endregion

        #region Messages

        public void SendTimelineEvent(Dictionary<string, object> e)
        {
            foreach (var storable in _otherTimelines)
            {
                if (storable == null)
                {
                    continue;
                    // SuperController.LogError($"Storables: {_otherTimelines.Count}, {storable?.containingAtom?.name}");
                    // throw new Exception("Target storable is disabled");
                }
                storable.SendMessage(nameof(OnTimelineEvent), e);
            }
        }

        public void SendPlaybackState(AtomAnimationClip clip)
        {
            SendTimelineEvent(new Dictionary<string, object>{
                {"name", TimelineEventNames.PlaybackState},
                {nameof(clip.animationName), clip.animationName},
                {nameof(clip.playbackEnabled), clip.playbackEnabled},
                {nameof(clip.clipTime), clip.clipTime},
                {nameof(animation.sequencing), animation.sequencing},
            });
        }

        #endregion
    }
}
