using System.Collections.Generic;
using System.Linq;

namespace VamTimeline
{
    public class ReduceScreen : ScreenBase
    {
        public const string ScreenName = "Reduce";

        private static List<ICurveAnimationTarget> _backup;

        public override string screenId => ScreenName;

        private UIDynamicButton _backupUI;
        private UIDynamicButton _restoreUI;
        private UIDynamicButton _reduceUI;

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName);

            prefabFactory.CreateSpacer();

            _backupUI = prefabFactory.CreateButton("Backup");
            _backupUI.button.onClick.AddListener(TakeBackup);
            _restoreUI = prefabFactory.CreateButton("Restore");
            _restoreUI.button.onClick.AddListener(RestoreBackup);

            prefabFactory.CreateSpacer();

            _reduceUI = prefabFactory.CreateButton("Reduce");
            _reduceUI.button.onClick.AddListener(Reduce);

            animationEditContext.onTargetsSelectionChanged.AddListener(OnTargetsSelectionChanged);
            OnTargetsSelectionChanged();
            _restoreUI.button.interactable = _backup != null;
        }

        private void OnTargetsSelectionChanged()
        {
            var hasSelectedTargets = animationEditContext.GetSelectedTargets().Any();
            _backupUI.button.interactable = hasSelectedTargets;
            _reduceUI.button.interactable = hasSelectedTargets;
        }

        protected override void OnCurrentAnimationChanged(AtomAnimationEditContext.CurrentAnimationChangedEventArgs args)
        {
            base.OnCurrentAnimationChanged(args);
            _backup = null;
            _restoreUI.button.interactable = false;
        }

        public override void OnDestroy()
        {
            animationEditContext.onTargetsSelectionChanged.RemoveListener(OnTargetsSelectionChanged);
            base.OnDestroy();
        }

        private void TakeBackup()
        {
            _backup = animationEditContext.GetAllOrSelectedTargets().OfType<ICurveAnimationTarget>().Select(t => t.Clone(true)).ToList();
            _restoreUI.button.interactable = true;
        }

        private void RestoreBackup()
        {
            if (_backup == null) return;
            var targets = animationEditContext.GetAllOrSelectedTargets().OfType<ICurveAnimationTarget>().ToList();
            foreach (var backup in _backup)
            {
                var target = targets.FirstOrDefault(t => t.TargetsSameAs(backup));
                target?.RestoreFrom(backup);
            }
        }

        private void Reduce()
        {
            // TODO: Also do this for controllers
            StartCoroutine(operations.Reduce().ReduceKeyframes(
                animationEditContext.GetAllOrSelectedTargets().OfType<ICurveAnimationTarget>().ToList())
            );
        }
    }
}

