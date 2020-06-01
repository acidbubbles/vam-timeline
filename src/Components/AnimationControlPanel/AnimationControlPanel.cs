using UnityEngine;
using UnityEngine.Events;
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
        private AtomAnimation _animation;

        public bool locked
        {
            get
            {
                return _dopeSheet.enabled;
            }
            set
            {
                _dopeSheet.locked = value;
            }
        }

        public AnimationControlPanel()
        {
            gameObject.AddComponent<VerticalLayoutGroup>();
        }

        public void Bind(IAtomPlugin plugin)
        {
            // TODO: Integrate play/stop inside scrubber
            _scrubber = InitScrubber(plugin.ScrubberJSON, plugin.SnapJSON);
            InitSpacer();
            // TODO: Make the JSON use animation features instead of the other way around
            InitFrameNav(plugin.Manager.configurableButtonPrefab, plugin.PreviousFrameJSON, plugin.NextFrameJSON);
            InitSpacer();
            InitPlaybackButtons(plugin.Manager.configurableButtonPrefab, plugin.PlayJSON, plugin.StopJSON);
            InitSpacer();
            _dopeSheet = InitDopeSheet();
            InitSpacer();
        }

        public void Bind(AtomAnimation animation)
        {
            _animation = animation;
            _scrubber.animation = animation;
            _dopeSheet.Bind(animation);
        }

        private Scrubber InitScrubber(JSONStorableFloat scrubberJSON, JSONStorableFloat snapJSON)
        {
            var go = new GameObject();
            go.transform.SetParent(transform, false);

            go.AddComponent<LayoutElement>().preferredHeight = 60f;

            var scrubber = go.AddComponent<Scrubber>();
            scrubber.scrubberJSON = scrubberJSON;
            scrubber.snapJSON = snapJSON;

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
            gridLayout.spacing = 2f;
            gridLayout.childForceExpandWidth = false;
            gridLayout.childControlWidth = true;

            CreateSmallButton(buttonPrefab, container.transform, "\u00AB", () => previousFrameJSON.actionCallback());

            CreateSmallButton(buttonPrefab, container.transform, "-1s", () =>
            {
                var time = _animation.Time - 1f;
                if (time < 0) time = 0;
                _animation.Time = time;
            });

            CreateSmallButton(buttonPrefab, container.transform, "-.1s", () =>
            {
                var time = _animation.Time - 0.1f;
                if (time < 0) time = 0;
                _animation.Time = time;
            });

            CreateSmallButton(buttonPrefab, container.transform, ">|<", () =>
            {
                _animation.Time = _animation.Time.Snap(1f);
            });

            CreateSmallButton(buttonPrefab, container.transform, "+.1s", () =>
            {
                var time = _animation.Time + 0.1f;
                if (time >= _animation.Current.AnimationLength - 0.001f) time = _animation.Current.Loop ? _animation.Current.AnimationLength - 0.1f : _animation.Current.AnimationLength;
                _animation.Time = time;
            });

            CreateSmallButton(buttonPrefab, container.transform, "+1s", () =>
            {
                var time = _animation.Time + 1f;
                if (time >= _animation.Current.AnimationLength - 0.001f) time = _animation.Current.Loop ? _animation.Current.AnimationLength - 1f : _animation.Current.AnimationLength;
                _animation.Time = time;
            });

            CreateSmallButton(buttonPrefab, container.transform, "\u00BB", () => nextFrameJSON.actionCallback());
        }

        private static void CreateSmallButton(Transform buttonPrefab, Transform parent, string label, UnityAction callback)
        {
            var btn = Instantiate(buttonPrefab);
            btn.SetParent(parent, false);
            var ui = btn.GetComponent<UIDynamicButton>();
            ui.label = label;
            ui.buttonText.fontSize = 27;
            ui.button.onClick.AddListener(callback);
            var layoutElement = btn.GetComponent<LayoutElement>();
            layoutElement.preferredWidth = 0;
            layoutElement.flexibleWidth = 20;
            layoutElement.minWidth = 20;
        }

        private DopeSheet InitDopeSheet()
        {
            var go = new GameObject();
            go.transform.SetParent(transform, false);

            go.AddComponent<LayoutElement>().flexibleHeight = 260f;

            var dopeSheet = go.AddComponent<DopeSheet>();

            return dopeSheet;
        }

        private void InitSpacer()
        {
            var go = new GameObject();
            go.transform.SetParent(transform, false);

            go.AddComponent<LayoutElement>().preferredHeight = 10f;
        }
    }
}
