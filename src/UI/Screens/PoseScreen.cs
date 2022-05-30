using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace VamTimeline
{
    public class PoseScreen : ScreenBase
    {
        public const string ScreenName = "Pose";

        private static AtomPose _poseClipboard;

        private JSONStorableString _poseStateJSON;
        private UIDynamicButton _savePoseUI;
        private UIDynamicButton _applyPoseUI;
        private UIDynamicButton _clearPoseUI;
        private JSONStorableBool _applyPoseOnTransition;
        private JSONStorableBool _savePoseIncludeRoot;
        private JSONStorableBool _savePoseIncludePose;
        private JSONStorableBool _savePoseIncludeMorphs;
        private JSONStorableBool _savePoseUseMergeLoad;
        private UIDynamicButton _copyPoseUI;
        private UIDynamicButton _pastePoseUI;
        private UIDynamicTextField _poseStateUI;

        public override string screenId => ScreenName;

        #region Init

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            InitHelp();

            prefabFactory.CreateSpacer();

            InitPoseStateUI();

            if (plugin.containingAtom.type == "Person")
            {
                InitPoseUI();
            }

            current.onAnimationSettingsChanged.AddListener(OnAnimationSettingsChanged);
            OnAnimationSettingsChanged();
        }

        private void InitHelp()
        {
            var helpJSON = new JSONStorableString("Help", @"Poses will save the state of <b>all</b> controls (on/off, physics settings, position).

The will automatically apply when selecting the animation or pressing Stop twice.

The pose will be applied instantly, avoiding 'physics explosions'. Whenever a pose is present, blending will automatically be set to 0 seconds. Keep in mind that poses are per animation, not per keyframe.

You can also use poses for animations by using 'Apply pose on transition'. This will apply the pose when switching to this animation using triggers or sequencing.");
            prefabFactory.CreateTextField(helpJSON);
        }

        private void InitPoseStateUI()
        {
            _poseStateJSON = new JSONStorableString("Pose State", "");
            _poseStateUI = prefabFactory.CreateTextField(_poseStateJSON);
            _poseStateUI.GetComponent<LayoutElement>().minHeight = 38f;
            _poseStateUI.UItext.alignment = TextAnchor.MiddleCenter;
            _poseStateUI.height = 40f;
        }

        private void InitPoseUI()
        {
            _savePoseUI = prefabFactory.CreateButton("Save pose");
            _savePoseUI.button.onClick.AddListener(() =>
            {
                current.pose = AtomPose.FromAtom(plugin.containingAtom, _savePoseIncludeRoot.val, _savePoseIncludePose.val, _savePoseIncludeMorphs.val, _savePoseUseMergeLoad.val);
            });

            prefabFactory.CreateSpacer();

            _savePoseIncludeRoot = new JSONStorableBool("Pose: Include Root", AtomPose.DefaultIncludeRoot, (bool val) => _savePoseUI.buttonColor = Color.yellow);
            prefabFactory.CreateToggle(_savePoseIncludeRoot);
            _savePoseIncludePose = new JSONStorableBool("Pose: Include Bones & Controls", AtomPose.DefaultIncludePose, (bool val) => _savePoseUI.buttonColor = Color.yellow);
            prefabFactory.CreateToggle(_savePoseIncludePose);
            _savePoseIncludeMorphs = new JSONStorableBool("Pose: Include Pose Morphs", AtomPose.DefaultIncludeMorphs, (bool val) => _savePoseUI.buttonColor = Color.yellow);
            prefabFactory.CreateToggle(_savePoseIncludeMorphs);
            _savePoseUseMergeLoad = new JSONStorableBool("Pose: Use Merge Load", AtomPose.DefaultUseMergeLoad, (bool val) => _savePoseUI.buttonColor = Color.yellow);
            prefabFactory.CreateToggle(_savePoseUseMergeLoad);

            prefabFactory.CreateSpacer();

            _applyPoseUI = prefabFactory.CreateButton("Apply saved pose");
            _applyPoseUI.button.onClick.AddListener(() => current.pose.Apply());
            _clearPoseUI = prefabFactory.CreateButton("Clear saved pose");
            _clearPoseUI.button.onClick.AddListener(() => { current.pose = null; current.applyPoseOnTransition = false; });
            _copyPoseUI = prefabFactory.CreateButton("Copy current pose");
            _copyPoseUI.button.onClick.AddListener(() => { _poseClipboard = AtomPose.FromAtom(plugin.containingAtom, _savePoseIncludeRoot.val, _savePoseIncludePose.val, _savePoseIncludeMorphs.val, _savePoseUseMergeLoad.val); _pastePoseUI.button.interactable = true; });
            _pastePoseUI = prefabFactory.CreateButton("Paste current pose");
            _pastePoseUI.button.onClick.AddListener(() =>
            {
                if (_poseClipboard == null) return;
                current.pose = _poseClipboard.Clone();
                current.pose.Apply();
            });
            _pastePoseUI.button.interactable = _poseClipboard != null;

            prefabFactory.CreateSpacer();

            _applyPoseOnTransition = new JSONStorableBool("Apply pose on transition", false, v => current.applyPoseOnTransition = v);
            prefabFactory.CreateToggle(_applyPoseOnTransition);
        }

        #endregion

        #region Callbacks

        #endregion

        #region Events

        protected override void OnCurrentAnimationChanged(AtomAnimationEditContext.CurrentAnimationChangedEventArgs args)
        {
            base.OnCurrentAnimationChanged(args);

            args.before.onAnimationSettingsChanged.RemoveListener(OnAnimationSettingsChanged);
            args.after.onAnimationSettingsChanged.AddListener(OnAnimationSettingsChanged);

            OnAnimationSettingsChanged();
        }

        private void OnAnimationSettingsChanged(string _)
        {
            OnAnimationSettingsChanged();
        }

        private void OnAnimationSettingsChanged()
        {
            if (_applyPoseOnTransition != null)
            {
                _applyPoseOnTransition.valNoCallback = current.applyPoseOnTransition;
                _applyPoseOnTransition.toggle.interactable = current.pose != null;
            }
            if (plugin.containingAtom.type == "Person")
            {
                if (current.pose == null)
                {
                    _savePoseUI.label = "Use current pose";
                    _savePoseUI.buttonColor = _copyPoseUI.buttonColor;
                    var poseClip = animation.index.ByName(current.animationSegmentId, current.animationNameId).FirstOrDefault(c => c.pose != null);
                    if (poseClip != null)
                    {
                        _poseStateJSON.val = $"Pose in layer '{poseClip.animationLayer}'";
                        _poseStateUI.backgroundColor = new Color(0.8f, 1f, 0.8f);
                    }
                    else
                    {
                        poseClip = animation.index.useSegment ? animation.GetDefaultClipsPerLayer(current, false).FirstOrDefault(c => c.pose != null) : null;
                        if (poseClip != null)
                        {
                            _poseStateJSON.val = $"Pose in '{poseClip.animationName}'";
                            _poseStateUI.backgroundColor = new Color(0.8f, 0.8f, 1f);
                        }
                        else
                        {
                            _poseStateJSON.val = "No pose saved";
                            _poseStateUI.backgroundColor = Color.grey;
                        }
                    }
                }
                else
                {
                    _savePoseUI.label = "Overwrite pose";
                    _savePoseUI.buttonColor = new Color(0.8f, 0.8f, 0.5f);
                    _poseStateJSON.val = "<b>Pose saved</b>";
                    _poseStateUI.backgroundColor = Color.green;
                }

                _applyPoseUI.button.interactable = current.pose != null;
                _clearPoseUI.button.interactable = current.pose != null;
                if (current.pose == null)
                {
                    _savePoseIncludeRoot.valNoCallback = AtomPose.DefaultIncludeRoot;
                    _savePoseIncludePose.valNoCallback = AtomPose.DefaultIncludePose;
                    _savePoseIncludeMorphs.valNoCallback = AtomPose.DefaultIncludeMorphs;
                    _savePoseUseMergeLoad.valNoCallback = AtomPose.DefaultUseMergeLoad;
                }
                else
                {
                    _savePoseIncludeRoot.valNoCallback = current.pose.includeRoot;
                    _savePoseIncludePose.valNoCallback = current.pose.includePose;
                    _savePoseIncludeMorphs.valNoCallback = current.pose.includeMorphs;
                    _savePoseUseMergeLoad.valNoCallback = current.pose.useMergeLoad;
                }
            }
            else
            {
                _poseStateJSON.val = "Only Person atoms support poses";
            }
        }

        public override void OnDestroy()
        {
            current.onAnimationSettingsChanged.RemoveListener(OnAnimationSettingsChanged);
            base.OnDestroy();
        }

        #endregion
    }
}
