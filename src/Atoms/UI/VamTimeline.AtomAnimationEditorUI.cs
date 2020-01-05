using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.UI;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class AtomAnimationEditorUI : AtomAnimationBaseUI
    {
        public const string ScreenName = "Animation Editor";
        public override string Name => ScreenName;

        private class FloatParamJSONRef
        {
            public JSONStorable Storable;
            public JSONStorableFloat SourceFloatParam;
            public JSONStorableFloat Proxy;
            public UIDynamicSlider Slider;
        }

        private List<FloatParamJSONRef> _jsfJSONRefs;


        public AtomAnimationEditorUI(IAtomPlugin plugin)
            : base(plugin)
        {

        }
        public override void Init()
        {
            base.Init();

            // Left side

            InitAnimationSelectorUI(false);

            InitPlaybackUI(false);
            
            InitFrameNavUI(false);

                        var changeCurveUI = Plugin.CreatePopup(Plugin.ChangeCurveJSON, false);
            changeCurveUI.popupPanelHeight = 800f;
            _linkedStorables.Add(Plugin.ChangeCurveJSON);

            var smoothAllFramesUI = Plugin.CreateButton("Smooth All Frames", false);
            smoothAllFramesUI.button.onClick.AddListener(() => Plugin.SmoothAllFramesJSON.actionCallback());
            _components.Add(smoothAllFramesUI);

            // Right side

            InitDisplayUI(true);
        }

        public override void UpdatePlaying()
        {
            if (_jsfJSONRefs != null)
            {
                foreach (var jsfJSONRef in _jsfJSONRefs)
                    jsfJSONRef.Proxy.valNoCallback = jsfJSONRef.SourceFloatParam.val;
            }
        }

        public override void Remove()
        {
            RemoveJsonRefs();
            base.Remove();
        }

        private void RemoveJsonRefs()
        {
            if (_jsfJSONRefs == null) return;
            foreach (var jsfJSONRef in _jsfJSONRefs)
            {
                // TODO: Take care of keeping track of those separately
                Plugin.RemoveSlider(jsfJSONRef.Proxy);
            }
        }
    }
}

