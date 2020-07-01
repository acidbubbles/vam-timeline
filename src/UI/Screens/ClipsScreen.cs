using System.Collections;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class ClipsScreen : ScreenBase
    {
        public const string ScreenName = "Animations";

        public override string screenId => ScreenName;

        public ClipsScreen()
            : base()
        {
        }

        public override void Init(IAtomPlugin plugin)
        {
            base.Init(plugin);

            InitClipsUI();

            prefabFactory.CreateSpacer();

            CreateChangeScreenButton("<i><b>Add</b> a new animation...</i>", AddAnimationScreen.ScreenName);
            CreateChangeScreenButton("<i><b>Edit</b> layers and clips...</i>", EditLayersScreen.ScreenName);
        }

        private void InitClipsUI()
        {
            if (!animation.clips.Any()) return;

            var hasLayers = animation.clips.Select(a => a.animationLayer).Distinct().Count() > 1;

            var layerName = animation.clips[0].animationLayer;
            if (hasLayers)
                CreateHeader($"{layerName}");

            foreach (var clip in animation.clips)
            {
                if (hasLayers && clip.animationLayer != layerName)
                {
                    layerName = clip.animationLayer;
                    CreateHeader($"{layerName}");
                }

                InitAnimButton(clip);
            }
        }

        private void InitAnimButton(AtomAnimationClip clip)
        {
            var btn = prefabFactory.CreateButton($"...");
            btn.buttonText.alignment = TextAnchor.MiddleLeft;
            btn.button.onClick.AddListener(() =>
            {
                if (clip.mainInLayer)
                {
                    animation.StopClip(clip.animationName);
                }
                else
                {
                    animation.PlayClip(clip.animationName, true);
                }
            });
            StartCoroutine(UpdateAnimButton(btn, clip));
        }

        private IEnumerator UpdateAnimButton(UIDynamicButton btn, AtomAnimationClip clip)
        {
            yield return 0;
            var playLabel = $" \u25B6 {clip.animationName}";
            while (!_disposing)
            {
                if (!clip.enabled)
                {
                    if (btn.label != playLabel)
                        btn.label = playLabel;
                }
                else
                {
                    btn.label = $" \u25A0 [time: {clip.clipTime:00.000}, weight: {Mathf.Round(clip.weight * 100f):000}%]";
                }

                for (var i = 0; i < 4; i++)
                    yield return 0;
            }
        }
    }
}

