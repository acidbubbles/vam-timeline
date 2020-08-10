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
        private FreeControllerAnimationTarget _target;

        public override string screenId => ScreenName;

        public ControllerParentScreen()
            : base()
        {
        }

        public override void Init(IAtomPlugin plugin)
        {
            _target = target;
            target = null;

            base.Init(plugin);

            if(_target == null)
            {
                return;
            }

            CreateChangeScreenButton("<b><</b> <i>Back</i>", TargetsScreen.ScreenName);

            prefabFactory.CreateSpacer();

            InitParentUI();
        }

        private void InitParentUI()
        {
            if (!current.targetControllers.Contains(_target))
            {
                SuperController.LogError($"Target {_target?.name ?? "(null)"} is not in the clip {current.animationName}");
                return;
            }

            _atomJSON = new JSONStorableStringChooser("Atom", SuperController.singleton.GetAtomUIDs(), "", "Atom", (string val) => SyncAtom());
            var atomUI = prefabFactory.CreatePopup(_atomJSON, true);
            atomUI.popupPanelHeight = 700f;
            _atomJSON.valNoCallback = _target.parentAtomId ?? "";

            _rigidbodyJSON = new JSONStorableStringChooser("Rigidbody", new List<string>(), "", "Rigidbody", (string val) => SyncRigidbody());
            var rigidbodyUI = prefabFactory.CreatePopup(_rigidbodyJSON, true);
            atomUI.popupPanelHeight = 700f;
            _rigidbodyJSON.valNoCallback = _target.parentRigidbodyId ?? "";
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
            _rigidbodyJSON.choices = atom.linkableRigidbodies.Select(r => r.name).ToList();
            _rigidbodyJSON.valNoCallback = "";
        }

        private void SyncRigidbody()
        {
            // TODO: Recalculate animation
            if (string.IsNullOrEmpty(_rigidbodyJSON.val))
                _target.SetParent(null, null);
            else
                _target.SetParent(_atomJSON.val, _rigidbodyJSON.val);
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

