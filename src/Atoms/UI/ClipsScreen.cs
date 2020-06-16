using System;
using System.Collections;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class ClipsScreen : ScreenBase
    {
        public const string ScreenName = "Clips";

        private static readonly Font _font = (Font)Resources.GetBuiltinResource(typeof(Font), "Arial.ttf");

        public override string name => ScreenName;

        public ClipsScreen(IAtomPlugin plugin)
            : base(plugin)
        {

        }
        public override void Init()
        {
            base.Init();

            InitLayers(true);

            CreateSpacer(true);

            if (animation.clips.Any())
            {
                var layer = InitLayerHeader(animation.clips[0].animationLayer);

                foreach (var clip in animation.clips)
                {
                    if (clip.animationLayer != layer)
                        layer = InitLayerHeader(clip.animationLayer);

                    InitAnimButton(clip);
                }
            }

            CreateSpacer(true);

            CreateChangeScreenButton("<i><b>Add</b> a new animation...</i>", AddAnimationScreen.ScreenName, true);
        }

        private void InitAnimButton(AtomAnimationClip clip)
        {
            var clipState = animation.state.GetClip(clip.animationName);
            var btn = plugin.CreateButton($"...", true);
            RegisterComponent(btn);
            btn.buttonText.alignment = TextAnchor.MiddleLeft;
            btn.button.onClick.AddListener(() =>
            {
                if (clipState.mainInLayer)
                {
                    animation.Stop(clip.animationName);
                }
                else
                {
                    animation.Play(clip.animationName);
                }
            });
            plugin.StartCoroutine(UpdateAnimButton(btn, clipState));
        }

        private IEnumerator UpdateAnimButton(UIDynamicButton btn, AtomClipPlaybackState clipState)
        {
            yield return 0;
            var playLabel = $" \u25B6 {clipState.clip.animationName}";
            while (!_disposing)
            {
                if (!clipState.enabled)
                {
                    if (btn.label != playLabel)
                        btn.label = playLabel;
                }
                else
                {
                    btn.label = $" \u25A0 [time: {clipState.clipTime:00.000}, weight: {Mathf.Round(clipState.weight * 100f):000}%]";
                }

                for (var i = 0; i < 4; i++)
                    yield return 0;
            }
        }

        private string InitLayerHeader(string animationLayer)
        {
            var layerUI = plugin.CreateSpacer(true);
            RegisterComponent(layerUI);
            layerUI.height = 40f;

            var text = layerUI.gameObject.AddComponent<Text>();
            text.text = $"Layer: {animationLayer}";
            text.font = _font;
            text.fontSize = 28;

            return animationLayer;
        }

        private void InitLayers(bool rightSide)
        {
            // TODO: Replace by a list of all layers, what they are currently playing, and a quick link to play/stop them
            var layers = new JSONStorableStringChooser("Layer", animation.clips.Select(c => c.animationLayer).Distinct().ToList(), current.animationLayer, "Layer", ChangeLayer);
            RegisterStorable(layers);
            var layersUI = plugin.CreateScrollablePopup(layers, rightSide);
            RegisterComponent(layersUI);
        }

        private void ChangeLayer(string val)
        {
            animation.SelectAnimation(animation.clips.First(c => c.animationLayer == val).animationName);
        }
    }
}

