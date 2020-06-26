using System.Collections;
using System.Linq;
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

        public override string screenId => ScreenName;

        public ClipsScreen()
            : base()
        {
        }

        public override void Init(IAtomPlugin plugin)
        {
            base.Init(plugin);

            if (animation.clips.Any())
            {
                var layerName = animation.clips[0].animationLayer;
                CreateHeader($"Layer: {layerName}");

                foreach (var clip in animation.clips)
                {
                    if (clip.animationLayer != layerName)
                    {
                        layerName = clip.animationLayer;
                        CreateHeader($"Layer: {layerName}");
                    }

                    InitAnimButton(clip);
                }
            }

            CreateSpacer(true);

            CreateChangeScreenButton("<i><b>Add</b> a new animation...</i>", AddAnimationScreen.ScreenName, true);
            CreateChangeScreenButton("<i><b>Edit</b> layers...</i>", EditLayersScreen.ScreenName, true);
        }

        private void InitAnimButton(AtomAnimationClip clip)
        {
            var clipState = animation.state.GetClip(clip.animationName);
            var btn = CreateButton($"...", true);
            RegisterComponent(btn);
            btn.buttonText.alignment = TextAnchor.MiddleLeft;
            btn.button.onClick.AddListener(() =>
            {
                if (clipState.mainInLayer)
                {
                    animation.StopClip(clip.animationName);
                }
                else
                {
                    animation.PlayClip(clip.animationName, true);
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
    }
}

