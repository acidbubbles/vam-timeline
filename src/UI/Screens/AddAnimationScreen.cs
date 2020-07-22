using System;
using System.Linq;

namespace VamTimeline
{
    public class AddAnimationScreen : ScreenBase
    {
        public const string ScreenName = "Add Animation";
        private UIDynamicButton _addAnimationTransitionUI;

        public override string screenId => ScreenName;

        public AddAnimationScreen()
            : base()
        {

        }

        #region Init

        public override void Init(IAtomPlugin plugin)
        {
            base.Init(plugin);

            // Right side

            CreateChangeScreenButton($"<b><</b> <i>Back to {AnimationsScreen.ScreenName}</i>", AnimationsScreen.ScreenName);

            CreateHeader("Add animations", 1);

            prefabFactory.CreateSpacer();

            CreateHeader("Animations", 2);

            InitCreateAnimationUI();

            prefabFactory.CreateSpacer();

            CreateHeader("Layers", 2);

            InitCreateLayerUI();
            InitSplitLayerUI();

            prefabFactory.CreateSpacer();

            CreateHeader("More", 2);

            CreateChangeScreenButton("<i><b>Import</b> from file...</i>", ImportExportScreen.ScreenName);
            CreateChangeScreenButton("<i><b>Manage</b> animations list...</i>", ManageAnimationsScreen.ScreenName);
        }

        private void InitCreateAnimationUI()
        {
            var addAnimationFromCurrentFrameUI = prefabFactory.CreateButton("Create Animation From Current Frame");
            addAnimationFromCurrentFrameUI.button.onClick.AddListener(() => AddAnimationFromCurrentFrame());

            var addAnimationAsCopyUI = prefabFactory.CreateButton("Create Copy Of Current Animation");
            addAnimationAsCopyUI.button.onClick.AddListener(() => AddAnimationAsCopy());

            _addAnimationTransitionUI = prefabFactory.CreateButton($"Create Transition (Current -> Next)");
            _addAnimationTransitionUI.button.onClick.AddListener(() => AddTransitionAnimation());

            RefreshButtons();
        }

        public void InitCreateLayerUI()
        {
            var createLayerUI = prefabFactory.CreateButton("Create New Layer");
            createLayerUI.button.onClick.AddListener(() => AddLayer());
        }

        private void InitSplitLayerUI()
        {
            var splitLayerUI = prefabFactory.CreateButton("Split selection to new layer");
            splitLayerUI.button.onClick.AddListener(() => SplitLayer());
        }

        #endregion

        #region Callbacks

        private void AddAnimationAsCopy()
        {
            var clip = animation.CreateClip(current.animationLayer);
            clip.loop = current.loop;
            clip.animationLength = current.animationLength;
            clip.nextAnimationName = current.nextAnimationName;
            clip.nextAnimationTime = current.nextAnimationTime;
            clip.ensureQuaternionContinuity = current.ensureQuaternionContinuity;
            clip.blendDuration = current.blendDuration;
            operations.Resize().CropOrExtendEnd(current.animationLength);
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
                var newTarget = clip.Add(new FloatParamAnimationTarget(plugin.containingAtom, origTarget.storableId, origTarget.floatParamName));
                newTarget.value.keys = origTarget.value.keys.ToArray();
                foreach (var kvp in origTarget.settings)
                {
                    newTarget.settings[kvp.Key] = new KeyframeSettings { curveType = kvp.Value.curveType };
                }
                newTarget.dirty = true;
            }
            foreach (var origTarget in current.targetTriggers)
            {
                var newTarget = clip.Add(new TriggersAnimationTarget { name = origTarget.name });
                foreach (var origTrigger in origTarget.triggersMap)
                {
                    var trigger = new AtomAnimationTrigger();
                    trigger.RestoreFromJSON(origTrigger.Value.GetJSON());
                    newTarget.SetKeyframe(origTrigger.Key, trigger);
                }
                newTarget.dirty = true;
            }

            animation.SelectAnimation(clip.animationName);
            onScreenChangeRequested.Invoke(EditAnimationScreen.ScreenName);
        }

