using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class ControllerTargetSettingsScreen : ScreenBase
    {
        public const string ScreenName = "Controller settings";
        private static string _lastArg;
        private JSONStorableStringChooser _atomJSON;
        private JSONStorableStringChooser _rigidbodyJSON;
        private FreeControllerV3AnimationTarget _target;

        public override string screenId => ScreenName;

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            if (arg == null) arg = _lastArg; else _lastArg = (string)arg;
            _target = current.targetControllers.FirstOrDefault(t => t.name == (string)arg);

            CreateChangeScreenButton("<b><</b> <i>Back</i>", TargetsScreen.ScreenName);

            prefabFactory.CreateHeader("Controller settings", 1);

            if (_target == null)
            {
                prefabFactory.CreateTextField(new JSONStorableString("", "Cannot show the selected target settings.\nPlease go back and re-enter this screen."));
                return;
            }
            prefabFactory.CreateHeader(_target.name, 2);

            prefabFactory.CreateHeader("Parenting", 1);

            InitParentUI();

            prefabFactory.CreateHeader("Options", 1);

            InitControlUI();
            InitWeightUI();
        }

        private void InitParentUI()
        {
            _atomJSON = new JSONStorableStringChooser("Atom", new[] { "None" }.Concat(SuperController.singleton.GetAtomUIDs()).ToList(), "None", "Atom", (string val) => SyncAtom());
            var atomUI = prefabFactory.CreatePopup(_atomJSON, true, false);
            atomUI.popupPanelHeight = 700f;
            _atomJSON.valNoCallback = _target.parentAtomId ?? "None";

            _rigidbodyJSON = new JSONStorableStringChooser("Rigidbody", new List<string> { "None" }, "None", "Rigidbody", (string val) => SyncRigidbody());
            var rigidbodyUI = prefabFactory.CreatePopup(_rigidbodyJSON, true, false);
            rigidbodyUI.popupPanelHeight = 700f;
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
            var controlPosition = new JSONStorableBool("Control position", _target.controlPosition, val => _target.controlPosition = val);
            prefabFactory.CreateToggle(controlPosition);

            var controlRotation = new JSONStorableBool("Control rotation", _target.controlRotation, val => _target.controlRotation = val);
            prefabFactory.CreateToggle(controlRotation);
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

            var snapshot = operations.Offset().Start(0f, new[] { _target });

            _target.SetParent(parentAtomId, parentRigidbodyId);
            if (!_target.EnsureParentAvailable())
            {
                SuperController.LogError($"Timeline: Cannot automatically adjust from {_target.parentAtomId ?? "None"}/{_target.parentRigidbodyId ?? "None"} to {parentAtomId ?? "None"}/{parentRigidbodyId ?? "None"} because the current parent is not available.");
                return;
            }

            targetControllerTransform.position = previousPosition;
            targetControllerTransform.rotation = previousRotation;
            animationEditContext.SetKeyframeToCurrentTransform(_target, 0f);

            operations.Offset().Apply(snapshot, 0f, current.animationLength, OffsetOperations.ChangePivotMode);
        }

        protected override void OnCurrentAnimationChanged(AtomAnimationEditContext.CurrentAnimationChangedEventArgs args)
        {
            base.OnCurrentAnimationChanged(args);
            ChangeScreen(TargetsScreen.ScreenName);
        }
    }
}

