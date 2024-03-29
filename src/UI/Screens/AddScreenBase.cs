﻿namespace VamTimeline
{
    public abstract class AddScreenBase : ScreenBase
    {
        private static bool _previousCreateInOtherAtoms;
        private static bool _previousAddAnother;

        protected JSONStorableString clipNameJSON;
        protected JSONStorableString layerNameJSON;
        protected JSONStorableString segmentNameJSON;
        protected JSONStorableBool createInOtherAtomsJSON;
        protected JSONStorableBool addAnotherJSON;
        protected JSONStorableStringChooser createPositionJSON;
        protected UIDynamicTextField clipNameUI;

        #region Init

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            // Right side

            CreateChangeScreenButton($"<b><</b> <i>Back to {AddAnimationsScreen.ScreenName}</i>", AddAnimationsScreen.ScreenName);
        }

        protected void InitNewClipNameUI()
        {
            clipNameJSON = new JSONStorableString("New animation name", "", (string _) => OptionsUpdated());
            clipNameUI = prefabFactory.CreateTextInput(clipNameJSON);
        }

        protected void InitNewLayerNameUI(string label = "New layer name")
        {
            layerNameJSON = new JSONStorableString(label, "", (string _) => OptionsUpdated());
            prefabFactory.CreateTextInput(layerNameJSON);
        }

        protected void InitNewSegmentNameUI(string label = "New segment name")
        {
            segmentNameJSON = new JSONStorableString(label, "", (string _) => OptionsUpdated());
            prefabFactory.CreateTextInput(segmentNameJSON);
        }

        protected void InitCreateInOtherAtomsUI(string label = "Create in other atoms")
        {
            createInOtherAtomsJSON = new JSONStorableBool(label, _previousCreateInOtherAtoms, val => _previousCreateInOtherAtoms = val);
            prefabFactory.CreateToggle(createInOtherAtomsJSON);
        }

        protected void InitAddAnotherUI()
        {
            addAnotherJSON = new JSONStorableBool("Stay in this screen", _previousAddAnother, val => _previousAddAnother = val);
            prefabFactory.CreateToggle(addAnotherJSON);
        }

        protected void InitNewPositionUI()
        {
            createPositionJSON = new JSONStorableStringChooser(
                "Add at position",
                AddAnimationOperations.Positions.all,
                AddAnimationOperations.Positions.PositionNext,
                "Add at position");
            prefabFactory.CreatePopup(createPositionJSON, false, true);
        }

        #endregion

        protected override void OnCurrentAnimationChanged(AtomAnimationEditContext.CurrentAnimationChangedEventArgs args)
        {
            base.OnCurrentAnimationChanged(args);

            RefreshUI();
        }

        protected virtual void RefreshUI()
        {
            OptionsUpdated();
        }

        protected abstract void OptionsUpdated();
    }
}
