namespace VamTimeline
{
    public abstract class AddScreenBase : ScreenBase
    {
        private static bool _previousCreateInOtherAtoms;

        protected JSONStorableString clipNameJSON;
        protected JSONStorableString layerNameJSON;
        protected JSONStorableString segmentNameJSON;
        protected JSONStorableBool createInOtherAtomsJSON;
        protected JSONStorableStringChooser createPositionJSON;

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
            prefabFactory.CreateTextInput(clipNameJSON);
        }

        protected void InitNewLayerNameUI()
        {
            layerNameJSON = new JSONStorableString("New layer name", "", (string _) => OptionsUpdated());
            prefabFactory.CreateTextInput(layerNameJSON);
        }

        protected void InitNewSegmentNameUI()
        {
            segmentNameJSON = new JSONStorableString("New segment name", "", (string _) => OptionsUpdated());
            prefabFactory.CreateTextInput(segmentNameJSON);
        }

        protected void InitCreateInOtherAtomsUI()
        {
            createInOtherAtomsJSON = new JSONStorableBool("Create in other atoms", _previousCreateInOtherAtoms, val => _previousCreateInOtherAtoms = val);
            prefabFactory.CreateToggle(createInOtherAtomsJSON);
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
