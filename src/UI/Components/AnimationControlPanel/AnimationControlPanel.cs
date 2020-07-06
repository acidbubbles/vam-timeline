using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace VamTimeline
{
    public class AnimationControlPanel : MonoBehaviour
    {
        public static AnimationControlPanel Configure(GameObject go)
        {
            var group = go.AddComponent<VerticalLayoutGroup>();
            group.spacing = 10f;

            var rect = go.AddComponent<RectTransform>() ?? go.GetComponent<RectTransform>();
            rect.pivot = new Vector2(0, 1);

            return go.AddComponent<AnimationControlPanel>();
        }

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

        public void Bind(IAtomPlugin plugin)
        {
            _animationsJSON = InitAnimationSelectorUI(plugin.manager.configurableScrollablePopupPrefab);
            _scrubber = InitScrubber();
            // TODO: Make the JSON use animation features instead of the other way around
            InitFrameNav(plugin.manager.configurableButtonPrefab);
            InitPlaybackButtons(plugin.manager.configurableButtonPrefab);
            _dopeSheet = InitDopeSheet();
        }

        public void Bind(AtomAnimation animation)
        {
            _animation = animation;
            if (_scrubber != null) _scrubber.animation = animation;
            if (_dopeSheet != null) _dopeSheet.Bind(animation);
            _animation.onClipsListChanged.AddListener(OnClipsListChanged);
            _animation.onCurrentAnimationChanged.AddListener(OnCurrentAnimationChanged);
            SyncAnimationsListNow();
        }

        private JSONStorableStringChooser InitAnimationSelectorUI(Transform configurableScrollablePopupPrefab)
        {
            var jsc = new JSONStorableStringChooser("Animation", new List<string>(), "", "Animation", (string val) =>
            {
                if (_ignoreAnimationChange) return;
                _animation?.SelectAnimation(val);
            });

            var popup = Instantiate(configurableScrollablePopupPrefab);
            popup.SetParent(transform, false);

            var ui = popup.GetComponent<UIDynamicPopup>();
            ui.label = "Play";
            ui.popupPanelHeight = GetComponent<UIDynamic>()?.height ?? 500;

            jsc.popup = ui.popup;

            return jsc;
        }

        private Scrubber InitScrubber()
        {
            var go = new GameObject("Scrubber");
            go.transform.SetParent(transform, false);

            go.AddComponent<LayoutElement>().preferredHeight = 60f;

            var scrubber = go.AddComponent<Scrubber>();

            return scrubber;
        }

        private void InitPlaybackButtons(Transform buttonPrefab)
        {
            var container = new GameObject("Playback");
            container.transform.SetParent(transform, false);

            var gridLayout = container.AddComponent<HorizontalLayoutGroup>();
            gridLayout.spacing = 4f;
            gridLayout.childForceExpandWidth = false;
            gridLayout.childControlWidth = true;

            var playAll = Instantiate(buttonPrefab);
            playAll.SetParent(container.transform, false);
            playAll.GetComponent<UIDynamicButton>().label = "\u25B6 Seq";
            playAll.GetComponent<UIDynamicButton>().button.onClick.AddListener(() => _animation.PlayAll());
            playAll.GetComponent<LayoutElement>().preferredWidth = 0;
            playAll.GetComponent<LayoutElement>().flexibleWidth = 100;

            var playClip = Instantiate(buttonPrefab);
            playClip.SetParent(container.transform, false);
            playClip.GetComponent<UIDynamicButton>().label = "\u25B6 Clip";
            playClip.GetComponent<UIDynamicButton>().button.onClick.AddListener(() => _animation.PlayClip(_animation.current, false));
            playClip.GetComponent<LayoutElement>().preferredWidth = 0;
            playClip.GetComponent<LayoutElement>().flexibleWidth = 100;

            var stop = Instantiate(buttonPrefab);
            stop.SetParent(container.transform, false);
            stop.GetComponent<UIDynamicButton>().label = "\u25A0 Stop";
            stop.GetComponent<UIDynamicButton>().button.onClick.AddListener(() => { if (_animation.isPlaying) _animation.StopAll(); else _animation.ResetAll(); });
            stop.GetComponent<LayoutElement>().preferredWidth = 0;
            stop.GetComponent<LayoutElement>().flexibleWidth = 30;
        }

        private void InitFrameNav(Transform buttonPrefab)
        {
            var container = new GameObject("Frame Nav");
            container.transform.SetParent(transform, false);

            var gridLayout = container.AddComponent<HorizontalLayoutGroup>();
            gridLayout.spacing = 2f;
            gridLayout.childForceExpandWidth = false;
            gridLayout.childControlWidth = true;

            CreateSmallButton(buttonPrefab, container.transform, "<\u0192", () =>
            {
                _animation.clipTime = _animation.current.GetPreviousFrame(_animation.clipTime);
            });

            CreateSmallButton(buttonPrefab, container.transform, "-1s", () =>
            {
                var time = _animation.clipTime - 1f;
                if (time < 0)
                    time = 0;
                _animation.clipTime = time;
            });

            CreateSmallButton(buttonPrefab, container.transform, "-.1s", () =>
            {
                var time = _animation.clipTime - 0.1f;
                if (time < 0)
                    time = 0;
                _animation.clipTime = time;
            });

            CreateSmallButton(buttonPrefab, container.transform, ">|<", () =>
            {
                _animation.clipTime = _animation.clipTime.Snap(1f);
            });

            CreateSmallButton(buttonPrefab, container.transform, "+.1s", () =>
            {
                var time = _animation.clipTime + 0.1f;
                if (time >= _animation.current.animationLength - 0.001f)
                    time = _animation.current.loop ? _animation.current.animationLength - 0.1f : _animation.current.animationLength;
                _animation.clipTime = time;
            });

            CreateSmallButton(buttonPrefab, container.transform, "+1s", () =>
            {
                var time = _animation.clipTime + 1f;
                if (time >= _animation.current.animationLength - 0.001f)
                    time = _animation.current.loop ? _animation.current.animationLength - 1f : _animation.current.animationLength;
                _animation.clipTime = time;
            });

            CreateSmallButton(buttonPrefab, container.transform, "\u0192>", () =>
            {
                _animation.clipTime = _animation.current.GetNextFrame(_animation.clipTime);
            });
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

        private void OnClipsListChanged()
        {
            if (_ignoreAnimationChange) return;
            StartCoroutine(SyncAnimationsList());
        }

        private IEnumerator SyncAnimationsList()
        {
            yield return 0;
            SyncAnimationsListNow();
        }

        private void SyncAnimationsListNow()
        {
            _ignoreAnimationChange = true;
            try
            {
                var hasLayers = _animation.EnumerateLayers().Skip(1).Any();
                _animationsJSON.choices = _animation.clips.Select(c => c.animationName).ToList();
                if (hasLayers)
                    _animationsJSON.displayChoices = _animation.clips.Select(c => $"[{c.animationLayer}] {c.animationName}").ToList();
                _animationsJSON.valNoCallback = null;
                _animationsJSON.valNoCallback = _animation.current.animationName;
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
