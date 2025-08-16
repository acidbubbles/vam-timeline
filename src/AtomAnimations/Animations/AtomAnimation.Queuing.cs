using System.Collections.Generic;
using System.Text;

namespace VamTimeline
{
    public partial class AtomAnimation
    {
        #region Queueing

        private string _queueName;
        private readonly List<AtomAnimationClip> _queue = new List<AtomAnimationClip>();
        private AtomAnimationClip _queueCurrent;
        private AtomAnimationClip _queueNext;
        private int _queueNextTimes = 1;
        private bool _processingQueue;

        public string GetStringifiedQueue()
        {
            if (_queue.Count == 0)
            {
                if (_queueName == null)
                    return "Queue is empty";
                else
                    return $"Queue pending {_queueName}";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Queue with {_queue.Count} items ({_queueName ?? "unnamed"})");

            var qI = 1;
            if (_queueCurrent != null)
            {
                sb.AppendLine($"{qI++}: {_queueCurrent.animationName} (Current)");
            }
            if (_queueNext != null)
            {
                sb.AppendLine($"{qI++}: {_queueNext.animationName} (Next)");
            }
            foreach(var clip in _queue)
            {
                sb.AppendLine($"{qI++}: {clip.animationName}");
            }
            return sb.ToString();
        }

        public void CreateQueue(string name)
        {
            ClearQueue();
            _queueName = name;
            onQueueUpdated.Invoke();
        }

        public void AddToQueue(AtomAnimationClip clip)
        {
            if (_queueName == null)
                _queueName = "unnamed";

            _queue.Add(clip);
            onQueueUpdated.Invoke();
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
            _queueNext = null;
            _queueNextTimes = 1;
            _queue.Clear();

            onQueueUpdated.Invoke();

            if (wasProcessingQueue)
                onQueueFinished.Invoke(queueName);
        }

        #endregion
    }
}
