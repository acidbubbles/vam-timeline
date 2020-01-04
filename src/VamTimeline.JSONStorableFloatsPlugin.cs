using System;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class JSONStorableFloatsPlugin : MVRScript, IAnimationPlugin
    {
        private readonly JSONStorableFloatsPluginImpl _impl;

        public Atom ContainingAtom => containingAtom;

        public JSONStorableFloatsPlugin()
        {
            _impl = new JSONStorableFloatsPluginImpl(this);
        }

        public override void Init()
        {
            try
            {
                _impl.Init();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.JSONStorableFloatsPlugin.Init: " + exc);
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
                SuperController.LogError("VamTimeline.JSONStorableFloatsPlugin.Update: " + exc);
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
                SuperController.LogError("VamTimeline.JSONStorableFloatsPlugin.OnEnable: " + exc);
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
                SuperController.LogError("VamTimeline.JSONStorableFloatsPlugin.OnDisable: " + exc);
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
                SuperController.LogError("VamTimeline.JSONStorableFloatsPlugin.OnDestroy: " + exc);
            }
        }
    }
}
