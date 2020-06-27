using System;
using System.Collections.Generic;
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

        public override string screenId => ScreenName;

        public EditLayersScreen()
            : base()
        {
        }

        public override void Init(IAtomPlugin plugin)
        {
            base.Init(plugin);

            InitRenameLayers();

            prefabFactory.CreateSpacer();

            InitCreateLayerUI();

            CreateChangeScreenButton("<i><b>Clips</b></i>", ClipsScreen.ScreenName);
            CreateChangeScreenButton("<i><b>Add</b> a new animation...</i>", AddAnimationScreen.ScreenName);
        }

        private void InitRenameLayers()
        {
            foreach (var layer in animation.clips.Select(c => c.animationLayer).Distinct())
            {
                InitRenameLayer(layer);
            }
        }

        private void InitRenameLayer(string layer)
        {
            var layerNameJSON = new JSONStorableString("Animation Name", "", (string val) => UpdateLayerName(ref layer, val));
            var layerNameUI = prefabFactory.CreateTextInput(layerNameJSON);
            var layout = layerNameUI.GetComponent<LayoutElement>();
            layout.minHeight = 50f;
            layerNameUI.height = 50;
            layerNameJSON.valNoCallback = layer;
        }

        public void InitCreateLayerUI()
        {
            var createLayerUI = prefabFactory.CreateButton("Create New Layer");
            createLayerUI.button.onClick.AddListener(() => AddLayer());
        }

        private void UpdateLayerName(ref string from, string to)
        {
            to = to.Trim();
            if (to == "")
                return;

            var layer = from;
            foreach (var clip in animation.clips.Where(c => c.animationLayer == layer))
                clip.animationLayer = to;
            from = to;
        }

        private void AddLayer()
        {
            var clip = animation.CreateClip(GetNewLayerName());

            animation.SelectAnimation(clip.animationName);
            onScreenChangeRequested.Invoke(EditScreen.ScreenName);
        }

        protected string GetNewLayerName()
        {
            var layers = new HashSet<string>(animation.clips.Select(c => c.animationLayer));
            for (var i = 1; i < 999; i++)
            {
                var layerName = "Layer " + i;
                if (!layers.Contains(layerName)) return layerName;
            }
            return Guid.NewGuid().ToString();
        }
    }
}

