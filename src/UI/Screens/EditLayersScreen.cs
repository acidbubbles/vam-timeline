using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace VamTimeline
{
    public class EditLayersScreen : ScreenBase
    {
        public const string ScreenName = "Layers";

        public override string screenId => ScreenName;

        public EditLayersScreen()
            : base()
        {
        }

        public override void Init(IAtomPlugin plugin)
        {
            base.Init(plugin);

            CreateChangeScreenButton("<i><b><</b> Back to clips...</i>", AddAnimationScreen.ScreenName);

            InitLayersAndAnimations();

            prefabFactory.CreateSpacer();

            CreateChangeScreenButton("<i><b>Add</b> a new animation...</i>", AddAnimationScreen.ScreenName);
        }

        private void InitLayersAndAnimations()
        {
            foreach (var layer in animation.clips.Select(c => c.animationLayer).Distinct())
            {
                InitRenameLayer(layer);

                foreach(var clip in animation.clips.Where(c => c.animationLayer == layer))
                {
                    InitRenameClip(clip);
                }
            }
        }

        private void InitRenameLayer(string layer)
        {
            var layerNameJSON = new JSONStorableString("Layer Name", "", (string val) => UpdateLayerName(ref layer, val));
            var layerNameUI = prefabFactory.CreateTextInput(layerNameJSON);
            var layout = layerNameUI.GetComponent<LayoutElement>();
            layout.minHeight = 50f;
            layerNameUI.height = 50;
            layerNameUI.backgroundColor = Color.clear;
            layerNameUI.UItext.fontSize = 32;
            layerNameUI.UItext.fontStyle = FontStyle.Bold;
            layerNameJSON.valNoCallback = layer;
        }

        private void UpdateLayerName(ref string from, string to)
        {
            to = to.Trim();
            if (to == "")
                return;

            var layer = from;
            foreach (var clip in animation.clips.Where(c => c.animationLayer == layer))
                clip.animationLayer = to;
            from = to;
        }

        private void InitRenameClip(AtomAnimationClip clip)
        {
            var layerNameJSON = new JSONStorableString("Animation Name", "", (string val) => UpdateAnimationName(clip, val));
            var layerNameUI = prefabFactory.CreateTextInput(layerNameJSON);
            var layout = layerNameUI.GetComponent<LayoutElement>();
            layout.minHeight = 50f;
            layerNameUI.height = 50;
            layerNameUI.backgroundColor = Color.clear;
            layerNameUI.UItext.fontSize = 26;
            layerNameJSON.valNoCallback = clip.animationName;
        }

        private void UpdateAnimationName(AtomAnimationClip clip, string val)
        {
            var previousAnimationName = clip.animationName;
            if (string.IsNullOrEmpty(val))
            {
                return;
            }
            if (animation.clips.Any(c => c.animationName == val))
            {
                return;
            }
            clip.animationName = val;
            foreach (var other in animation.clips)
            {
                if (other.nextAnimationName == previousAnimationName)
                    other.nextAnimationName = val;
            }
        }
    }
}

