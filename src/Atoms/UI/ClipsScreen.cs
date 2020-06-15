using System.Linq;

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

            CreateChangeScreenButton("<i><b>Add</b> a new animation...</i>", AddAnimationScreen.ScreenName, true);
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

