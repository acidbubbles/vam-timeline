using System;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class MorphsPlugin : MVRScript, IAnimationPlugin
    {
        private readonly MorphsPluginImpl _impl;

        public Atom ContainingAtom => containingAtom;

        public MorphsPlugin()
        {
            _impl = new MorphsPluginImpl(this);
        }

        public override void Init()
        {
            try
            {
                _impl.Init();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.MorphsPlugin.Init: " + exc);
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
                SuperController.LogError("VamTimeline.MorphsPlugin.Update: " + exc);
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
                SuperController.LogError("VamTimeline.MorphsPlugin.OnEnable: " + exc);
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
                SuperController.LogError("VamTimeline.MorphsPlugin.OnDisable: " + exc);
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
                SuperController.LogError("VamTimeline.MorphsPlugin.OnDestroy: " + exc);
            }
        }
    }
}
