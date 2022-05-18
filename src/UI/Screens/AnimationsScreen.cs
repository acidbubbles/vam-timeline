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

            if (animation.index.segmentIds.Contains(AtomAnimationClip.SharedAnimationSegmentId))
            {
                prefabFactory.CreateHeader("Animations", 1);
                InitClipsUI(AtomAnimationClip.SharedAnimationSegmentId);
                prefabFactory.CreateSpacer();
            }

            if (current.animationSegmentId == AtomAnimationClip.NoneAnimationSegmentId)
            {
                prefabFactory.CreateHeader($"Animations", 1);
                InitClipsUI(current.animationSegmentId);
                prefabFactory.CreateSpacer();
            } else if (current.animationSegmentId != AtomAnimationClip.SharedAnimationSegmentId)
            {
                prefabFactory.CreateHeader($"{current.animationSegment} animations", 1);
                InitClipsUI(current.animationSegmentId);
                prefabFactory.CreateSpacer();
            }

            prefabFactory.CreateHeader("Operations", 1);

            CreateChangeScreenButton("<i><b>Create</b> anims/layers/segments...</i>", AddAnimationsScreen.ScreenName);
            CreateChangeScreenButton("<i><b>Manage/reorder</b> animations...</i>", ManageAnimationsScreen.ScreenName);
        }

        private void InitClipsUI(int segmentNameId)
        {
            var layers = animation.index.segmentsById[segmentNameId].layers;
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
                    animation.SoftStopClip(clip, clip.blendInDuration);
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
                    if (clip.playbackScheduledNextAnimation != null)
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

