using System;
using System.Collections;
using System.Linq;
using System.Text;
using UnityEngine;

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

            // Temporary (for testing)
            foreach (var clip in animation.clips)
            {
                var btn = plugin.CreateButton($"Play {clip.animationName}", true);
                RegisterComponent(btn);
                btn.button.onClick.AddListener(() =>
                {
                    animation.Play(clip.animationName);
                });
            }

            var stateJSON = new JSONStorableString("Playback state", "");
            RegisterStorable(stateJSON);
            var stateUI = plugin.CreateTextField(stateJSON, true);
            RegisterComponent(stateUI);
            plugin.StartCoroutine(UpdateState(stateJSON));

            CreateSpacer(true);

            CreateChangeScreenButton("<i><b>Add</b> a new animation...</i>", AddAnimationScreen.ScreenName, true);
        }

        private IEnumerator UpdateState(JSONStorableString stateJSON)
        {
            yield return 0;
            while (!_disposing)
            {
                var sb = new StringBuilder();
                foreach (var clipState in animation.state.clips)
                {
                    sb.Append(clipState.clip.animationName);
                    sb.Append(": ");
                    if (clipState.enabled)
                    {
                        sb.Append($"{clipState.clipTime:0.00} {Mathf.Round(clipState.weight * 100f)}%");
                    }
                    else
                    {
                        sb.Append("disabled");
                    }
                    sb.AppendLine();
                }
                stateJSON.val = sb.ToString();
                yield return 0;
                yield return 0;
            }
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

