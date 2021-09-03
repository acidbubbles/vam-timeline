using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
// ReSharper disable UnusedParameter.Local

namespace VamTimeline
{
    public class PeerManager
    {
        public AtomAnimationEditContext animationEditContext;
        private AtomAnimation animation => animationEditContext.animation;
        private bool syncing => _sending > 0 || _receiving;

        private readonly List<JSONStorable> _peers = new List<JSONStorable>();
        private readonly Atom _containingAtom;
        private readonly IAtomPlugin _plugin;
        private readonly Logger _logger;
        private bool _receiving;
        private int _sending;
        private bool _reportedLengthErrorOnce;

        public PeerManager(Atom containingAtom, IAtomPlugin plugin, Logger logger)
        {
            _containingAtom = containingAtom;
            _plugin = plugin;
            _logger = logger;
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

        public void OnTimelineAnimationReady(MVRScript storable)
        {
            if (ReferenceEquals(storable, _plugin) || _peers.Contains(storable)) return;
            _peers.Add(storable);
        }

        public void OnTimelineAnimationDisabled(MVRScript storable)
        {
            _peers.Remove(storable);
        }

        private bool IsExcludedFromLogging(string eventName)
        {
            return eventName == nameof(SendScreen) || eventName == nameof(SendLoggingSettings);
        }

        public void OnTimelineEvent(object[] e)
        {
            if (!animation.syncWithPeers) return;

            if (_receiving)
                throw new InvalidOperationException("Already syncing, infinite loop avoided!");

            var eventName = (string)e[0];

            if (_logger.peersSync && !IsExcludedFromLogging(eventName))
                _logger.Log(_logger.peersSyncCategory, $"Receiving '{e[0]}'");

            _receiving = true;
            try
            {
                switch (eventName)
                {
                    case nameof(SendPlaybackState):
                        ReceivePlaybackState(e);
                        break;
                    case nameof(SendMasterClipState):
                        ReceiveMasterClipState(e);
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
                    case nameof(SendSyncAnimation):
                        ReceiveSyncAnimation(e);
                        break;
                    case nameof(SendStopAndReset):
                        ReceiveStopAndReset(e);
                        break;
                    case nameof(SendStop):
                        ReceiveStop(e);
                        break;
                    case nameof(SendPaused):
                        ReceivePaused(e);
                        break;
                    case nameof(SendStartRecording):
                        ReceiveStartRecording(e);
                        break;
                    case nameof(SendStopRecording):
                        ReceiveStopRecording(e);
                        break;
                    case nameof(SendLoggingSettings):
                        ReceiveLoggingSettings(e);
                        break;
                    default:
                        SuperController.LogError($"Received message name {e[0]} but no handler exists for that event");
                        break;
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline.{nameof(PeerManager)}.{nameof(OnTimelineEvent)}: {exc}");
            }
            finally
            {
                _receiving = false;
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
                var storable = atom.GetStorableByID(storableId) as MVRScript;
                if (storable == null) continue;
                if (ReferenceEquals(storable, _plugin)) continue;
                if (!storable.enabled) continue;
                OnTimelineAnimationReady(storable);
            }
        }

        #endregion

        #region Messages

        public void SendPlaybackState(AtomAnimationClip clip)
        {
            if (syncing) return;
            SendTimelineEvent(new object[]{
                 nameof(SendPlaybackState), // 0
                 clip.animationName, // 1
                 clip.playbackEnabled, // 2
                 clip.clipTime, // 3
                 animation.sequencing // 4
            });
        }

        private void ReceivePlaybackState(object[] e)
        {
            if (!ValidateArgumentCount(e.Length, 5)) return;
            var clips = GetClips(e);
            var isPlaying = (bool)e[2];
            var clipTime = (float)e[3];
            var sequencing = (bool)e[4];
            for (var i = 0; i < clips.Count; i++)
            {
                var clip = clips[i];
                if (clip == null) continue;
                if (isPlaying)
                    animation.PlayClip(clip, sequencing);
                else
                    animation.StopClip(clip);
                clip.clipTime = clipTime;
            }
        }

        public void SendMasterClipState(AtomAnimationClip clip)
        {
            if (syncing) return;
            SendTimelineEvent(new object[]{
                 nameof(SendMasterClipState), // 0
                 clip.animationName, // 1
                 clip.clipTime //2
            });
        }

        private void ReceiveMasterClipState(object[] e)
        {
            if (!ValidateArgumentCount(e.Length, 3)) return;
            if (animation.master)
            {
                SuperController.LogError($"Atom {_containingAtom.name} received a master clip state from another atom. Please make sure only one of your atoms is a sequence master during playback.");
                return;
            }
            var clips = GetClips(e);
            for(var i = 0; i < clips.Count; i++)
            {
                var clip = clips[i];
                if (clip == null || clip.playbackEnabled) continue;
                animation.PlayClip(clip, true, false);
                clip.clipTime = (float) e[2];
            }
        }

        public void SendStop()
        {
            if (syncing) return;
            SendTimelineEvent(new object[]{
                 nameof(SendStop), // 0
            });
        }

        private void ReceiveStop(object[] e)
        {
            if (!ValidateArgumentCount(e.Length, 1)) return;
            animationEditContext.Stop();
        }

        public void SendPaused()
        {
            if (syncing) return;
            SendTimelineEvent(new object[]{
                 nameof(SendPaused), // 0
                 animation.paused, // 1
            });
        }

        private void ReceivePaused(object[] e)
        {
            if (!ValidateArgumentCount(e.Length, 2)) return;
            animation.paused = (bool) e[1];
        }

        public void SendStopAndReset()
        {
            if (syncing) return;
            SendTimelineEvent(new object[]{
                 nameof(SendStopAndReset) // 0
            });
        }

        private void ReceiveStopAndReset(object[] _)
        {
            animationEditContext.StopAndReset();
        }

        public void SendTime(AtomAnimationClip clip)
        {
            if (syncing) return;
            SendTimelineEvent(new object[]{
                 nameof(SendTime), // 0
                 clip.animationName, // 1
                 clip.clipTime // 2
            });
        }

        private void ReceiveTime(object[] e)
        {
            if (!ValidateArgumentCount(e.Length, 3)) return;
            var clips = GetClips(e);
            for(var i = 0; i < clips.Count; i++)
            {
                var clip = clips[i];
                if (clip != animationEditContext.current) continue;
                animationEditContext.clipTime = (float)e[2];
            }
        }

        public void SendCurrentAnimation(AtomAnimationClip clip)
        {
            if (syncing) return;
            SendTimelineEvent(new object[]{
                 nameof(SendCurrentAnimation), // 0
                 clip.animationName // 1
            });
        }

        private void ReceiveCurrentAnimation(object[] e)
        {
            if (!ValidateArgumentCount(e.Length, 2)) return;
            var clips = GetClips(e);
            for(var i = 0; i < clips.Count; i++)
            {
                var clip = clips[i];
                if (clip == null) continue;
                animationEditContext.SelectAnimation(clip);
            }
        }

        public void SendSyncAnimation(AtomAnimationClip clip)
        {
            if (syncing) return;
            string previousAnimationName = null;
            var idx = animation.clips.IndexOf(clip);
            if (idx > 0)
            {
                var previousClip = animation.clips[idx - 1];
                if (previousClip.animationLayer == clip.animationLayer)
                    previousAnimationName = previousClip.animationName;
            }
            else
            {
                previousAnimationName = "";
            }
            SendTimelineEvent(new object[]{
                 nameof(SendSyncAnimation), // 0
                 clip.animationName, // 1
                 clip.animationLayer, // 2
                 clip.animationLength, // 3
                 clip.nextAnimationName, // 4
                 clip.nextAnimationTime, // 5
                 clip.blendInDuration, // 6
                 clip.autoPlay, // 7
                 clip.loop, // 8
                 clip.autoTransitionPrevious, // 9
                 clip.autoTransitionNext, // 10
                 clip.speed, // 11
                 clip.weight, // 12
                 clip.uninterruptible, // 13
                 clip.preserveLoops, // 14
                 previousAnimationName // 15
            });
        }

        private void ReceiveSyncAnimation(object[] e)
        {
            if (!ValidateArgumentCount(e.Length, 16)) return;
            var animationName = (string)e[1];
            var animationLayer = (string)e[2];

            var existing = animation.GetClip(animationLayer, animationName);
            if (existing == null)
            {
                existing = animation.clips.FirstOrDefault(c => c.animationName == animationName);
                if (existing == null)
                {
                    if (animation.clips.Any(c => c.animationLayer == animationLayer))
                    {
                        new AddAnimationOperations(animation, animation.clips.First(c => c.animationLayer == animationLayer))
                            .AddAnimationFromCurrentFrame(false, animationName, GetPosition(animationLayer, animationName, e.Length >= 16 && e[15] != null ? (string)e[15] : null));
                    }
                    else
                    {
                        animation.CreateClip(animationLayer, animationName, GetPosition(animationLayer, animationName, e.Length >= 16 && e[15] != null ? (string)e[15] : null));
                    }
                }
            }

            var clips = animation.GetClips(animationName);
            for(var i = 0; i < clips.Count; i++)
            {
                var clip = clips[i];
                new ResizeAnimationOperations().CropOrExtendEnd(clip, (float)e[3]);
                var nextAnimationName = (string)e[4];
                if (!string.IsNullOrEmpty(nextAnimationName) && animation.index.ByLayer(clip.animationLayer).Any(c => c.animationName == nextAnimationName))
                {
                    clip.nextAnimationName = nextAnimationName;
                    clip.nextAnimationTime = 0f; // Will be managed by the master setting
                    clip.autoTransitionNext = (bool)e[10];
                }
                if (animation.index.ByLayer(clip.animationLayer).Any(c => c.nextAnimationName == clip.animationName))
                {
                    clip.autoTransitionPrevious = (bool)e[9];
                }
                clip.blendInDuration = (float)e[6];
                clip.loop = (bool)e[8];
                clip.speed = (float)e[11];
                clip.weight = (float)e[12];
                clip.uninterruptible = (bool)e[13];
                clip.preserveLoops = (bool)e[14];
                animationEditContext.SelectAnimation(clip);
            }
        }

        private int GetPosition(string animationLayer, string animationName, string previousAnimationName)
        {
            var previousClipPosition = animation.clips.FindIndex(c => c.animationLayer == animationLayer && c.animationName == previousAnimationName);
            if (previousClipPosition > -1)
                return previousClipPosition + 1;

            return animation.clips.FindIndex(c => c.animationLayer == animationLayer);
        }

        public void SendScreen(string screenName, object screenArg)
        {
            if (syncing) return;
            SendTimelineEvent(new[]{
                 nameof(SendScreen), // 0
                 screenName, // 1
                 screenArg // 2
            });
        }

        private void ReceiveScreen(object[] e)
        {
            if (!ValidateArgumentCount(e.Length, 3)) return;
            _plugin.ChangeScreen((string)e[1], e[2]);
        }

        public void SendStartRecording(int timeMode)
        {
            if (syncing) return;
            SendTimelineEvent(new object[]{
                 nameof(SendStartRecording), // 0
                 timeMode // 1
            });
        }

        private void ReceiveStartRecording(object[] e)
        {
            if (!ValidateArgumentCount(e.Length, 2)) return;
            animation.SetTemporaryTimeMode((int)e[1]);
        }

        public void SendStopRecording()
        {
            if (syncing) return;
            SendTimelineEvent(new object[]{
                 nameof(SendStopRecording) // 0
            });
        }

        private void ReceiveStopRecording(object[] e)
        {
            if (!ValidateArgumentCount(e.Length, 1)) return;
            animation.RestoreTemporaryTimeMode();
        }

        public void SendLoggingSettings()
        {
            if (syncing) return;
            SendTimelineEvent(new object[]{
                 nameof(SendLoggingSettings), // 0
                 _logger.filter, // 1
                 _logger.clearOnPlay, // 2
                 _logger.general, // 3
                 _logger.triggers, // 4
                 _logger.sequencing, // 5
                 _logger.peersSync, // 6
            });
        }

        private void ReceiveLoggingSettings(object[] e)
        {
            if (!ValidateArgumentCount(e.Length, 7)) return;
            _logger.filter = (Regex)e[1];
            _logger.clearOnPlay = (bool)e[2];
            _logger.general = (bool)e[3];
            _logger.triggers = (bool)e[4];
            _logger.sequencing = (bool)e[5];
            _logger.peersSync = (bool)e[6];
        }

        private void SendTimelineEvent(object[] e)
        {
            if (!animation.syncWithPeers) return;
            if (_logger.peersSync && !IsExcludedFromLogging((string)e[0]))
                _logger.Log(_logger.peersSyncCategory, $"Broadcasting '{e[0]}'");
            Begin();
            try
            {
                for (var i = 0; i < _peers.Count; i++)
                {
                    var storable = _peers[i];
                    if (storable == null) continue;
#if (VAM_GT_1_20)
                    if (animation.syncSubsceneOnly && storable.containingAtom.containingSubScene != _containingAtom.containingSubScene) continue;
#endif
                    storable.SendMessage(nameof(IRemoteAtomPlugin.OnTimelineEvent), e);
                }
            }
            finally
            {
                Complete();
            }
        }

        private IList<AtomAnimationClip> GetClips(object[] e)
        {
            return animation.GetClips((string)e[1]);
        }

        private void Begin()
        {
            _sending++;
        }

        private void Complete()
        {
            _sending--;
        }

        private bool ValidateArgumentCount(int actualLength, int expectedLength)
        {
            if (actualLength >= expectedLength) return true;
            if (_reportedLengthErrorOnce) return false;
            _reportedLengthErrorOnce = true;
            SuperController.LogError($"Atom {_plugin.containingAtom.name} received a peer message with the wrong number of arguments. This usually means you have more than one version of Timeline in your scene. Make sure all Timeline instances are running the same version. Actual: {actualLength}, Expected: {expectedLength}.");
            return false;

        }

        #endregion
    }
}
