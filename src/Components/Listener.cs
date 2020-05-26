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

        public void OnEnable()
        {
            _attach(Handle);
        }

        public void OnDisable()
        {
            _detach(Handle);
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
        }
    }
}
