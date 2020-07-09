using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class TargetsScreen : ScreenBase
    {
        private static bool _lastFilterVal = true;

        public const string ScreenName = "Targets";

        public override string screenId => ScreenName;

        private class TargetRef
        {
            public ITargetFrame Component;
            public IAtomAnimationTarget Target;
        }

        private readonly List<TargetRef> _targets = new List<TargetRef>();
        private bool _selectionChangedPending;
        private UIDynamicToggle _filterUI;
        private UIDynamicButton _manageTargetsUI;
        private UIDynamic _spacerUI;
        private JSONStorableBool _filterJSON;
        private UnityEngine.UI.Text _noTargetsUI;
        private JSONStorableString _textJSON;
        private UIDynamicTextField _textUI;

        public TargetsScreen()
            : base()
        {

        }
        public override void Init(IAtomPlugin plugin)
        {
            base.Init(plugin);

            _filterJSON = new JSONStorableBool("Filter unselected targets", _lastFilterVal, (bool val) => { _lastFilterVal = val; OnSelectionChanged(); });

            current.onTargetsSelectionChanged.AddListener(OnSelectionChanged);

            if (animation.IsEmpty())
            {
                InitExplanation();
                CreateAddRemoveTargetsButton(true);
            }
            else
            {
                OnSelectionChanged();
            }
        }

        private void InitExplanation()
        {
            _textJSON = new JSONStorableString("Help", HelpScreen.HelpText);
            _textUI = prefabFactory.CreateTextField(_textJSON);
            _textUI.height = 1078f;
        }

        protected override void OnCurrentAnimationChanged(AtomAnimation.CurrentAnimationChangedEventArgs args)
        {
            base.OnCurrentAnimationChanged(args);
            args.before.onTargetsSelectionChanged.RemoveListener(OnSelectionChanged);
            args.after.onTargetsSelectionChanged.AddListener(OnSelectionChanged);
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

            var time = animation.clipTime;
            var hasTargets = false;

            foreach (var target in _filterJSON.val ? current.GetAllOrSelectedTargets().OfType<TriggersAnimationTarget>() : current.targetTriggers)
            {
                hasTargets = true;
                var keyframeUI = prefabFactory.CreateSpacer();
                keyframeUI.height = 60f;
                var component = keyframeUI.gameObject.AddComponent<TriggersTargetFrame>();
                component.popupParent = popupParent;
                component.Bind(plugin, animation.current, target);
                _targets.Add(new TargetRef
                {
                    Component = component,
                    Target = target,
                });
            }

            foreach (var target in _filterJSON.val ? current.GetAllOrSelectedTargets().OfType<FreeControllerAnimationTarget>() : current.targetControllers)
            {
                hasTargets = true;
                var keyframeUI = prefabFactory.CreateSpacer();
                keyframeUI.height = 60f;
                var component = keyframeUI.gameObject.AddComponent<ControllerTargetFrame>();
                component.Bind(plugin, animation.current, target);
                _targets.Add(new TargetRef
                {
                    Component = component,
                    Target = target
                });
            }

            foreach (var target in _filterJSON.val ? current.GetAllOrSelectedTargets().OfType<FloatParamAnimationTarget>() : current.targetFloatParams)
            {
                hasTargets = true;
                var keyframeUI = prefabFactory.CreateSpacer();
                keyframeUI.height = 60f;
                var component = keyframeUI.gameObject.AddComponent<FloatParamTargetFrame>();
                component.Bind(plugin, animation.current, target);
                _targets.Add(new TargetRef
                {
                    Component = component,
                    Target = target,
                });
            }

            if (!hasTargets)
            {
                _noTargetsUI = CreateHeader("No targets", 2);
            }
            else
            {
                _spacerUI = prefabFactory.CreateSpacer();

                _filterUI = prefabFactory.CreateToggle(_filterJSON);
                _filterUI.backgroundColor = navButtonColor;
                var toggleColors = _filterUI.toggle.colors;
                toggleColors.normalColor = navButtonColor;
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
                _manageTargetsUI.buttonColor = navButtonColor;
        }

        public override void OnDestroy()
        {
            current.onTargetsSelectionChanged.RemoveListener(OnSelectionChanged);
            Destroy(_manageTargetsUI);
            RemoveTargets();
            base.OnDestroy();
        }

        private void RemoveTargets()
        {
            foreach (var targetRef in _targets)
            {
                Destroy(targetRef.Component.gameObject);
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

