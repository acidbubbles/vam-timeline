using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class EditScreen : ScreenBase
    {
        public const string ScreenName = "Edit";

        public override string screenId => ScreenName;

        private class TargetRef
        {
            public ITargetFrame Component;
            public IAnimationTargetWithCurves Target;
        }

        private readonly List<TargetRef> _targets = new List<TargetRef>();
        private bool _selectionChangedPending;
        private UIDynamicButton _manageTargetsUI;

        public EditScreen()
            : base()
        {

        }
        public override void Init(IAtomPlugin plugin)
        {
            base.Init(plugin);

            current.onTargetsSelectionChanged.AddListener(OnSelectionChanged);

            OnSelectionChanged();

            if (animation.IsEmpty()) InitExplanation();
        }

        private void InitExplanation()
        {
            var textJSON = new JSONStorableString("Help", HelpScreen.HelpText);
            var textUI = prefabFactory.CreateTextField(textJSON, true);
            textUI.height = 900;
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
            Destroy(_manageTargetsUI);
            var time = animation.clipTime;

            foreach (var target in current.GetAllOrSelectedTargets().OfType<FreeControllerAnimationTarget>())
            {
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

            foreach (var target in current.GetAllOrSelectedTargets().OfType<FloatParamAnimationTarget>())
            {
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
            _manageTargetsUI = CreateChangeScreenButton("<b>[+/-]</b> Add/Remove Targets", TargetsScreen.ScreenName, true);
            if (current.allTargetsCount == 0)
                _manageTargetsUI.buttonColor = new Color(0f, 1f, 0f);
            else
                _manageTargetsUI.buttonColor = new Color(0.8f, 0.7f, 0.8f);
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
                Destroy(targetRef.Component.Container);
            }
            _targets.Clear();
        }
    }
}

