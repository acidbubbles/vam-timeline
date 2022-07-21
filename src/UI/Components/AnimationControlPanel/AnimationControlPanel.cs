using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace VamTimeline
{
    public class AnimationControlPanel : MonoBehaviour
    {
        private const string _sharedSequenceLabel = "[Shared Layers]";

        public static AnimationControlPanel Configure(GameObject go)
        {
            var group = go.AddComponent<VerticalLayoutGroup>();
            group.spacing = 10f;

            var rect = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
            rect.pivot = new Vector2(0, 1);

            return go.AddComponent<AnimationControlPanel>();
        }

        private VamPrefabFactory _prefabFactory;
        private DopeSheet _dopeSheet;
        private AtomAnimationEditContext _animationEditContext;
        private JSONStorableStringChooser _segmentsJSON;
        private JSONStorableStringChooser _layersJSON;
        private JSONStorableStringChooser _animationsJSON;
        private UIDynamicButton _playAll;
        private UIDynamicButton _playClip;
        private UIDynamicPopup _segmentsUI;
        private UIDynamicPopup _layersUI;
        private UIDynamicPopup _clipsUI;

        public void Bind(IAtomPlugin plugin)
        {
            _prefabFactory = gameObject.AddComponent<VamPrefabFactory>();
            _prefabFactory.plugin = plugin;
            InitAnimationSelectorUI();
            InitFrameNav(plugin.manager.configurableButtonPrefab);
            InitPlaybackButtons(plugin.manager.configurableButtonPrefab);
            _dopeSheet = InitDopeSheet();
        }

        public void Bind(AtomAnimationEditContext animationEditContext)
        {
            _animationEditContext = animationEditContext;
            if (_dopeSheet != null) _dopeSheet.Bind(animationEditContext);
            _animationEditContext.animation.onClipsListChanged.AddListener(OnClipsListChanged);
            _animationEditContext.animation.onClipIsPlayingChanged.AddListener(OnClipIsPlayingChanged);
            _animationEditContext.onCurrentAnimationChanged.AddListener(OnCurrentAnimationChanged);
            _animationEditContext.onTimeChanged.AddListener(OnTimeChanged);
            SyncAnimationsListNow();
            _animationEditContext.current?.onAnimationSettingsChanged.AddListener(OnAnimationSettingsChanged);
            OnAnimationSettingsChanged(nameof(AtomAnimationClip.animationName));
        }

        private void InitAnimationSelectorUI()
        {
            _segmentsJSON = new JSONStorableStringChooser("Segment", new List<string>(), "", "Segment", val =>
            {
                _animationEditContext.SelectSegment(val);
                SyncAnimationsListNow();
            });
            _layersJSON = new JSONStorableStringChooser("Layer", new List<string>(), "", "Layer", (string val) =>
            {
                _animationEditContext.SelectLayer(val);
                SyncAnimationsListNow();
            });
            _animationsJSON = new JSONStorableStringChooser("Animation", new List<string>(), "", "Animation", (string _) =>
            {
                _animationEditContext.SelectAnimation(_segmentsJSON.val, _layersJSON.val, _animationsJSON.val);
            });

            _segmentsUI = _prefabFactory.CreateMicroPopup(_segmentsJSON, 900f);
            _segmentsUI.popup.onOpenPopupHandlers += OnSegmentsUIOpened;
            _layersUI = _prefabFactory.CreateMicroPopup(_layersJSON, 850f);
            _clipsUI = _prefabFactory.CreateMicroPopup(_animationsJSON, 800f);
            _clipsUI.popup.onOpenPopupHandlers += OnClipsUIOpened;
        }

        private void OnSegmentsUIOpened()
        {
            var playingSegment = _animationEditContext.animation.playingAnimationSegment;

            var buttonParent = _clipsUI.popup.buttonParent;
            for (var i = 0; i < buttonParent.childCount; i++)
            {
                var child = buttonParent.GetChild(i);
                var button = child.GetComponent<Button>();
                var buttonText = button.GetComponentInChildren<Text>(true);
                buttonText.fontStyle = buttonText.text == playingSegment ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        private void OnClipsUIOpened()
        {
            var playingAnimation =
                _animationEditContext.animation.index.segmentsById[_animationEditContext.current.animationSegmentId]
                .layersMapById[_animationEditContext.current.animationLayerId]
                .FirstOrDefault(c => c.playbackMainInLayer)
                ?.animationName;

            var buttonParent = _clipsUI.popup.buttonParent;
            for (var i = 0; i < buttonParent.childCount; i++)
            {
                var child = buttonParent.GetChild(i);
                var button = child.GetComponent<Button>();
                var buttonText = button.GetComponentInChildren<Text>(true);
                buttonText.fontStyle = buttonText.text == playingAnimation ? FontStyle.Bold : FontStyle.Normal;
            }
        }

        private void InitPlaybackButtons(Transform buttonPrefab)
        {
            var container = new GameObject("Playback");
            container.transform.SetParent(transform, false);

            var gridLayout = container.AddComponent<HorizontalLayoutGroup>();
            gridLayout.spacing = 4f;
            gridLayout.childForceExpandWidth = false;
            gridLayout.childControlWidth = true;

            var playAll = Instantiate(buttonPrefab, container.transform, false);
            playAll.GetComponent<UIDynamicButton>().label = "\u25B6 All";
            playAll.GetComponent<UIDynamicButton>().button.onClick.AddListener(() => _animationEditContext.PlayAll());
            playAll.GetComponent<LayoutElement>().preferredWidth = 0;
            playAll.GetComponent<LayoutElement>().flexibleWidth = 100;
            _playAll = playAll.GetComponent<UIDynamicButton>();

            var playClip = Instantiate(buttonPrefab, container.transform, false);
            playClip.GetComponent<UIDynamicButton>().label = "\u25B6 Clip";
            playClip.GetComponent<UIDynamicButton>().button.onClick.AddListener(() => _animationEditContext.PlayCurrentClip());
            playClip.GetComponent<LayoutElement>().preferredWidth = 0;
            playClip.GetComponent<LayoutElement>().flexibleWidth = 100;
            _playClip = playClip.GetComponent<UIDynamicButton>();

            var stop = Instantiate(buttonPrefab, container.transform, false);
            stop.GetComponent<UIDynamicButton>().label = "\u25A0 Stop";
            stop.GetComponent<UIDynamicButton>().button.onClick.AddListener(() => { _animationEditContext.Stop(); });
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

            CreateSmallButton(
                buttonPrefab, container.transform, "<\u0192",
                () => _animationEditContext.PreviousFrame(),
                () => _animationEditContext.RewindSeconds(_animationEditContext.snap)
            );


            CreateSmallButton(buttonPrefab, container.transform, "-1s",
                () => _animationEditContext.RewindSeconds(1f),
                () => _animationEditContext.RewindSeconds(0.01f)
            );

            CreateSmallButton(buttonPrefab, container.transform, "-.1s",
                () => _animationEditContext.RewindSeconds(0.1f),
                () => _animationEditContext.RewindSeconds(0.001f)
            );

            CreateSmallButton(buttonPrefab, container.transform, ">|<",
                () => _animationEditContext.SnapTo(1f),
                () => _animationEditContext.SnapToClosestKeyframe()
            );

            CreateSmallButton(buttonPrefab, container.transform, "+.1s",
                () => _animationEditContext.ForwardSeconds(0.1f),
                () => _animationEditContext.ForwardSeconds(0.001f)
            );

            CreateSmallButton(buttonPrefab, container.transform, "+1s",
                () => _animationEditContext.ForwardSeconds(1f),
                () => _animationEditContext.ForwardSeconds(0.01f)
            );

            CreateSmallButton(
                buttonPrefab, container.transform, "\u0192>",
                () => _animationEditContext.NextFrame(),
                () => _animationEditContext.ForwardSeconds(_animationEditContext.snap)
            );
        }

        private static void CreateSmallButton(Transform buttonPrefab, Transform parent, string label, UnityAction leftClick, UnityAction rightClick=null)
        {
            var btn = Instantiate(buttonPrefab, parent, false);
            var ui = btn.GetComponent<UIDynamicButton>();
            ui.label = label;
            ui.buttonText.fontSize = 27;

            var click = btn.gameObject.AddComponent<Clickable>();

            if (leftClick != null)
                click.onClick.AddListener(eventData => leftClick());

            if (rightClick != null)
                click.onRightClick.AddListener(eventData => rightClick());

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
            SyncAnimationsListNow();
        }

        private void SyncAnimationsListNow()
        {
            if(_animationEditContext.animation.index.segmentIds.Count == 0)
            {
                var emptyList = new List<string>();
                _segmentsJSON.choices = emptyList;
                _segmentsJSON.displayChoices = emptyList;
                _layersJSON.choices = emptyList;
                _animationsJSON.choices = emptyList;
                _segmentsUI.gameObject.SetActive(false);
                _layersUI.gameObject.SetActive(false);

                return;
            }

            _segmentsJSON.choices = _animationEditContext.animation.index.segmentNames;
            _segmentsJSON.displayChoices = _animationEditContext.animation.index.segmentNames.Select(x => x == AtomAnimationClip.SharedAnimationSegment ? _sharedSequenceLabel : x).ToList();
            _layersJSON.choices = _animationEditContext.animation.clips.Where(c => c.animationSegment == _animationEditContext.current.animationSegment).Select(c => c.animationLayer).Distinct().ToList();
            _animationsJSON.choices = _animationEditContext.animation.clips.Where(c => c.animationSegment == _animationEditContext.current.animationSegment && c.animationLayer == _animationEditContext.current.animationLayer).Select(c => c.animationName).ToList();

            _segmentsJSON.valNoCallback = null;
            _segmentsJSON.valNoCallback = _animationEditContext.current.animationSegment;
            _layersJSON.valNoCallback = null;
            _layersJSON.valNoCallback = _animationEditContext.current.animationLayer;
            _animationsJSON.valNoCallback = null;
            _animationsJSON.valNoCallback = _animationEditContext.current.animationName;

            _segmentsUI.gameObject.SetActive(_animationEditContext.animation.index.segmentIds.Count > 1 || _animationEditContext.animation.index.segmentIds[0] == AtomAnimationClip.SharedAnimationSegmentId);
            _layersUI.gameObject.SetActive(_animationEditContext.animation.index.segmentsById.Any(s => s.Value.layerNames.Count > 1));
        }

        private void OnCurrentAnimationChanged(AtomAnimationEditContext.CurrentAnimationChangedEventArgs args)
        {
            _segmentsJSON.valNoCallback = args.after.animationSegment;
            _layersJSON.valNoCallback = args.after.animationLayer;
            _animationsJSON.valNoCallback = args.after.animationName;
            args.before?.onAnimationSettingsChanged.RemoveListener(OnAnimationSettingsChanged);
            args.after?.onAnimationSettingsChanged.AddListener(OnAnimationSettingsChanged);
            OnAnimationSettingsChanged(nameof(AtomAnimationClip.animationName));
            OnTimeChanged(_animationEditContext.timeArgs);
            SyncAnimationsListNow();
        }

        private void OnAnimationSettingsChanged(string prop)
        {
            _playClip.label = $"\u25B6 {_animationEditContext.current.animationName}";
        }

        private void OnClipIsPlayingChanged(AtomAnimationClip clip)
        {
            OnTimeChanged(_animationEditContext.timeArgs);
        }

        private void OnTimeChanged(AtomAnimationEditContext.TimeChangedEventArgs args)
        {
            _playAll.button.interactable = !_animationEditContext.current.playbackEnabled;
            _playClip.button.interactable = !_animationEditContext.current.playbackEnabled;
        }

        public void OnDestroy()
        {
            if (_animationEditContext != null)
            {
                _animationEditContext.animation.onClipsListChanged.RemoveListener(OnClipsListChanged);
                _animationEditContext.animation.onClipIsPlayingChanged.RemoveListener(OnClipIsPlayingChanged);
                _animationEditContext.onCurrentAnimationChanged.RemoveListener(OnCurrentAnimationChanged);
                _animationEditContext.onTimeChanged.RemoveListener(OnTimeChanged);
                _animationEditContext.current?.onAnimationSettingsChanged.AddListener(OnAnimationSettingsChanged);
            }
        }

        public void ToggleDopeSheetMode()
        {
            _dopeSheet.ToggleMode();
        }
    }
}
