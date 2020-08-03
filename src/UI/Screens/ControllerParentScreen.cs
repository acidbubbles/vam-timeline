using System;
using System.Collections.Generic;
using System.Linq;

namespace VamTimeline
{
    public class ControllerParentScreen : ScreenBase
    {
        public static FreeControllerAnimationTarget target;

        public const string ScreenName = "Controller Parent";
        private JSONStorableStringChooser _atomJSON;
        private JSONStorableStringChooser _rigidbodyJSON;

        public override string screenId => ScreenName;

        public ControllerParentScreen()
            : base()
        {
        }

        public override void Init(IAtomPlugin plugin)
        {
            base.Init(plugin);

            // Right side

            CreateChangeScreenButton("<b><</b> <i>Back</i>", TargetsScreen.ScreenName);

            prefabFactory.CreateSpacer();

            InitParentUI();
        }

        private void InitParentUI()
        {
            if (target == null || !current.targetControllers.Contains(target))
            {
                SuperController.LogError($"Target {target?.name ?? "(null)"} is not in the clip {current.animationName}");
                return;
            }

            _atomJSON = new JSONStorableStringChooser("Atom", SuperController.singleton.GetAtomUIDs(), "", "Atom", (string val) => SyncAtom());
            var atomUI = prefabFactory.CreateScrollablePopup(_atomJSON);
            atomUI.popupPanelHeight = 700f;
            _atomJSON.valNoCallback = target.parentAtomId ?? "";

            _rigidbodyJSON = new JSONStorableStringChooser("Rigidbody", new List<string>(), "", "Rigidbody", (string val) => SyncRigidbody());
            var rigidbodyUI = prefabFactory.CreateScrollablePopup(_rigidbodyJSON);
            atomUI.popupPanelHeight = 700f;
            _rigidbodyJSON.valNoCallback = target.parentRigidbodyId ?? "";
        }

        private void SyncAtom()
        {
            if (string.IsNullOrEmpty(_atomJSON.val))
            {
                target.SetParent(null, null);
                _rigidbodyJSON.valNoCallback = "";
                return;
            }
            var atom = SuperController.singleton.GetAtomByUid(_atomJSON.val);
            _rigidbodyJSON.choices = atom.linkableRigidbodies.Select(r => r.name).ToList();
            _rigidbodyJSON.valNoCallback = "";
        }

        private void SyncRigidbody()
        {
            if (string.IsNullOrEmpty(_rigidbodyJSON.val))
                target.SetParent(null, null);
            else
                target.SetParent(_atomJSON.val, _rigidbodyJSON.val);
        }

        protected override void OnCurrentAnimationChanged(AtomAnimation.CurrentAnimationChangedEventArgs args)
        {
            base.OnCurrentAnimationChanged(args);
            onScreenChangeRequested.Invoke(TargetsScreen.ScreenName);
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
        }
    }
}

