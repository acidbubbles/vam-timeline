using System.Collections;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class AnimationsScreen : ScreenBase
    {
        public const string ScreenName = "Animations";

        public override string screenId => ScreenName;

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            if (animation.clips.Any(c => c.animationSegment == AtomAnimationClip.SharedAnimationSegment))
            {
                prefabFactory.CreateHeader("Animations", 1);
                InitClipsUI(AtomAnimationClip.SharedAnimationSegment);
                prefabFactory.CreateSpacer();
            }

            if (current.animationSegment != AtomAnimationClip.SharedAnimationSegment)
            {
                prefabFactory.CreateHeader($"{current.animationSegment} animations", 1);
                InitClipsUI(current.animationSegment);
                prefabFactory.CreateSpacer();
            }

            prefabFactory.CreateHeader("Operations", 1);

            CreateChangeScreenButton("<i><b>Add</b> animations/layers...</i>", AddAnimationScreen.ScreenName);
            CreateChangeScreenButton("<i><b>Manage</b> animations list...</i>", ManageAnimationsScreen.ScreenName);
        }

        private void InitClipsUI(string segmentName)
        {
            var layers = animation.index.segments[segmentName].layers;
            var hasLayers = layers.Count > 1;

            foreach (var layer in layers)
            {
                if (hasLayers)
                {
                    prefabFactory.CreateHeader(layer[0].animationLayer, 2);
                }

                foreach (var clip in layer)
                {
                    InitAnimButton(clip);
                }
            }
        }

        private void InitAnimButton(AtomAnimationClip clip)
        {
            var btn = prefabFactory.CreateButton("");
            btn.buttonText.alignment = TextAnchor.MiddleLeft;
            btn.button.onClick.AddListener(() =>
            {
                if (clip.playbackMainInLayer)
                {
                    animation.SoftStopClip(clip);
                }
                else
                {
                    animation.PlayClip(clip, true);
                }
            });
            StartCoroutine(UpdateAnimButton(btn, clip));
        }

        private IEnumerator UpdateAnimButton(UIDynamicButton btn, AtomAnimationClip clip)
        {
            yield return 0;
            var playLabel = $" \u25B6 {clip.animationName}";
            while (!disposing)
            {
                if (UIPerformance.ShouldSkip(UIPerformance.LowFrequency))
                    yield return 0;

                if (clip.playbackMainInLayer && clip.playbackBlendRate == 0)
                {
                    if (clip.playbackScheduledNextAnimationName != null)
                        btn.label = $" \u25B6 [{clip.clipTime:00.00} seq>{clip.playbackScheduledNextTimeLeft:0.00}s] {clip.animationName}";
                    else
                        btn.label = $" \u25A0  [{clip.clipTime:00.00}] {clip.animationName}";
                }
                else if (clip.playbackEnabled)
                {
                    if(clip.playbackBlendRate > 0)
                        btn.label = $" \u25A0  [{clip.clipTime:00.00} in>{clip.playbackBlendWeight * 100:00}%] {clip.animationName}";
                    else
                        btn.label = $" \u25B6 [{clip.clipTime:00.00} out>{clip.playbackBlendWeight * 100:00}%] {clip.animationName}";
                }
                else
                {
                    if (btn.label != playLabel)
                        btn.label = playLabel;
                }

                yield return 0;
            }
        }

        protected override void OnCurrentAnimationChanged(AtomAnimationEditContext.CurrentAnimationChangedEventArgs args)
        {
            base.OnCurrentAnimationChanged(args);

            if (args.before.animationSegment != args.after.animationSegment)
            {
                ReloadScreen();
            }
        }
    }
}

