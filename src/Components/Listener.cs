using System;
using UnityEngine;

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
        private Action _handler;
        private Action<EventHandler> _attach;
        private Action<EventHandler> _detach;

        public void OnEnable()
        {
            if (!_attached && _attach != null)
            {
                _attach.Invoke(Handle);
                _attached = true;
            }
        }

        public void OnDisable()
        {
            if (_attached && _detach != null)
            {
                _detach.Invoke(Handle);
                _attached = false;
            }
        }

        public void Handle(object o, EventArgs args)
        {
            _handler();
        }

        public void Bind(Action handler, Action<EventHandler> attach, Action<EventHandler> detach)
        {
            _handler = handler;
            _attach = attach;
            _detach = detach;
            OnEnable();
        }
    }
}
