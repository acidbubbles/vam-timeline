using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EventArgs = System.Collections.Generic.Dictionary<string, object>;

namespace VamTimeline
{
    public class TimelineEventManager
    {
        public AtomAnimation animation;

        private readonly List<JSONStorable> _otherPlugins = new List<JSONStorable>();
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
            if (storable == _plugin || _otherPlugins.Contains(storable)) return;
            _otherPlugins.Add(storable);
        }

        public void OnTimelineAnimationDisabled(JSONStorable storable)
        {
            _otherPlugins.Remove(storable);
        }

        public void OnTimelineEvent(EventArgs e)
        {
            if (_syncing)
                throw new InvalidOperationException("Already syncing, infinite loop avoided!");

            _syncing = true;
            try
            {
                switch (e.Get<string>("name"))
                {
                    case nameof(SendPlaybackState):
                        ReceivePlaybackState(e);
                        break;
                    case nameof(SendTime):
                        ReceiveTime(e);
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

        public void SendTimelineEvent(EventArgs e)
        {
            foreach (var storable in _otherPlugins)
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
            if (_syncing) return;
            SendTimelineEvent(new EventArgs{
                {"name", nameof(SendPlaybackState)},
                {nameof(clip.animationName), clip.animationName},
                {nameof(clip.playbackEnabled), clip.playbackEnabled},
                {nameof(clip.clipTime), clip.clipTime},
                {nameof(animation.sequencing), animation.sequencing},
            });
        }

        private void ReceivePlaybackState(EventArgs e)
        {
            var clip = GetClip(e);
            if (clip == null) return;
            clip.clipTime = e.Get<float>(nameof(clip.clipTime));
            if (e.Get<bool>(nameof(clip.playbackEnabled)))
                animation.PlayClip(clip, e.Get<bool>(nameof(animation.sequencing)));
            else
                animation.StopClip(clip);
        }

        public void SendTime(AtomAnimationClip clip)
        {
            if (_syncing) return;
            SendTimelineEvent(new EventArgs{
                {"name", nameof(SendTime)},
                {nameof(clip.animationName), clip.animationName},
                {nameof(clip.clipTime), clip.clipTime},
            });
        }

        private void ReceiveTime(EventArgs e)
        {
            var clip = GetClip(e);
            if (clip != animation.current) return;
            animation.clipTime = e.Get<float>(nameof(clip.clipTime));
        }

        private AtomAnimationClip GetClip(EventArgs e)
        {
            return animation.GetClip(e.Get<string>(nameof(AtomAnimationClip.animationName)));
        }

        #endregion
    }
}
