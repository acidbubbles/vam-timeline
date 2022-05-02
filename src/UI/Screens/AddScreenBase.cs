namespace VamTimeline
{
    public abstract class AddScreenBase : ScreenBase
    {
        private static bool _previousCreateInOtherAtoms;

        protected JSONStorableString clipNameJSON;
        protected JSONStorableString layerNameJSON;
        protected JSONStorableString segmentNameJSON;
        protected JSONStorableBool createInOtherAtoms;

        #region Init

        public override void Init(IAtomPlugin plugin, object arg)
        {
            base.Init(plugin, arg);

            // Right side

            CreateChangeScreenButton($"<b><</b> <i>Back to {AddAnimationsScreen.ScreenName}</i>", AddAnimationsScreen.ScreenName);
        }

        protected void InitNewClipNameUI()
        {
            clipNameJSON = new JSONStorableString("New animation name", "");
            prefabFactory.CreateTextInput(clipNameJSON);
        }

        protected void InitNewLayerNameUI()
        {
            layerNameJSON = new JSONStorableString("New layer name", "");
            prefabFactory.CreateTextInput(layerNameJSON);
        }

        protected void InitNewSegmentNameUI()
        {
            segmentNameJSON = new JSONStorableString("New segment name", "");
            prefabFactory.CreateTextInput(segmentNameJSON);
        }

        protected void InitCreateInOtherAtomsUI()
        {
            createInOtherAtoms = new JSONStorableBool("Create in other atoms", _previousCreateInOtherAtoms, val => _previousCreateInOtherAtoms = val);
            prefabFactory.CreateToggle(createInOtherAtoms);
        }

        #endregion

        protected override void OnCurrentAnimationChanged(AtomAnimationEditContext.CurrentAnimationChangedEventArgs args)
        {
            base.OnCurrentAnimationChanged(args);

            RefreshUI();
        }

        protected virtual void RefreshUI()
        {
        }
    }
}
