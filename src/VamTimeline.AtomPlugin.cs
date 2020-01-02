using System;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class AtomPlugin : MVRScript, IAnimationPlugin
    {
        private readonly AtomPluginImpl _impl;

        public Atom ContainingAtom => containingAtom;

        public AtomPlugin()
        {
            _impl = new AtomPluginImpl(this);
        }

        public override void Init()
        {
            try
            {
                _impl.Init();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.Init: " + exc);
            }
        }

        public void Update()
        {
            try
            {
                _impl.Update();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.Update: " + exc);
            }
        }

        public void OnEnable()
        {
            try
            {
                _impl.OnEnable();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.OnEnable: " + exc);
            }
        }

        public void OnDisable()
        {
            try
            {
                _impl.OnDisable();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.OnDisable: " + exc);
            }
        }

        public void OnDestroy()
        {
            try
            {
                _impl.OnDestroy();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomPlugin.OnDestroy: " + exc);
            }
        }
    }
}
