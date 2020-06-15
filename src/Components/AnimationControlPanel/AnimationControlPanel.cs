using System.Collections.Generic;
using System.Linq;
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
        private JSONStorableStringChooser _animationsJSON;
        private bool _ignoreAnimationChange;

        public bool locked
        {
            get
            {
                return _dopeSheet.enabled;
            }
            set
            {
                _dopeSheet.locked = value;
                _scrubber.enabled = !value;
            }
        }

        public AnimationControlPanel()
        {
            gameObject.AddComponent<VerticalLayoutGroup>();
        }

        public void Bind(IAtomPlugin plugin)
        {
            _animationsJSON = InitAnimationSelectorUI(plugin.manager.configurableScrollablePopupPrefab);
            InitSpacer();
            _scrubber = InitScrubber(plugin.scrubberJSON, plugin.snapJSON);
            InitSpacer();
            // TODO: Make the JSON use animation features instead of the other way around
            InitFrameNav(plugin.manager.configurableButtonPrefab, plugin.previousFrameJSON, plugin.nextFrameJSON);
            InitSpacer();
            InitPlaybackButtons(plugin.manager.configurableButtonPrefab, plugin.playJSON, plugin.stopJSON);
            InitSpacer();
            _dopeSheet = InitDopeSheet();
        }

        public void Bind(AtomAnimation animation)
        {
            _animation = animation;
            if (_scrubber != null) _scrubber.animation = animation;
            if (_dopeSheet != null) _dopeSheet.Bind(animation);
            _animation.onClipsListChanged.AddListener(OnClipsListChanged);
            _animation.onCurrentAnimationChanged.AddListener(OnCurrentAnimationChanged);
            OnClipsListChanged();
            OnCurrentAnimationChanged(new AtomAnimation.CurrentAnimationChangedEventArgs { after = animation.current });
        }

        private JSONStorableStringChooser InitAnimationSelectorUI(Transform configurableScrollablePopupPrefab)
        {
            var jsc = new JSONStorableStringChooser("Animation", new List<string>(), "", "Animation", (string val) =>
            {
                if (_ignoreAnimationChange) return;
                _animation?.ChangeAnimation(val);
            });

            var popup = Instantiate(configurableScrollablePopupPrefab);
            popup.SetParent(transform, false);

            var ui = popup.GetComponent<UIDynamicPopup>();
            ui.label = "Play";
            ui.popupPanelHeight = GetComponent<UIDynamic>()?.height ?? 500;

            jsc.popup = ui.popup;

            return jsc;
        }

        private Scrubber InitScrubber(JSONStorableFloat scrubberJSON, JSONStorableFloat snapJSON)
        {
            var go = new GameObject("Scrubber");
            go.transform.SetParent(transform, false);

            go.AddComponent<LayoutElement>().preferredHeight = 60f;

            var scrubber = go.AddComponent<Scrubber>();
            scrubber.scrubberJSON = scrubberJSON;
            scrubber.snapJSON = snapJSON;

            return scrubber;
        }

        private void InitPlaybackButtons(Transform buttonPrefab, JSONStorableAction playJSON, JSONStorableAction stopJSON)
        {
            var container = new GameObject("Playback");
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
            var container = new GameObject("Frame Nav");
            container.transform.SetParent(transform, false);

            var gridLayout = container.AddComponent<HorizontalLayoutGroup>();
            gridLayout.spacing = 2f;
            gridLayout.childForceExpandWidth = false;
            gridLayout.childControlWidth = true;

            CreateSmallButton(buttonPrefab, container.transform, "<\u0192", () => previousFrameJSON.actionCallback());

            CreateSmallButton(buttonPrefab, container.transform, "-1s", () =>
            {
                var time = _animation.time - 1f;
                if (time < 0) time = 0;
                _animation.time = time;
            });

            CreateSmallButton(buttonPrefab, container.transform, "-.1s", () =>
            {
                var time = _animation.time - 0.1f;
                if (time < 0) time = 0;
                _animation.time = time;
            });

            CreateSmallButton(buttonPrefab, container.transform, ">|<", () =>
            {
                _animation.time = _animation.time.Snap(1f);
            });

            CreateSmallButton(buttonPrefab, container.transform, "+.1s", () =>
            {
                var time = _animation.time + 0.1f;
                if (time >= _animation.current.animationLength - 0.001f) time = _animation.current.loop ? _animation.current.animationLength - 0.1f : _animation.current.animationLength;
                _animation.time = time;
            });

            CreateSmallButton(buttonPrefab, container.transform, "+1s", () =>
            {
                var time = _animation.time + 1f;
                if (time >= _animation.current.animationLength - 0.001f) time = _animation.current.loop ? _animation.current.animationLength - 1f : _animation.current.animationLength;
                _animation.time = time;
            });

            CreateSmallButton(buttonPrefab, container.transform, "\u0192>", () => nextFrameJSON.actionCallback());
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
            var go = new GameObject("Dope Sheet");
            go.transform.SetParent(transform, false);

            go.AddComponent<LayoutElement>().flexibleHeight = 260f;

            var dopeSheet = go.AddComponent<DopeSheet>();

            return dopeSheet;
        }

        private void InitSpacer()
        {
            var go = new GameObject("Spacer");
            go.transform.SetParent(transform, false);

            go.AddComponent<LayoutElement>().preferredHeight = 10f;
        }

        private void OnClipsListChanged()
        {
            _ignoreAnimationChange = true;
            try
            {
                var choices = _animation.clips.Where(c => c.animationLayer == _animation.current.animationLayer).Select(c => c.animationName).ToList();
                _animationsJSON.choices = choices;
            }
            finally
            {
                _ignoreAnimationChange = false;
            }
        }

        private void OnCurrentAnimationChanged(AtomAnimation.CurrentAnimationChangedEventArgs args)
        {
            _animationsJSON.valNoCallback = args.after.animationName;
        }

        public void OnDestroy()
        {
            if (_animation != null)
            {
                _animation.onClipsListChanged.RemoveListener(OnClipsListChanged);
                _animation.onCurrentAnimationChanged.RemoveListener(OnCurrentAnimationChanged);
            }
        }
    }
}
