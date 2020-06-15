using System.Linq;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class LayersScreen : ScreenBase
    {
        public const string ScreenName = "Layers";

        public override string name => ScreenName;

        public LayersScreen(IAtomPlugin plugin)
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
            var layers = new JSONStorableStringChooser("Layer", plugin.animation.clips.Select(c => c.animationLayer).Distinct().ToList(), current.animationLayer, "Layer", ChangeLayer);
            RegisterStorable(layers);
            var layersUI = plugin.CreateScrollablePopup(layers, rightSide);
            RegisterComponent(layersUI);
        }

        private void ChangeLayer(string val)
        {
            plugin.animation.ChangeAnimation(plugin.animation.clips.First(c => c.animationLayer == val).animationName);
        }
    }
}