        private void AddAnimationFromCurrentFrame()
        {
            var clip = animation.CreateClip(current.animationLayer);
            clip.loop = current.loop;
            clip.nextAnimationName = current.nextAnimationName;
            clip.nextAnimationTime = current.nextAnimationTime;
            clip.ensureQuaternionContinuity = current.ensureQuaternionContinuity;
            clip.blendDuration = current.blendDuration;
            operations.Resize().CropOrExtendEnd(current.animationLength);
            foreach (var origTarget in current.targetControllers)
            {
                var newTarget = clip.Add(origTarget.controller);
                newTarget.SetKeyframeToCurrentTransform(0f);
                newTarget.SetKeyframeToCurrentTransform(clip.animationLength);
            }
            foreach (var origTarget in current.targetFloatParams)
            {
                if (!origTarget.EnsureAvailable(false)) continue;
                var newTarget = clip.Add(origTarget.storable, origTarget.floatParam);
                newTarget.SetKeyframe(0f, origTarget.floatParam.val);
                newTarget.SetKeyframe(clip.animationLength, origTarget.floatParam.val);
            }

            animation.SelectAnimation(clip.animationName);
            onScreenChangeRequested.Invoke(EditAnimationScreen.ScreenName);
        }

        private void AddTransitionAnimation()
        {
            var next = animation.GetClip(current.nextAnimationName);
            if (next == null)
            {
                SuperController.LogError("There is no animation to transition to");
                return;
            }

            var clip = animation.CreateClip(current.animationLayer);
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
                newTarget.SetCurveSnapshot(0f, origTarget.GetCurveSnapshot(current.animationLength));
                newTarget.SetCurveSnapshot(clip.animationLength, next.targetControllers.First(t => t.TargetsSameAs(origTarget)).GetCurveSnapshot(0f));
            }
            foreach (var origTarget in current.targetFloatParams)
            {
                var newTarget = clip.Add(origTarget.storable, origTarget.floatParam);
                newTarget.SetCurveSnapshot(0f, origTarget.GetCurveSnapshot(current.animationLength));
                newTarget.SetCurveSnapshot(clip.animationLength, next.targetFloatParams.First(t => t.TargetsSameAs(origTarget)).GetCurveSnapshot(0f));
            }

            animation.clips.Remove(clip);
            animation.clips.Insert(animation.clips.IndexOf(current) + 1, clip);

            current.nextAnimationName = clip.animationName;

            animation.SelectAnimation(clip.animationName);
            onScreenChangeRequested.Invoke(EditAnimationScreen.ScreenName);
        }

        private void AddLayer()
        {
            var clip = animation.CreateClip(GetNewLayerName());

            animation.SelectAnimation(clip.animationName);
            onScreenChangeRequested.Invoke(EditAnimationScreen.ScreenName);
        }

        private void SplitLayer()
        {
            var targets = current.GetSelectedTargets().ToList();
            if (targets.Count == 0)
            {
                SuperController.LogError("VamTimeline: You must select a subset of targets to split to another layer.");
                return;
            }

            var newLayerName = GetNewLayerName();
            foreach (var clip in animation.clips.Where(c => c.animationLayer == current.animationLayer).ToList())
            {
                var targetsToMove = clip.GetAllTargets().Where(t => targets.Any(t2 => t2.TargetsSameAs(t))).ToList();

                foreach (var t in targetsToMove)
                    clip.Remove(t);

                var newClip = animation.CreateClip(newLayerName);
                newClip.animationLength = clip.animationLength;
                newClip.blendDuration = clip.blendDuration;
                newClip.nextAnimationName = clip.nextAnimationName;
                newClip.nextAnimationTime = clip.nextAnimationTime;
                newClip.animationName = GetSplitAnimationName(clip.animationName);

                foreach (var m in targetsToMove)
                    newClip.Add(m);
            }
        }

        private string GetSplitAnimationName(string animationName)
        {
            for (var i = 1; i < 999; i++)
            {
                var newName = $"{animationName} (Split {i})";
                if (!animation.clips.Any(c => c.animationName == newName)) return newName;
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

