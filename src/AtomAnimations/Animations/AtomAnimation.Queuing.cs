namespace VamTimeline
{
    public partial class AtomAnimation
    {
        #region Queueing

        private bool _processingQueue;
        private AtomAnimationClip _lastQueuedClip;

        public void AddToQueue(AtomAnimationClip clip)
        {
            var current = isPlaying
                ? GetMainClipInLayer(index.ByLayerQualified(clip.animationLayerQualifiedId))
                : null;

            if (current == null)
            {
                if (_lastQueuedClip.animationSegmentId == clip.animationSegmentId)
                {
                    SuperController.LogError(
                        $"Timeline: Switching from a non-playing layer in a queue is not yet supported: {clip.animationNameQualified}");
                    return;
                }

                current = _lastQueuedClip;
            }

            _lastQueuedClip = clip;

            // If we're not processing the queue yet, we can immediately transition to the new clip
            if (!_processingQueue)
            {
                _processingQueue = true;
                onQueueStarted.Invoke();
                var clipTime = clip.clipTime - clip.timeOffset;
                TransitionClips(current, clip, clipTime);
            }
            // If we are processing the queue, make sure we will eventually pick up the new clip, otherwise configure the new clip immediately
            else if (current.playbackScheduledNextAnimation == null)
            {
                ScheduleNextAnimation(current, clip, forQueue: true);
            }
            // Wait for the next animation to be scheduled
            else
            {
                _queue.Add(clip);
            }
        }

        public void ClearQueue()
        {
            var wasProcessingQueue = _processingQueue;
            _processingQueue = false;
            _queue.Clear();
            _lastQueuedClip = null;

            if (wasProcessingQueue)
                onQueueFinished.Invoke();
        }

        #endregion
    }
}
