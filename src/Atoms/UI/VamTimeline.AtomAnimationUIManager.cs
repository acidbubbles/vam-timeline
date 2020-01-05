using System;
using System.Collections;
using UnityEngine;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class AtomAnimationUIManager
    {
        protected IAtomPlugin _plugin;
        private AtomAnimationBaseUI _current;

        public AtomAnimationUIManager(IAtomPlugin plugin)
        {
            _plugin = plugin;
        }

        public void AnimationUpdated()
        {
            if (_plugin.Animation == null) return;
            RefreshCurrentUI(() => _current.AnimationUpdated());
        }

        public void UIUpdated()
        {
            if (_plugin.Animation == null) return;
            RefreshCurrentUI(() => _current.UIUpdated());
        }

        public void RefreshCurrentUI(Action fn = null)
        {
            if (_plugin.Animation == null) return;

            _plugin.StartCoroutine(RefreshCurrentUIDefered(fn));
        }

        private IEnumerator RefreshCurrentUIDefered(Action fn)
        {
            yield return new WaitForEndOfFrame();
            Type type;
            if (_plugin.LockedJSON.val)
                type = typeof(AtomAnimationLockedUI);
            else
                type = typeof(AtomAnimationSettingsUI);

            if (_current == null || _current.GetType() != type)
            {
                // TODO: Only recreate if necessary!
                if (_current != null)
                    _current.Remove();

                _current = _plugin.LockedJSON.val ? new AtomAnimationLockedUI(_plugin) as AtomAnimationBaseUI : new AtomAnimationSettingsUI(_plugin);
                _current.Init();
            }

            fn?.Invoke();
        }

        public void UpdatePlaying()
        {
            if (_current == null) return;
            _current.UpdatePlaying();
        }
    }
}

