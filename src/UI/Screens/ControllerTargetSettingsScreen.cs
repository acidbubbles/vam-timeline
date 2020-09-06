using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class ControllerTargetSettingsScreen : ScreenBase
    {
        public const string ScreenName = "Controller Settings";
        private JSONStorableStringChooser _atomJSON;
        private JSONStorableStringChooser _rigidbodyJSON;
        private FreeControllerAnimationTarget _target;

        public override string screenId => ScreenName;

        public ControllerTargetSettingsScreen()
            : base()
        {
        }

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            _target = current.targetControllers.FirstOrDefault(t => t.name == (string)arg);

            if (_target == null)
            {
                prefabFactory.CreateTextField(new JSONStorableString("", "Please leave and re-enter this screen."));
                return;
            }

            CreateChangeScreenButton("<b><</b> <i>Back</i>", TargetsScreen.ScreenName);

            CreateHeader($"Parenting", 1);
            CreateHeader(_target.name, 2);

            InitParentUI();
        }

        private void InitParentUI()
        {
            _atomJSON = new JSONStorableStringChooser("Atom", new[] { "None" }.Concat(SuperController.singleton.GetAtomUIDs()).ToList(), "None", "Atom", (string val) => SyncAtom());
            var atomUI = prefabFactory.CreatePopup(_atomJSON, true, false);
            atomUI.popupPanelHeight = 700f;
            _atomJSON.valNoCallback = _target.parentAtomId ?? "None";

            _rigidbodyJSON = new JSONStorableStringChooser("Rigidbody", new List<string> { "None" }, "None", "Rigidbody", (string val) => SyncRigidbody());
            var rigidbodyUI = prefabFactory.CreatePopup(_rigidbodyJSON, true, false);
            atomUI.popupPanelHeight = 700f;
            _rigidbodyJSON.valNoCallback = _target.parentRigidbodyId ?? "None";

            PopulateRigidbodies();

            var parentWeight = new JSONStorableFloat("Weight", 1f, (float val) => _target.weight = val, 0f, 1f)
            {
                valNoCallback = _target.weight
            };
            prefabFactory.CreateSlider(parentWeight);
        }

        private void SyncAtom()
        {
            if (string.IsNullOrEmpty(_atomJSON.val) || _atomJSON.val == "None")
            {
                _target.SetParent(null, null);
                _rigidbodyJSON.valNoCallback = "None";
                return;
            }
            PopulateRigidbodies();
            _rigidbodyJSON.valNoCallback = "None";
        }

        private void PopulateRigidbodies()
        {
            if (string.IsNullOrEmpty(_atomJSON.val) || _atomJSON.val == "None")
            {
                _rigidbodyJSON.choices = new List<string> { "None" };
                return;
            }
            var atom = SuperController.singleton.GetAtomByUid(_atomJSON.val);
            var selfRigidbodyControl = _target.controller.GetComponent<Rigidbody>().name;
            var selfRigidbodyTarget = selfRigidbodyControl.EndsWith("Control") ? selfRigidbodyControl.Substring(0, selfRigidbodyControl.Length - "Control".Length) : null;
            var choices = atom.linkableRigidbodies
                .Select(rb => rb.name)
                .Where(n => n != selfRigidbodyControl && n != selfRigidbodyTarget)
                .ToList();
            choices.Insert(0, "None");
            _rigidbodyJSON.choices = choices;
        }

        private void SyncRigidbody()
        {
            var parentAtomId = string.IsNullOrEmpty(_atomJSON.val) || _atomJSON.val == "None" ? null : _atomJSON.val;
            var parentRigidbodyId = string.IsNullOrEmpty(_rigidbodyJSON.val) || _rigidbodyJSON.val == "None" ? null : _rigidbodyJSON.val;

            if (_target.parentAtomId == parentAtomId && _target.parentRigidbodyId == parentRigidbodyId) return;

            var previousParent = _target.GetParent();
            var previousParentPosition = previousParent.transform.position;
            var previousParentRotation = previousParent.transform.rotation;

            var snapshot = operations.Offset().Start(0f, new[] { _target });

            _target.SetParent(parentAtomId, parentRigidbodyId);
            if (!_target.EnsureParentAvailable()) return;

            var newParent = _target.GetParent();
            var newParentPosition = newParent.transform.position;
            var newParentRotation = newParent.transform.rotation;

            var positionDelta = newParentPosition - previousParentPosition;
            var rotationDelta = Quaternion.Inverse(previousParentRotation) * newParentRotation;

            var localPosition = _target.GetKeyframePosition(0);
            var localRotation = _target.GetKeyframeRotation(0);

            _target.SetKeyframe(0f, localPosition - positionDelta, rotationDelta * localRotation);

            operations.Offset().Apply(snapshot, 0f, current.animationLength, OffsetOperations.ChangePivotMode);
        }

        protected override void OnCurrentAnimationChanged(AtomAnimation.CurrentAnimationChangedEventArgs args)
        {
            base.OnCurrentAnimationChanged(args);
            ChangeScreen(TargetsScreen.ScreenName);
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
        }
    }
}

