using System.Collections;
using System.Collections.Generic;
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

            var playingAnimationSegmentId = animation.playingAnimationSegmentId;
            if (playingAnimationSegmentId == AtomAnimationClip.NoneAnimationSegmentId && animation.index.useSegment)
                playingAnimationSegmentId = current.animationSegmentId;

            if (animation.index.segmentIds.Contains(AtomAnimationClip.SharedAnimationSegmentId))
            {
                prefabFactory.CreateHeader("Animations", 1);
                InitClipsUI(AtomAnimationClip.SharedAnimationSegmentId);
                prefabFactory.CreateSpacer();
            }

            if (animation.index.useSegment)
            {
                prefabFactory.CreateHeader("Segments", 1);
                InitSegmentsUI();
                prefabFactory.CreateSpacer();
            }

            if (playingAnimationSegmentId == AtomAnimationClip.NoneAnimationSegmentId)
            {
                prefabFactory.CreateHeader($"Animations", 1);
                InitClipsUI(AtomAnimationClip.NoneAnimationSegmentId);
                prefabFactory.CreateSpacer();
            }
            else if (playingAnimationSegmentId != AtomAnimationClip.SharedAnimationSegmentId)
            {
                prefabFactory.CreateHeader($"{animation.playingAnimationSegment} animations", 1);
                InitClipsUI(playingAnimationSegmentId);
                prefabFactory.CreateSpacer();
            }

            prefabFactory.CreateHeader("Operations", 1);

            CreateChangeScreenButton("<i><b>Create</b> anims/layers/segments...</i>", AddAnimationsScreen.ScreenName);
            CreateChangeScreenButton("<i><b>Manage/reorder</b> animations...</i>", ManageAnimationsScreen.ScreenName);

            animation.onSegmentChanged.AddListener(ReloadScreen);
        }

        #region Clips

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
                        btn.label = $" \u25B6 [{clip.clipTime:00.00} seq>{clip.playbackScheduledNextTimeLeft:0.00}s] <b>{clip.animationName}</b>";
                    else
                        btn.label = $" \u25A0  [{clip.clipTime:00.00}] <b>{clip.animationName}</b>";
                }
                else if (clip.playbackEnabled)
                {
                    if(clip.playbackMainInLayer)
                        btn.label = $" \u25A0  [{clip.clipTime:00.00} in>{clip.playbackBlendWeight * 100:00}%] <b>{clip.animationName}</b>";
                    else if(clip.playbackBlendRate > 0)
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

        #endregion

        #region Segments

        private void InitSegmentsUI()
        {
            foreach (var segment in animation.index.segmentNames)
            {
                InitSegmentButton(segment);
            }
        }

        private void InitSegmentButton(string segment)
        {
            var segmentId = segment.ToId();
            var btn = prefabFactory.CreateButton("");
            btn.buttonText.alignment = TextAnchor.MiddleLeft;
            btn.button.onClick.AddListener(() =>
            {
                if (animation.playingAnimationSegmentId != segmentId)
                {
                    animation.PlaySegment(animation.index.segmentsById[segmentId].mainClip);
                }
            });
            if (segmentId == animation.playingAnimationSegmentId)
            {
                btn.label = $"<b>      {segment}</b>";
                btn.button.interactable = false;
            }
            else
            {
                btn.label = $" \u25B6 {segment}";
                btn.button.interactable = true;
            }
        }

        #endregion

        public override void OnDestroy()
        {
            base.OnDestroy();
            animation.onSegmentChanged.RemoveListener(ReloadScreen);
        }
    }
}

