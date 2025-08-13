using System.Collections.Generic;

namespace VamTimeline
{
    public partial class AtomAnimation
    {
        #region Queueing

        private string _queueName;
        private readonly List<AtomAnimationClip> _queue = new List<AtomAnimationClip>();
        private AtomAnimationClip _queueCurrent;
        private int _queueNextTimes = 1;
        private bool _processingQueue;

        public void CreateQueue(string name)
        {
            ClearQueue();
            _queueName = name;
        }

        public void AddToQueue(AtomAnimationClip clip)
        {
            if (_queueName == null)
                _queueName = "unnamed";

            _queue.Add(clip);
        }

        public void PlayQueue()
        {
            if (logger.triggersReceived) logger.Log(logger.triggersCategory, $"Triggered '{StorableNames.PlayQueue}' with queue '{_queueName}' containing {_queue.Count} clips.");

            if (_queue.Count == 0)
            {
                SuperController.LogError($"Timeline: Cannot play queue '{_queueName}', no clips in queue.");
                ClearQueue();
                return;
            }

            var next = _queue[0];
            _queue.RemoveAt(0);

            if (_queue.Count == 0)
            {
                ClearQueue();
            }
            else
            {
                _processingQueue = true;
            }

            _processingQueue = true;
            PlayClip(next, true);
        }

        public void ClearQueue()
        {
            var queueName = _queueName;
            var wasProcessingQueue = _processingQueue;
            _processingQueue = false;
            _queueName = null;
            _queueCurrent = null;
            _queueNextTimes = 1;
            _queue.Clear();

            if (wasProcessingQueue)
                onQueueFinished.Invoke(queueName);
        }

        #endregion
    }
}
