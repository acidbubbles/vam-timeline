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
        private bool _reportedMissingEventOnce;
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
                    case nameof(SendAddAnimation):
                        ReceiveAddAnimation(e);
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
                    case nameof(SendPlaySegment):
                        ReceivePlaySegment(e);
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
                        if (!_reportedMissingEventOnce)
                        {
                            SuperController.LogError($"Received message name {e[0]} but no handler exists for that event");
                            _reportedMissingEventOnce = true;
                        }
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

        private readonly object[] _sendPlaybackStateMessage = new object[7] { nameof(SendPlaybackState), null, null, null, null, null, null };
        public void SendPlaybackState(AtomAnimationClip clip)
        {
            if (syncing) return;
            _sendPlaybackStateMessage[1] = clip.animationName;
            _sendPlaybackStateMessage[2] = clip.playbackEnabled;
            _sendPlaybackStateMessage[3] = clip.clipTime - clip.timeOffset;
            _sendPlaybackStateMessage[4] = animation.sequencing;
            _sendPlaybackStateMessage[5] = clip.animationSet;
            _sendPlaybackStateMessage[6] = clip.animationSegment;
            SendTimelineEvent(_sendPlaybackStateMessage);
        }

        private void ReceivePlaybackState(object[] e)
        {
            if (!ValidateArgumentCount(e.Length, 6)) return;
            var animationName = (string)e[1];
            var animationSet = (string)e[5];
            var animationSegment = e.Length >= 7 ? (string)e[6] : AtomAnimationClip.NoneAnimationSegment;
            var isPlaying = (bool)e[2];
            var clipTime = (float)e[3];
            var sequencing = (bool)e[4];

            if (isPlaying) animation.PlayClipBySet(animationName, animationSet, animationSegment, sequencing);
            var clips = animation.index.ByName(animationSegment, animationName);
            if (clips.Count == 0) return;
            for (var i = 0; i < clips.Count; i++)
            {
                var clip = clips[i];
                clip.clipTime = clipTime + clip.timeOffset;
            }
        }

        public void SendPlaySegment(AtomAnimationClip clip)
        {
            if (syncing) return;
            if (clip.animationSegment == AtomAnimationClip.NoneAnimationSegment) return;
            SendTimelineEvent(new object[]{
                 nameof(SendPlaySegment), // 0
                 clip.animationSegment, // 1
                 clip.animationName, // 2
                 animation.sequencing // 3
            });
        }

        private void ReceivePlaySegment(object[] e)
        {
            if (!ValidateArgumentCount(e.Length, 4)) return;
            var animationSegment = (string)e[1];
            var animationName = (string)e[2];
            var sequencing = (bool)e[3];
            if (!animation.isPlaying || !animation.IsPlayingAnimationSegment(animationSegment))
            {
                var byName = animation.index.ByName(animationSegment, animationName);
                if(byName.Count > 0)
                    animation.PlaySegment(byName[0], sequencing);
                else
                    animation.PlaySegment(animationSegment, sequencing);
            }
        }

        public void SendMasterClipState(AtomAnimationClip clip)
        {
            if (syncing) return;
            SendTimelineEvent(new object[]{
                 nameof(SendMasterClipState), // 0
                 clip.animationName, // 1
                 clip.clipTime - clip.timeOffset, //2
                 clip.animationSegment, // 3
            });
        }

        private void ReceiveMasterClipState(object[] e)
        {
            if (!ValidateArgumentCount(e.Length, 4)) return;
            if (animation.master)
            {
                SuperController.LogError($"Atom {_containingAtom.name} received a master clip state from another atom. Please make sure only one of your atoms is a sequence master during playback.");
                return;
            }
            var clips = animation.index.ByName((string)e[3], (string)e[1]);
            for(var i = 0; i < clips.Count; i++)
            {
                var clip = clips[i];
                if (!clip.playbackMainInLayer)
                    animation.PlayClip(clip, true, false);
                clip.clipTime = (float) e[2] + clip.timeOffset;
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

        private readonly object[] _sendTimeMessage = new object[4] { nameof(SendTime), null, null, null };
        public void SendTime(AtomAnimationClip clip)
        {
            if (syncing) return;
            _sendTimeMessage[1] = clip.animationName;
            _sendTimeMessage[2] = clip.clipTime - clip.timeOffset;
            _sendTimeMessage[3] = clip.animationSegment;
            SendTimelineEvent(_sendTimeMessage);
        }

        private void ReceiveTime(object[] e)
        {
            if (!ValidateArgumentCount(e.Length, 4)) return;
            var clips = animation.index.ByName((string)e[3], (string)e[1]);
            if(clips.Contains(animationEditContext.current))
                animationEditContext.clipTime = (float)e[2] + animationEditContext.current.timeOffset;
        }

        public void SendCurrentAnimation(AtomAnimationClip clip)
        {
            if (syncing) return;
            SendTimelineEvent(new object[]{
                 nameof(SendCurrentAnimation), // 0
                 clip.animationName, // 1
                 clip.animationSegment, // 2
                 clip.clipTime - clip.timeOffset, // 3
                 clip.animationLayer, // 4
            });
        }

        private void ReceiveCurrentAnimation(object[] e)
        {
            if (!ValidateArgumentCount(e.Length, 5)) return;
            var clips = animation.index.ByName((string)e[2], (string)e[1]);
            var clip = clips.FirstOrDefault(c => c.animationLayer == (string)e[4]) ?? clips.FirstOrDefault();
            if (clip == null) return;
            animationEditContext.SelectAnimation(clip);
            if (!animation.isPlaying)
                animationEditContext.clipTime = (float)e[3] + animationEditContext.current.timeOffset;
        }

        public void SendSyncAnimation(AtomAnimationClip clip)
        {
            if (syncing) return;
            string previousAnimationName = null;
            var idx = animation.clips.IndexOf(clip);
            if (idx > 0)
            {
                var previousClip = animation.clips[idx - 1];
                if (previousClip.animationLayerQualifiedId == clip.animationLayerQualifiedId)
                    previousAnimationName = previousClip.animationName;
            }
            else
            {
                previousAnimationName = "";
            }
            SuperController.LogMessage("Previous was: " + previousAnimationName);
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
                 previousAnimationName, // 15
                 clip.animationSegment, // 16
                 clip.preserveLength, // 17
            });
        }

        private void ReceiveSyncAnimation(object[] e)
        {
            if (!ValidateArgumentCount(e.Length, 18)) return;
            var animationSegment = (string)e[16];
            var animationLayer = (string)e[2];
            var animationName = (string)e[1];

            if ((animationSegment != AtomAnimationClip.NoneAnimationSegment) != animation.index.useSegment)
            {
                return;
            }

            if (animation.index.ByName(animationSegment, animationName).Count == 0)
            {
                var previousAnimationName = e.Length >= 16 ? (string)e[15] : null;
                var clipOnLayer = animation.clips.FirstOrDefault(c => c.animationSegment == animationSegment && c.animationLayer == animationLayer);
                if (clipOnLayer != null)
                {
                    new AddAnimationOperations(animation, clipOnLayer)
                        .AddAnimation(animationName, AddAnimationOperations.Positions.PositionLast, false, false, false);
                }
                else
                {
                    var position = GetPosition(animationName, animationLayer, animationSegment, previousAnimationName);
                    animation.CreateClip(animationName, animationLayer, animationSegment, position);
                }
            }

            var clips = animation.index.ByName(animationSegment, animationName);
            for(var i = 0; i < clips.Count; i++)
            {
                var clip = clips[i];
                new ResizeAnimationOperations().CropOrExtendEnd(clip, (float)e[3]);
                var nextAnimationName = (string)e[4];
                if (!string.IsNullOrEmpty(nextAnimationName) && animation.index.ByLayerQualified(clip.animationLayerQualifiedId).Any(c => c.animationName == nextAnimationName))
                {
                    clip.nextAnimationName = nextAnimationName;
                    clip.nextAnimationTime = 0f; // Will be managed by the master setting
                    clip.autoTransitionNext = (bool)e[10];
                }
                if (animation.index.ByLayerQualified(clip.animationLayerQualifiedId).Any(c => c.nextAnimationName == clip.animationName))
                {
                    clip.autoTransitionPrevious = (bool)e[9];
                }
                clip.blendInDuration = (float)e[6];
                clip.loop = (bool)e[8];
                clip.speed = (float)e[11];
                clip.weight = (float)e[12];
                clip.uninterruptible = (bool)e[13];
                clip.preserveLoops = (bool)e[14];
                clip.preserveLength = (bool)e[17];
            }

            animation.index.Rebuild();
        }

        public void SendAddAnimation(AtomAnimationClip clip, string createPosition, bool copySettings, bool copyKeyframes, bool createOnAllLayers)
        {
            if (syncing) return;
            SendTimelineEvent(new object[]{
                 nameof(SendAddAnimation), // 0
                 clip.animationName, // 1
                 clip.animationLayer, // 2
                 clip.animationSegment, // 3
                 createPosition, //4
                 copySettings, // 5
                 copyKeyframes, // 6
                 createOnAllLayers, // 7
            });
        }

        private void ReceiveAddAnimation(object[] e)
        {
            if (!ValidateArgumentCount(e.Length, 8)) return;
            var animationSegment = (string)e[3];
            var animationLayer = (string)e[2];
            var animationName = (string)e[1];
            var createPosition = (string)e[4];
            var copySettings = (bool)e[5];
            var copyKeyframes = (bool)e[6];
            var createOnAllLayers = (bool)e[7];

            if (animationEditContext.current.animationSegment != animationSegment) return;
            if (animationEditContext.current.animationName != animationName && animationEditContext.current.animationLayer != animationLayer) return;

            var clip = animationEditContext.current;

            new AddAnimationOperations(animation, clip)
                .AddAnimation(animationName, createPosition, copySettings, copyKeyframes, createOnAllLayers);
        }

        private int GetPosition(string animationName, string animationLayer, string animationSegment, string previousAnimationName)
        {
            var previousClipPosition = animation.clips.FindIndex(c => c.animationSegment == animationSegment && c.animationLayer == animationLayer && c.animationName == previousAnimationName);
            SuperController.LogMessage($"Previous clip was found at {previousClipPosition}");
            if (previousClipPosition > -1)
                return previousClipPosition + 1;

            return animation.clips.FindIndex(c => c.animationSegment == animationSegment && c.animationLayer == animationLayer);
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
                 _logger.triggersReceived, // 4
                 _logger.sequencing, // 5
                 _logger.peersSync, // 6
                 _logger.triggersInvoked, // 7<
            });
        }

        private void ReceiveLoggingSettings(object[] e)
        {
            if (!ValidateArgumentCount(e.Length, 8)) return;
            _logger.filter = (Regex)e[1];
            _logger.clearOnPlay = (bool)e[2];
            _logger.general = (bool)e[3];
            _logger.triggersReceived = (bool)e[4];
            _logger.sequencing = (bool)e[5];
            _logger.peersSync = (bool)e[6];
            _logger.triggersInvoked = (bool)e[7];
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
