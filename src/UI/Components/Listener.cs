using UnityEngine;
using UnityEngine.Events;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class Listener : MonoBehaviour
    {
        private bool _attached;
        private UnityAction _fn;
        private UnityEvent _handler;

        public void OnEnable()
        {
            if (!_attached && _handler != null)
            {
                _handler.AddListener(_fn);
                _attached = true;
            }
        }

        public void OnDisable()
        {
            if (_attached && _handler != null)
            {
                _handler.RemoveListener(_fn);
                _attached = false;
            }
        }

        public void Bind(UnityEvent handler, UnityAction fn)
        {
            _fn = fn;
            _handler = handler;
            OnEnable();
        }
    }
}
