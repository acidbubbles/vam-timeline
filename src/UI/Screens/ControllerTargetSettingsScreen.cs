using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class ControllerTargetSettingsScreen : ScreenBase
    {
        public const string ScreenName = "Controller settings";
        private static FreeControllerV3 _lastArg;
        private JSONStorableStringChooser _atomJSON;
        private JSONStorableStringChooser _rigidbodyJSON;
        private FreeControllerV3AnimationTarget _target;

        public override string screenId => ScreenName;

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            if (arg == null) arg = _lastArg; else _lastArg = (FreeControllerV3)arg;
            _target = current.targetControllers.FirstOrDefault(t => t.animatableRef.controller == (FreeControllerV3)arg);

            CreateChangeScreenButton("<b><</b> <i>Back</i>", TargetsScreen.ScreenName);

            prefabFactory.CreateHeader("Controller settings", 1);

            if (_target == null)
            {
                prefabFactory.CreateTextField(new JSONStorableString("", "Cannot show the selected target settings.\nPlease go back and re-enter this screen."));
                return;
            }
            prefabFactory.CreateHeader("Control: " + _target.name, 2);

            if (_target.targetsPosition && _target.targetsRotation)
            {
                prefabFactory.CreateHeader("Parenting", 1);

                InitParentUI();
            }

            prefabFactory.CreateHeader("Options", 1);

            InitControlUI();
            InitWeightUI();

            if (_target.targetsPosition && _target.targetsRotation && string.IsNullOrEmpty(_target.parentRigidbodyId))
            {
                prefabFactory.CreateHeader("Advanced", 1);

                InitSplitPosRotUI();
            }
        }

        private void InitParentUI()
        {
            _atomJSON = new JSONStorableStringChooser("Atom", new[] { "None" }.Concat(SuperController.singleton.GetAtomUIDs()).ToList(), "None", "Atom", (string val) => SyncAtom());
            prefabFactory.CreatePopup(_atomJSON, true, false, 700f);
            _atomJSON.valNoCallback = _target.parentAtomId ?? "None";

            _rigidbodyJSON = new JSONStorableStringChooser("Rigidbody", new List<string> { "None" }, "None", "Rigidbody", (string val) => SyncRigidbody());
            prefabFactory.CreatePopup(_rigidbodyJSON, true, false, 700f);
            _rigidbodyJSON.valNoCallback = _target.parentRigidbodyId ?? "None";

            PopulateRigidbodies();
        }

        private void InitWeightUI()
        {
            var parentWeight = new JSONStorableFloat("Weight", 1f, val => _target.weight = val, 0f, 1f)
            {
                valNoCallback = _target.weight
            };
            parentWeight.valNoCallback = _target.weight;
            prefabFactory.CreateSlider(parentWeight);
        }

        private void InitControlUI()
        {
            if (_target.targetsPosition)
            {
                var controlPosition = new JSONStorableBool("Position enabled", _target.controlPosition, val => _target.controlPosition = val);
                prefabFactory.CreateToggle(controlPosition);
            }
            if (_target.targetsRotation)
            {
                var controlRotation = new JSONStorableBool("Rotation enabled", _target.controlRotation, val => _target.controlRotation = val);
                prefabFactory.CreateToggle(controlRotation);
            }
        }

        private void InitSplitPosRotUI()
        {
            prefabFactory.CreateButton("Split Position & Rotation (BETA)").button.onClick.AddListener(() =>
            {
                var pos = _target;
                pos.targetsRotation = false;
                var rot = current.Add(pos.animatableRef, false, true);
                if (rot == null) throw new NullReferenceException("Could not add rotation controller");
                rot.rotation.rotX.keys = new List<BezierKeyframe>(pos.rotation.rotX.keys);
                rot.rotation.rotY.keys = new List<BezierKeyframe>(pos.rotation.rotY.keys);
                rot.rotation.rotZ.keys = new List<BezierKeyframe>(pos.rotation.rotZ.keys);
                rot.rotation.rotW.keys = new List<BezierKeyframe>(pos.rotation.rotW.keys);
                if (pos.rotation.rotX.length > 2)
                {
                    pos.rotation.rotX.keys.RemoveRange(1, pos.rotation.rotX.length - 2);
                    pos.curves.Remove(pos.rotation.rotX);
                    pos.rotation.rotY.keys.RemoveRange(1, pos.rotation.rotX.length - 2);
                    pos.curves.Remove(pos.rotation.rotY);
                    pos.rotation.rotZ.keys.RemoveRange(1, pos.rotation.rotX.length - 2);
                    pos.curves.Remove(pos.rotation.rotZ);
                    pos.rotation.rotW.keys.RemoveRange(1, pos.rotation.rotX.length - 2);
                    pos.curves.Remove(pos.rotation.rotW);
                }
            });
        }

        private void SyncAtom()
        {
            if (string.IsNullOrEmpty(_atomJSON.val) || _atomJSON.val == "None")
            {
                _rigidbodyJSON.val = "None";
                return;
            }
            PopulateRigidbodies();
            _rigidbodyJSON.val = "None";
        }

        private void PopulateRigidbodies()
        {
            if (string.IsNullOrEmpty(_atomJSON.val) || _atomJSON.val == "None")
            {
                _rigidbodyJSON.choices = new List<string> { "None" };
                return;
            }
            var atom = SuperController.singleton.GetAtomByUid(_atomJSON.val);
            if(atom == null)
            {
                _rigidbodyJSON.choices = new List<string> { "None" };
                return;
            }
            var selfRigidbodyControl = _target.animatableRef.controller.GetComponent<Rigidbody>().name;
            var selfRigidbodyTarget = selfRigidbodyControl.EndsWith("Control") ? selfRigidbodyControl.Substring(0, selfRigidbodyControl.Length - "Control".Length) : null;
            var choices = atom.linkableRigidbodies
                .Select(rb => rb.name)
                .Where(n => atom != plugin.containingAtom || n != selfRigidbodyControl && n != selfRigidbodyTarget)
                .ToList();
            choices.Insert(0, "None");
            _rigidbodyJSON.choices = choices;
        }

        private void SyncRigidbody()
        {
            if (!animationEditContext.CanEdit())
            {
                _atomJSON.valNoCallback = _target.parentAtomId ?? "None";
                _rigidbodyJSON.valNoCallback = _target.parentRigidbodyId ?? "None";
                return;
            }

            var parentAtomId = string.IsNullOrEmpty(_atomJSON.val) || _atomJSON.val == "None" ? null : _atomJSON.val;
            var parentRigidbodyId = string.IsNullOrEmpty(_rigidbodyJSON.val) || _rigidbodyJSON.val == "None" ? null : _rigidbodyJSON.val;

            if (_target.parentRigidbodyId == null && parentRigidbodyId == null) return;
            if (_target.parentAtomId == parentAtomId && _target.parentRigidbodyId == parentRigidbodyId) return;

            animationEditContext.clipTime = 0f;

            var targetControllerTransform = _target.animatableRef.controller.transform;
            var previousPosition = targetControllerTransform.position;
            var previousRotation = targetControllerTransform.rotation;

            var offset = operations.Offset();

            var snapshot = offset.Start(0f, new[] { _target }, null, OffsetOperations.ChangePivotMode);

            _target.SetParent(parentAtomId, parentRigidbodyId);
            if (!_target.EnsureParentAvailable())
            {
                SuperController.LogError($"Timeline: Cannot automatically adjust from {_target.parentAtomId ?? "None"}/{_target.parentRigidbodyId ?? "None"} to {parentAtomId ?? "None"}/{parentRigidbodyId ?? "None"} because the current parent is not available.");
                return;
            }

            targetControllerTransform.position = previousPosition;
            targetControllerTransform.rotation = previousRotation;
            animationEditContext.SetKeyframeToCurrentTransform(_target, 0f);

            offset.Apply(snapshot, 0f, current.animationLength, OffsetOperations.ChangePivotMode);
        }

        protected override void OnCurrentAnimationChanged(AtomAnimationEditContext.CurrentAnimationChangedEventArgs args)
        {
            base.OnCurrentAnimationChanged(args);
            ChangeScreen(TargetsScreen.ScreenName);
        }
    }
}

