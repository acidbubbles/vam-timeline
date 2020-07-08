using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class PeerManager
    {
        public AtomAnimation animation;

        private readonly List<JSONStorable> _peers = new List<JSONStorable>();
        private readonly Atom _containingAtom;
        private readonly IAtomPlugin _plugin;
        private bool _syncing = false;

        public PeerManager(Atom containingAtom, IAtomPlugin plugin)
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
            if (ReferenceEquals(storable, _plugin) || _peers.Contains(storable)) return;
            _peers.Add(storable);
        }

        public void OnTimelineAnimationDisabled(JSONStorable storable)
        {
            _peers.Remove(storable);
        }

        public void OnTimelineEvent(object[] e)
        {
            if (_syncing)
                throw new InvalidOperationException("Already syncing, infinite loop avoided!");

            _syncing = true;
            try
            {
                switch ((string)e[0])
                {
                    case nameof(SendPlaybackState):
                        ReceivePlaybackState(e);
                        break;
                    case nameof(SendTime):
                        ReceiveTime(e);
                        break;
                    case nameof(SendCurrentAnimation):
                        ReceiveCurrentAnimation(e);
                        break;
                    case nameof(SendScreen):
                        ReceiveScreen(e);
                        break;
                    default:
                        SuperController.LogError($"Received message name {e[0]} but no handler exists for that event");
                        break;
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError($"VamTimeline.{nameof(PeerManager)}.{nameof(OnTimelineEvent)}: {exc}");
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
                    if (ReferenceEquals(plugin, _plugin)) continue;
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
                if (ReferenceEquals(storable, _plugin)) continue;
                if (!storable.enabled) continue;
                OnTimelineAnimationReady(storable);
            }
        }

        #endregion

        #region Messages

        public void SendTimelineEvent(object[] e)
        {
            foreach (var storable in _peers)
            {
                if (storable == null) continue;
                storable.SendMessage(nameof(OnTimelineEvent), e);
            }
        }

        public void SendPlaybackState(AtomAnimationClip clip)
        {
            if (_syncing) return;
            SendTimelineEvent(new object[]{
                 nameof(SendPlaybackState),
                 clip.animationName,
                 clip.playbackEnabled,
                 clip.clipTime,
                 animation.sequencing,
            });
        }

        private void ReceivePlaybackState(object[] e)
        {
            var clip = GetClip(e);
            if (clip == null) return;
            if ((bool)e[2])
                animation.PlayClip(clip, (bool)e[4]);
            else
                animation.StopClip(clip);
            clip.clipTime = (float)e[3];
        }

        public void SendTime(AtomAnimationClip clip)
        {
            if (_syncing) return;
            SendTimelineEvent(new object[]{
                 nameof(SendTime),
                 clip.animationName,
                 clip.clipTime,
            });
        }

        private void ReceiveTime(object[] e)
        {
            var clip = GetClip(e);
            if (clip != animation.current) return;
            animation.clipTime = (float)e[2];
        }

        public void SendCurrentAnimation(AtomAnimationClip clip)
        {
            if (_syncing) return;
            SendTimelineEvent(new object[]{
                 nameof(SendCurrentAnimation),
                 clip.animationName,
            });
        }

        private void ReceiveCurrentAnimation(object[] e)
        {
            var clip = GetClip(e);
            if (clip == null) return;
            animation.SelectAnimation(clip);
        }

        public void SendScreen(string screen)
        {
            if (_syncing) return;
            SendTimelineEvent(new object[]{
                 nameof(SendScreen),
                 screen,
            });
        }

        private void ReceiveScreen(object[] e)
        {
            if (_plugin.ui != null)
            {
                _plugin.ui.screensManager.ChangeScreen((string)e[1]);
                // If the selection cannot be dispatched, change the controller injected ui up front
                if (!_plugin.ui.isActiveAndEnabled && _plugin.controllerInjectedUI != null)
                {
                    _plugin.controllerInjectedUI.screensManager.ChangeScreen((string)e[1]);
                }
            }
        }

        private AtomAnimationClip GetClip(object[] e)
        {
            return animation.GetClip((string)e[1]);
        }

        #endregion
    }
}
