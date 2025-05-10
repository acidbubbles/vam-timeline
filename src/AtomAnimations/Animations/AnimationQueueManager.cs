using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace VamTimeline
{
    public class AnimationQueueManager
    {
        private readonly AtomAnimation _atomAnimation;
        public Logger logger;
        private readonly AtomAnimationsClipsIndex _index;

        public List<string> Queue { get; } = new List<string>();
        public int CurrentIndex { get; private set; } = -1;
        public bool IsActive { get; private set; } = false;

        public UnityEvent onQueueFinished { get; } = new UnityEvent();

        public AnimationQueueManager(AtomAnimation atomAnimation, AtomAnimationsClipsIndex index)
        {
            _atomAnimation = atomAnimation;
            _index = index;
        }

        public void SetQueue(string queueString)
        {
            Deactivate();
            Queue.Clear();

            if (string.IsNullOrEmpty(queueString) || queueString.Trim().Length == 0)
            {
                if (logger.sequencing) logger.Log(logger.sequencingCategory, "Animation queue cleared.");
                return;
            }

            var names = queueString.Split(new[] { "::" }, System.StringSplitOptions.RemoveEmptyEntries)
                                   .Select(s => s.Trim())
                                   .Where(s => !string.IsNullOrEmpty(s))
                                   .ToList();

            if (names.Count > 0)
            {
                Queue.AddRange(names);
                if (logger.sequencing) logger.Log(logger.sequencingCategory, $"Animation queue set with {names.Count} animations: {string.Join(", ", names.ToArray())}");
            }
            else
            {
                if (logger.sequencing) logger.Log(logger.sequencingCategory, "Animation queue string parsed to empty list.");
            }
        }

        public void StartQueue(AtomAnimation animation, AtomAnimationClip currentClip)
        {
            if (Queue.Count == 0)
            {
                logger.Log(logger.sequencingCategory, "Cannot start queue: Queue is empty.");
                return;
            }
            var firstAnimationName = Queue[0];
            var firstClip = _atomAnimation.FindClipInPriorityOrder(firstAnimationName, currentClip.animationSegmentId, currentClip.animationLayer);

            if (firstClip != null)
            {
                IsActive = true;
                CurrentIndex = 0;
                if (logger.sequencing) logger.Log(logger.sequencingCategory, $"Starting animation queue with '{firstAnimationName}'.");

                if (firstClip.animationSegmentId != animation.playingAnimationSegmentId && !firstClip.isOnSharedSegment)
                {
                    animation.PlaySegment(firstClip, true, startingQueue: true);
                }
                else
                {
                    animation.PlayClip(firstClip, true, startingQueue: true);
                }
            }
            else
            {
                logger.Log(logger.sequencingCategory, $"Cannot start queue: First animation '{firstAnimationName}' not found.");
                Deactivate();
            }
        }

        public void Deactivate()
        {
            if (IsActive && logger.sequencing) logger.Log(logger.sequencingCategory, "Deactivating animation queue.");
            IsActive = false;
            CurrentIndex = -1;
        }

        public AtomAnimationClip GetNextClipInQueue(AtomAnimationClip currentClip)
        {
            if (!IsActive || CurrentIndex < 0 || CurrentIndex + 1 >= Queue.Count)
            {
                if (IsActive)
                {
                    Deactivate();
                    onQueueFinished.Invoke();
                }
                return null;
            }

            var nextAnimationName = Queue[CurrentIndex + 1];
            var nextClip = _atomAnimation.FindClipInPriorityOrder(nextAnimationName, currentClip.animationSegmentId, currentClip.animationLayer);

            if (nextClip == null)
            {
                logger.Log(logger.sequencingCategory, $"Could not find animation '{nextAnimationName}' from queue at index {CurrentIndex + 1}. Deactivating queue.");
                Deactivate();
                onQueueFinished.Invoke();
                return null;
            }
            CurrentIndex++;
            if (logger.sequencing) logger.Log(logger.sequencingCategory, $"Queue providing next clip: '{nextClip.animationNameQualified}' (from index {CurrentIndex})");
            return nextClip;
        }

        public float CalculateNextTimeForQueue(AtomAnimationClip source, AtomAnimationClip next, float currentClipTime)
        {
            float nextTime;
            if (source.loop)
            {
                float timeToEndOfCurrentLoop = source.animationLength - currentClipTime;

                if (timeToEndOfCurrentLoop >= next.blendInDuration)
                {
                    nextTime = timeToEndOfCurrentLoop - next.blendInDuration;
                }
                else
                {
                    nextTime = timeToEndOfCurrentLoop + source.animationLength - next.blendInDuration;
                }
            }
            else
            {
                float remainingTime = source.animationLength - currentClipTime;
                nextTime = remainingTime - next.blendInDuration;
            }
            return Mathf.Max(0f, nextTime);
        }
    }
}