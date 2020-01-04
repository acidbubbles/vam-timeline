using System;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class FloatParamsPlugin : MVRScript, IAnimationPlugin
    {
        private readonly FloatParamsPluginImpl _impl;

        public Atom ContainingAtom => containingAtom;

        public FloatParamsPlugin()
        {
            _impl = new FloatParamsPluginImpl(this);
        }

        public override void Init()
        {
            try
            {
                _impl.Init();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.FloatParamsPlugin.Init: " + exc);
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
                SuperController.LogError("VamTimeline.FloatParamsPlugin.Update: " + exc);
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
                SuperController.LogError("VamTimeline.FloatParamsPlugin.OnEnable: " + exc);
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
                SuperController.LogError("VamTimeline.FloatParamsPlugin.OnDisable: " + exc);
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
                SuperController.LogError("VamTimeline.FloatParamsPlugin.OnDestroy: " + exc);
            }
        }
    }
}
