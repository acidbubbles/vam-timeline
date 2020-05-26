using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

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
        private Action _handler;
        private Action<EventHandler> _attach;
        private Action<EventHandler> _detach;

        public void Awake()
        {
            enabled = false;
        }

        public void OnEnable()
        {
            _attach?.Invoke(Handle);
        }

        public void OnDisable()
        {
            _detach?.Invoke(Handle);
        }

        public void Handle(object o, EventArgs args)
        {
            _handler();
        }

        public void BindAndEnable(Action handler, Action<EventHandler> attach, Action<EventHandler> detach)
        {
            _handler = handler;
            _attach = attach;
            _detach = detach;
            enabled = true;
        }
    }
}
