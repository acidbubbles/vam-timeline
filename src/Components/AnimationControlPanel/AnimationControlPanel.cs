using UnityEngine;
using UnityEngine.UI;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class AnimationControlPanel : MonoBehaviour
    {
        private DopeSheet _dopeSheet;
        private Scrubber _scrubber;

        public AnimationControlPanel()
        {
            gameObject.AddComponent<VerticalLayoutGroup>();
        }

        public void Bind(IAtomPlugin plugin)
        {
            _scrubber = InitScrubber(plugin.ScrubberJSON);
            InitSpacer();
            // TODO: Integrate play/stop inside scrubber
            InitPlaybackButtons(plugin.Manager.configurableButtonPrefab, plugin.PlayJSON, plugin.StopJSON);
            InitSpacer();
            _dopeSheet = InitDopeSheet();
            InitSpacer();
            InitFrameNav(plugin.Manager.configurableButtonPrefab, plugin.PreviousFrameJSON, plugin.NextFrameJSON);
        }

        private Scrubber InitScrubber(JSONStorableFloat scrubberJSON)
        {
            // TODO: Multiple sliders will be created for the same scrubber. Replace by a custom (and nicer) component.
            var go = new GameObject();
            go.transform.SetParent(transform, false);

            go.AddComponent<LayoutElement>().preferredHeight = 60f;

            var scrubber = go.AddComponent<Scrubber>();
            scrubber.jsf = scrubberJSON;

            return scrubber;
        }

        private void InitPlaybackButtons(Transform buttonPrefab, JSONStorableAction playJSON, JSONStorableAction stopJSON)
        {
            var container = new GameObject();
            container.transform.SetParent(transform, false);

            var gridLayout = container.AddComponent<HorizontalLayoutGroup>();
            gridLayout.spacing = 4f;
            gridLayout.childForceExpandWidth = false;
            gridLayout.childControlWidth = true;

            var play = Instantiate(buttonPrefab);
            play.SetParent(container.transform, false);
            play.GetComponent<UIDynamicButton>().label = "\u25B6 Play";
            play.GetComponent<UIDynamicButton>().button.onClick.AddListener(() => playJSON.actionCallback());
            play.GetComponent<LayoutElement>().preferredWidth = 0;
            play.GetComponent<LayoutElement>().flexibleWidth = 100;

            var stop = Instantiate(buttonPrefab);
            stop.SetParent(container.transform, false);
            stop.GetComponent<UIDynamicButton>().label = "\u25A0 Stop";
            stop.GetComponent<UIDynamicButton>().button.onClick.AddListener(() => stopJSON.actionCallback());
            stop.GetComponent<LayoutElement>().preferredWidth = 0;
            stop.GetComponent<LayoutElement>().flexibleWidth = 30;
        }

        private void InitFrameNav(Transform buttonPrefab, JSONStorableAction previousFrameJSON, JSONStorableAction nextFrameJSON)
        {
            var container = new GameObject();
            container.transform.SetParent(transform, false);

            var gridLayout = container.AddComponent<HorizontalLayoutGroup>();
            gridLayout.spacing = 4f;
            gridLayout.childForceExpandWidth = false;
            gridLayout.childControlWidth = true;

            var previousFrame = Instantiate(buttonPrefab);
            previousFrame.SetParent(container.transform, false);
            previousFrame.GetComponent<UIDynamicButton>().label = "\u2190 Previous Frame";
            previousFrame.GetComponent<UIDynamicButton>().button.onClick.AddListener(() => previousFrameJSON.actionCallback());
            previousFrame.GetComponent<LayoutElement>().preferredWidth = 0;
            previousFrame.GetComponent<LayoutElement>().flexibleWidth = 50;

            var nextFrame = Instantiate(buttonPrefab);
            nextFrame.SetParent(container.transform, false);
            nextFrame.GetComponent<UIDynamicButton>().label = "Next Frame \u2192";
            nextFrame.GetComponent<UIDynamicButton>().button.onClick.AddListener(() => nextFrameJSON.actionCallback());
            nextFrame.GetComponent<LayoutElement>().preferredWidth = 0;
            nextFrame.GetComponent<LayoutElement>().flexibleWidth = 50;
        }

        private DopeSheet InitDopeSheet()
        {
            var go = new GameObject();
            go.transform.SetParent(transform, false);

            go.AddComponent<LayoutElement>().flexibleHeight = 260f;

            // TODO: Get rid of the "filtered" list and instead use a boolean flag on the animation clip target
            return go.AddComponent<DopeSheet>();
        }

        private void InitSpacer()
        {
            var go = new GameObject();
            go.transform.SetParent(transform, false);

            go.AddComponent<LayoutElement>().preferredHeight = 10f;
        }

        public void Bind(AtomAnimationClip current)
        {
            _dopeSheet.Bind(current);
        }

        public void SetScrubberPosition(float time)
        {
            _dopeSheet.SetScrubberPosition(time);
        }
    }
}
