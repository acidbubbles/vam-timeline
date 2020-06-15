using System;
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
    public class AddAnimationScreen : ScreenBase
    {
        public const string ScreenName = "Add Animation";
        private UIDynamicButton _addAnimationTransitionUI;

        public override string name => ScreenName;

        public AddAnimationScreen(IAtomPlugin plugin)
            : base(plugin)
        {

        }

        #region Init

        public override void Init()
        {
            base.Init();

            // Right side

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName, true);

            CreateSpacer(true);

            InitCreateAnimationUI(true);

            CreateSpacer(true);

            InitCreateLayerUI(true);

            CreateSpacer(true);

            CreateChangeScreenButton("<i><b>Edit</b> animation settings...</i>", EditAnimationScreen.ScreenName, true);
            CreateChangeScreenButton("<i><b>Reorder</b> and <b>delete</b> animations...</i>", ManageAnimationsScreen.ScreenName, true);
            CreateChangeScreenButton("<i><b>Sequence</b> animations...</i>", EditSequenceScreen.ScreenName, true);
        }

        private void InitCreateAnimationUI(bool rightSide)
        {
            var addAnimationFromCurrentFrameUI = plugin.CreateButton("Create Animation From Current Frame", rightSide);
            addAnimationFromCurrentFrameUI.button.onClick.AddListener(() => AddAnimationFromCurrentFrame());
            RegisterComponent(addAnimationFromCurrentFrameUI);

            var addAnimationAsCopyUI = plugin.CreateButton("Create Copy Of Current Animation", rightSide);
            addAnimationAsCopyUI.button.onClick.AddListener(() => AddAnimationAsCopy());
            RegisterComponent(addAnimationAsCopyUI);

            _addAnimationTransitionUI = plugin.CreateButton($"Create Transition (Current -> Next)", rightSide);
            _addAnimationTransitionUI.button.onClick.AddListener(() => AddTransitionAnimation());
            RegisterComponent(_addAnimationTransitionUI);

            RefreshButtons();
        }

        public void InitCreateLayerUI(bool rightSide)
        {
            var createLayerUI = plugin.CreateButton("Create New Layer", rightSide);
            createLayerUI.button.onClick.AddListener(() => AddLayer());
            RegisterComponent(createLayerUI);
        }

        #endregion

        #region Callbacks

        private void AddAnimationAsCopy()
        {
            var clip = animation.AddAnimation(current.animationLayer);
            clip.loop = current.loop;
            clip.nextAnimationName = current.nextAnimationName;
            clip.nextAnimationTime = current.nextAnimationTime;
            clip.ensureQuaternionContinuity = current.ensureQuaternionContinuity;
            clip.blendDuration = current.blendDuration;
            clip.CropOrExtendLengthEnd(current.animationLength);
            foreach (var origTarget in current.targetControllers)
            {
                var newTarget = clip.Add(origTarget.controller);
                for (var i = 0; i < origTarget.curves.Count; i++)
                {
                    newTarget.curves[i].keys = origTarget.curves[i].keys.ToArray();
                }
                foreach (var kvp in origTarget.settings)
                {
                    newTarget.settings[kvp.Key] = new KeyframeSettings { curveType = kvp.Value.curveType };
                }
                newTarget.dirty = true;
            }
            foreach (var origTarget in current.targetFloatParams)
            {
                var newTarget = clip.Add(origTarget.storable, origTarget.floatParam);
                newTarget.value.keys = origTarget.value.keys.ToArray();
                newTarget.dirty = true;
            }

            animation.SelectAnimation(clip.animationName);
            onScreenChangeRequested.Invoke(EditScreen.ScreenName);
        }

        private void AddAnimationFromCurrentFrame()
        {
            var clip = animation.AddAnimation(current.animationLayer);
            clip.loop = current.loop;
            clip.nextAnimationName = current.nextAnimationName;
            clip.nextAnimationTime = current.nextAnimationTime;
            clip.ensureQuaternionContinuity = current.ensureQuaternionContinuity;
            clip.blendDuration = current.blendDuration;
            clip.CropOrExtendLengthEnd(current.animationLength);
            foreach (var origTarget in current.targetControllers)
            {
                var newTarget = clip.Add(origTarget.controller);
                newTarget.SetKeyframeToCurrentTransform(0f);
                newTarget.SetKeyframeToCurrentTransform(clip.animationLength);
            }
            foreach (var origTarget in current.targetFloatParams)
            {
                var newTarget = clip.Add(origTarget.storable, origTarget.floatParam);
                newTarget.SetKeyframe(0f, origTarget.floatParam.val);
                newTarget.SetKeyframe(clip.animationLength, origTarget.floatParam.val);
            }

            animation.SelectAnimation(clip.animationName);
            onScreenChangeRequested.Invoke(EditScreen.ScreenName);
        }

        private void AddTransitionAnimation()
        {
            var next = animation.GetClip(current.nextAnimationName);
            if (next == null)
            {
                SuperController.LogError("There is no animation to transition to");
                return;
            }

            var clip = animation.AddAnimation(current.animationLayer);
            clip.animationName = $"{current.animationName} > {next.animationName}";
            clip.loop = false;
            clip.transition = true;
            clip.nextAnimationName = current.nextAnimationName;
            clip.blendDuration = AtomAnimationClip.DefaultBlendDuration;
            clip.nextAnimationTime = clip.animationLength - clip.blendDuration;
            clip.ensureQuaternionContinuity = current.ensureQuaternionContinuity;

            foreach (var origTarget in current.targetControllers)
            {
                var newTarget = clip.Add(origTarget.controller);
                newTarget.SetKeyframe(0f, Vector3.zero, Quaternion.identity);
                newTarget.SetCurveSnapshot(0f, origTarget.GetCurveSnapshot(current.animationLength));
                newTarget.SetKeyframe(clip.animationLength, Vector3.zero, Quaternion.identity);
                newTarget.SetCurveSnapshot(clip.animationLength, next.targetControllers.First(t => t.controller == origTarget.controller).GetCurveSnapshot(0f));
            }
            foreach (var origTarget in current.targetFloatParams)
            {
                var newTarget = clip.Add(origTarget.storable, origTarget.floatParam);
                newTarget.SetKeyframe(0f, origTarget.value.Evaluate(current.animationLength));
                newTarget.SetKeyframe(clip.animationLength, next.targetFloatParams.First(t => ReferenceEquals(t.floatParam, origTarget.floatParam)).value.Evaluate(0f));
            }

            animation.clips.Remove(clip);
            animation.clips.Insert(animation.clips.IndexOf(current) + 1, clip);

            current.nextAnimationName = clip.animationName;

            animation.SelectAnimation(clip.animationName);
            onScreenChangeRequested.Invoke(EditScreen.ScreenName);
        }

        private void AddLayer()
        {
            var clip = animation.AddAnimation(GetNewLayerName());

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

        #endregion

        #region Events

        protected override void OnCurrentAnimationChanged(AtomAnimation.CurrentAnimationChangedEventArgs args)
        {
            base.OnCurrentAnimationChanged(args);

            RefreshButtons();
        }

        private void RefreshButtons()
        {
            bool hasNext = current.nextAnimationName != null;
            bool nextIsTransition = hasNext && animation.GetClip(current.nextAnimationName).transition;
            _addAnimationTransitionUI.button.interactable = hasNext && !nextIsTransition;
            if (!hasNext)
                _addAnimationTransitionUI.label = $"Create Transition (No sequence)";
            else if (nextIsTransition)
                _addAnimationTransitionUI.label = $"Create Transition (Already transition)";
            else
                _addAnimationTransitionUI.label = $"Create Transition (Current -> Next)";
        }

        #endregion
    }
}

