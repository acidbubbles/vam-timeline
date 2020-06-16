using System;
using System.Linq;
using UnityEngine.UI;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class EditLayersScreen : ScreenBase
    {
        public const string ScreenName = "Layers";

        public override string name => ScreenName;

        public EditLayersScreen(IAtomPlugin plugin)
            : base(plugin)
        {

        }
        public override void Init()
        {
            base.Init();

            InitRenameLayers(true);

            CreateChangeScreenButton("<i><b>Clips</b></i>", ClipsScreen.ScreenName, true);
            CreateChangeScreenButton("<i><b>Add</b> a new animation...</i>", AddAnimationScreen.ScreenName, true);
        }

        private void InitRenameLayers(bool rightSide)
        {
            foreach (var layer in animation.clips.Select(c => c.animationLayer).Distinct())
            {
                InitRenameLayer(layer, rightSide);
            }
        }

        private void InitRenameLayer(string layer, bool rightSide)
        {
            var layerNameJSON = new JSONStorableString("Animation Name", "", (string val) => UpdateLayerName(ref layer, val));
            RegisterStorable(layerNameJSON);
            var layerNameUI = plugin.CreateTextInput(layerNameJSON, rightSide);
            RegisterComponent(layerNameUI);
            var layout = layerNameUI.GetComponent<LayoutElement>();
            layout.minHeight = 50f;
            layerNameUI.height = 50;
            layerNameJSON.valNoCallback = layer;
        }

        private void UpdateLayerName(ref string from, string to)
        {
            var layer = from;
            foreach(var clip in animation.clips.Where(c => c.animationLayer == layer))
            clip.animationLayer = to;
            from = to;
        }
    }
}

