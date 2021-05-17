using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace VamTimeline
{
    public class TargetsScreen : ScreenBase
    {
        private static bool _lastFilterVal = true;

        public const string ScreenName = "Targets";

        public override string screenId => ScreenName;

        private readonly List<ITargetFrame> _targets = new List<ITargetFrame>();
        private bool _selectionChangedPending;
        private UIDynamicToggle _filterUI;
        private UIDynamicButton _manageTargetsUI;
        private UIDynamic _spacerUI;
        private JSONStorableBool _filterJSON;
        private Text _noTargetsUI;
        private JSONStorableString _textJSON;
        private UIDynamicTextField _textUI;

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            _filterJSON = new JSONStorableBool("Filter unselected targets", _lastFilterVal, val => { _lastFilterVal = val; OnSelectionChanged(); });

            animationEditContext.onTargetsSelectionChanged.AddListener(OnSelectionChanged);

            if (animation.IsEmpty())
            {
                InitExplanation();
                CreateAddRemoveTargetsButton(true);
            }
            else
            {
                OnSelectionChanged();

                var beginJSON = new JSONStorableFloat("[TEMP] View Begin", 0, 0, current.animationLength)
                {
                    setCallbackFunction = val => { animationEditContext.scrubberRange = new ScrubberRange {rangeBegin = val, rangeDuration = animationEditContext.scrubberRange.rangeDuration}; }
                };
                beginJSON.valNoCallback = animationEditContext.scrubberRange.rangeBegin;
                prefabFactory.CreateSlider(beginJSON);

                var rangeJSON = new JSONStorableFloat("[TEMP] View Range", current.animationLength, 0, current.animationLength)
                {
                    setCallbackFunction = val => { animationEditContext.scrubberRange = new ScrubberRange {rangeBegin = animationEditContext.scrubberRange.rangeBegin, rangeDuration = val}; }
                };
                rangeJSON.valNoCallback = animationEditContext.scrubberRange.rangeDuration;
                prefabFactory.CreateSlider(rangeJSON);
            }
        }

        private void InitExplanation()
        {
            _textJSON = new JSONStorableString("Help", @"
<b>Welcome to Timeline!</b>

For instructions on how to get started, check out <b>Help</b> in the <b>More</b> menu.

You'll find a built-in guide, and links to the more detailed wiki as well as tutorial videos.

" + GetRandomQuote());
            _textUI = prefabFactory.CreateTextField(_textJSON);
            _textUI.height = 1078f;
        }

        private static string GetRandomQuote()
        {
            switch (Random.Range(0, 9))
            {
                case 0: return "You look nice today!";
                case 1: return "Let's get creative!";
                case 2: return "I believe in you!";
                case 3: return "Show me what you got.";
                case 4: return "Don't forget to share!";
                case 5: return "You rock.";
                case 6: return "Here's a hug!";
                case 7: return "Don't forget to sleep!";
                case 8: return "Keep being you!";
                default: return "Hope you don't find any bugs!";
            }
        }

        protected override void OnCurrentAnimationChanged(AtomAnimationEditContext.CurrentAnimationChangedEventArgs args)
        {
            base.OnCurrentAnimationChanged(args);
            RefreshTargetsList();
        }

        private void OnSelectionChanged()
        {
            if (_selectionChangedPending) return;
            _selectionChangedPending = true;
            StartCoroutine(SelectionChangedDeferred());
        }

        private IEnumerator SelectionChangedDeferred()
        {
            yield return new WaitForEndOfFrame();
            _selectionChangedPending = false;
            if (_disposing) yield break;
            RefreshTargetsList();
        }

        private void RefreshTargetsList()
        {
            if (animation == null) return;
            RemoveTargets();
            RemoveTargetSiblingComponents();

            var hasTargets = false;

            foreach (var target in _filterJSON.val ? animationEditContext.GetAllOrSelectedTargets().OfType<TriggersAnimationTarget>() : current.targetTriggers)
            {
                hasTargets = true;
                var keyframeUI = prefabFactory.CreateSpacer();
                keyframeUI.height = 60f;
                var component = keyframeUI.gameObject.AddComponent<TriggersTargetFrame>();
                component.popupParent = popupParent;
                component.Bind(plugin, animationEditContext.current, target);
                _targets.Add(component);
            }

            foreach (var target in _filterJSON.val ? animationEditContext.GetAllOrSelectedTargets().OfType<FreeControllerAnimationTarget>() : current.targetControllers)
            {
                hasTargets = true;
                var keyframeUI = prefabFactory.CreateSpacer();
                keyframeUI.height = 60f;
                var component = keyframeUI.gameObject.AddComponent<ControllerTargetFrame>();
                component.Bind(plugin, animationEditContext.current, target);
                _targets.Add(component);
            }

            foreach (var target in _filterJSON.val ? animationEditContext.GetAllOrSelectedTargets().OfType<FloatParamAnimationTarget>() : current.targetFloatParams)
            {
                hasTargets = true;
                var keyframeUI = prefabFactory.CreateSpacer();
                keyframeUI.height = 60f;
                var component = keyframeUI.gameObject.AddComponent<FloatParamTargetFrame>();
                component.Bind(plugin, animationEditContext.current, target);
                _targets.Add(component);
            }

            if (!hasTargets)
            {
                _noTargetsUI = prefabFactory.CreateHeader("No targets", 2);
            }
            else
            {
                _spacerUI = prefabFactory.CreateSpacer();

                _filterUI = prefabFactory.CreateToggle(_filterJSON);
                _filterUI.backgroundColor = NavButtonColor;
                var toggleColors = _filterUI.toggle.colors;
                toggleColors.normalColor = NavButtonColor;
                _filterUI.toggle.colors = toggleColors;
            }

            CreateAddRemoveTargetsButton(current.GetAllTargetsCount() == 0);
        }

        private void CreateAddRemoveTargetsButton(bool highlight)
        {
            _manageTargetsUI = CreateChangeScreenButton("<b>[+/-]</b> Add/remove targets", AddRemoveTargetsScreen.ScreenName);
            if (highlight)
                _manageTargetsUI.buttonColor = new Color(0f, 1f, 0f);
            else
                _manageTargetsUI.buttonColor = NavButtonColor;
        }

        public override void OnDestroy()
        {
            animationEditContext.onTargetsSelectionChanged.RemoveListener(OnSelectionChanged);
            Destroy(_manageTargetsUI);
            RemoveTargets();
            base.OnDestroy();
        }

        private void RemoveTargets()
        {
            foreach (var targetRef in _targets)
            {
                Destroy(targetRef.gameObject);
            }
            _targets.Clear();
            Destroy(_manageTargetsUI?.gameObject);
        }

        private void RemoveTargetSiblingComponents()
        {
            if (_filterUI != null) prefabFactory.RemoveToggle(_filterJSON, _filterUI);
            if (_textUI != null) prefabFactory.RemoveTextField(_textJSON, _textUI);
            _filterUI = null;
            Destroy(_noTargetsUI?.gameObject);
            _noTargetsUI = null;
            Destroy(_spacerUI?.gameObject);
            _spacerUI = null;
            Destroy(_manageTargetsUI?.gameObject);
            _manageTargetsUI = null;
        }
    }
}

