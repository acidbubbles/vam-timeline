using System;
using System.Collections;
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
    public class AtomAnimationAdvancedUI : AtomAnimationBaseUI
    {
        public const string ScreenName = "Advanced";
        private JSONStorableStringChooser _exportToJSON;

        public override string Name => ScreenName;

        public AtomAnimationAdvancedUI(IAtomPlugin plugin)
            : base(plugin)
        {

        }
        public override void Init()
        {
            base.Init();

            // Left side

            InitAnimationSelectorUI(false);

            InitPlaybackUI(false);

            InitFrameNavUI(false);

            var keyframeCurrentPoseUI = Plugin.CreateButton("Keyframe Current Pose (All)", true);
            keyframeCurrentPoseUI.button.onClick.AddListener(() => KeyframeCurrentPose(true));
            _components.Add(keyframeCurrentPoseUI);

            var keyframeCurrentPoseTrackedUI = Plugin.CreateButton("Keyframe Current Pose (Tracked)", true);
            keyframeCurrentPoseTrackedUI.button.onClick.AddListener(() => KeyframeCurrentPose(false));
            _components.Add(keyframeCurrentPoseTrackedUI);

            var bakeUI = Plugin.CreateButton("Bake Animation (Arm & Record)", true);
            bakeUI.button.onClick.AddListener(() => Bake());
            _components.Add(bakeUI);

            _exportToJSON = new JSONStorableStringChooser("Export To", SuperController.singleton.GetAtoms().Where(a => a != Plugin.ContainingAtom && a.type == Plugin.ContainingAtom.type && a.GetStorableIDs().Any(s => s.EndsWith("VamTimeline.AtomPlugin"))).Select(a => a.uid).ToList(), "", "Export To");
            var exportToUI = Plugin.CreateScrollablePopup(_exportToJSON, true);
            _linkedStorables.Add(_exportToJSON);

            var exportUI = Plugin.CreateButton("Export (All)", true);
            exportUI.button.onClick.AddListener(() => Export());
            _components.Add(exportUI);

            // TODO: Keyframe all animatable morphs

            // TODO: Copy all missing controllers and morphs on every animation

            // TODO: Import / Export animation(s) to another atom and create an atom just to store and share animations
        }

        private void Export()
        {
            if (string.IsNullOrEmpty(_exportToJSON.val)) return;
            var atom = SuperController.singleton.GetAtomByUid(_exportToJSON.val);
            var storableId = atom.GetStorableIDs().First(s => s.EndsWith("VamTimeline.AtomPlugin"));
            var storable = atom.GetStorableByID(storableId);
            var storageJSON = storable.GetStringJSONParam("Save");
            SuperController.LogMessage(Plugin.StorageJSON.val);
            storageJSON.val = Plugin.StorageJSON.val;
        }

        private void KeyframeCurrentPose(bool all)
        {
            try
            {
                var time = Plugin.Animation.Time;
                foreach (var fc in Plugin.ContainingAtom.freeControllers)
                {
                    if (!fc.name.EndsWith("Control")) continue;
                    if (fc.currentPositionState != FreeControllerV3.PositionState.On) continue;
                    if (fc.currentRotationState != FreeControllerV3.RotationState.On) continue;

                    var target = Plugin.Animation.Current.TargetControllers.FirstOrDefault(tc => tc.Controller == fc);
                    if (target == null)
                    {
                        if (!all) continue;
                        target = Plugin.Animation.Add(fc);
                    }
                    Plugin.Animation.SetKeyframeToCurrentTransform(target, time);
                }
                Plugin.Animation.RebuildAnimation();
                Plugin.AnimationModified();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.AtomAnimationAdvancedUI: " + exc.ToString());
            }
        }

        private void Bake()
        {
            var controllers = Plugin.Animation.Clips.SelectMany(c => c.TargetControllers).Select(c => c.Controller).Distinct().ToList();
            foreach (var mac in Plugin.ContainingAtom.motionAnimationControls)
            {
                if (!controllers.Contains(mac.controller)) continue;
                mac.armedForRecord = true;
            }

            Plugin.Animation.Play();
            SuperController.singleton.motionAnimationMaster.StartRecord();

            Plugin.StartCoroutine(StopWhenPlaybackIsComplete());
        }

        private IEnumerator StopWhenPlaybackIsComplete()
        {
            var waitFor = Plugin.Animation.Clips.Sum(c => c.NextAnimationTime == 0 ? c.AnimationLength : c.NextAnimationTime);
            yield return new WaitForSeconds(waitFor);

            SuperController.singleton.motionAnimationMaster.StopRecord();
            Plugin.Animation.Stop();
        }
    }
}

