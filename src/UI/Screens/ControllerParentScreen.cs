using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class ControllerParentScreen : ScreenBase
    {
        public const string ScreenName = "Controller Parent";
        private JSONStorableStringChooser _atomJSON;
        private JSONStorableStringChooser _rigidbodyJSON;
        private FreeControllerAnimationTarget _target;

        public override string screenId => ScreenName;

        public ControllerParentScreen()
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
            _atomJSON = new JSONStorableStringChooser("Atom", SuperController.singleton.GetAtomUIDs(), "", "Atom", (string val) => SyncAtom());
            var atomUI = prefabFactory.CreatePopup(_atomJSON, true);
            atomUI.popupPanelHeight = 700f;
            _atomJSON.valNoCallback = _target.parentAtomId ?? plugin.containingAtom.uid;

            _rigidbodyJSON = new JSONStorableStringChooser("Rigidbody", new List<string>(), "", "Rigidbody", (string val) => SyncRigidbody());
            var rigidbodyUI = prefabFactory.CreatePopup(_rigidbodyJSON, true);
            atomUI.popupPanelHeight = 700f;
            _rigidbodyJSON.valNoCallback = _target.parentRigidbodyId ?? "";

            if (string.IsNullOrEmpty(_rigidbodyJSON.val) && !string.IsNullOrEmpty(_atomJSON.val))
                SyncAtom();
        }

        private void SyncAtom()
        {
            if (string.IsNullOrEmpty(_atomJSON.val))
            {
                _target.SetParent(null, null);
                _rigidbodyJSON.valNoCallback = "";
                return;
            }
            var atom = SuperController.singleton.GetAtomByUid(_atomJSON.val);
            var selfRigidbodyControl = _target.controller.GetComponent<Rigidbody>().name;
            var selfRigidbodyTarget = selfRigidbodyControl.EndsWith("Control") ? selfRigidbodyControl.Substring(0, selfRigidbodyControl.Length - "Control".Length) : null;
            _rigidbodyJSON.choices = atom.linkableRigidbodies
                .Select(rb => rb.name)
                .Where(n => n != selfRigidbodyControl && n != selfRigidbodyTarget)
                .ToList();
            _rigidbodyJSON.valNoCallback = "";
        }

        private void SyncRigidbody()
        {
            var previousParent = _target.GetParent();
            var previousParentPosition = previousParent.transform.position;
            var previousParentRotation = previousParent.transform.rotation;
            var snapshot = operations.Offset().Start(0f, new[] { _target });

            if (string.IsNullOrEmpty(_rigidbodyJSON.val))
                _target.SetParent(null, null);
            else
                _target.SetParent(_atomJSON.val, _rigidbodyJSON.val);

            var newParent = _target.GetParent();
            var newParentPosition = newParent.transform.position;
            var newParentRotation = newParent.transform.rotation;

            var positionOffset = newParentPosition - previousParentPosition;
            var rotationOffset = Quaternion.Inverse(previousParentRotation) * newParentRotation;

            var localPosition = _target.GetKeyframePosition(0);
            var localRotation = _target.GetKeyframeRotation(0);

            _target.SetKeyframe(0f, localPosition - positionOffset, Quaternion.Inverse(rotationOffset) * localRotation);

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

