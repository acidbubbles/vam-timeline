using System.Linq;
using System.Text;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class BulkScreen : ScreenBase
    {
        public const string ScreenName = "Bulk";

        public override string screenId => ScreenName;

        private JSONStorableString _selectionJSON;
        private string _selectedControllers;
        private float _selectionStart = 0;
        private float _selectionEnd = 0;
        private JSONStorableStringChooser _changeCurveJSON;

        public BulkScreen()
            : base()
        {
        }

        public override void Init(IAtomPlugin plugin)
        {
            base.Init(plugin);

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName);

            InitBulkClipboardUI();

            InitSelectionUI();

            prefabFactory.CreateSpacer();

            InitChangeCurveUI();

            prefabFactory.CreateSpacer();

            InitDeleteUI();

            // Init

            _selectionStart = 0f;
            _selectionEnd = current.animationLength;
            current.onTargetsSelectionChanged.AddListener(OnTargetsSelectionChanged);
            OnTargetsSelectionChanged();
        }

        protected void InitBulkClipboardUI()
        {
            var cutUI = prefabFactory.CreateButton("Cut / Delete Frame(s)");
            cutUI.button.onClick.AddListener(() => CopyDeleteSelected(true, true));

            var copyUI = prefabFactory.CreateButton("Copy Frame(s)");
            copyUI.button.onClick.AddListener(() => CopyDeleteSelected(true, false));

            var pasteUI = prefabFactory.CreateButton("Paste Frame(s)");
            pasteUI.button.onClick.AddListener(() => plugin.pasteJSON.actionCallback());
        }

        private void InitSelectionUI()
        {
            _selectionJSON = new JSONStorableString("Selected Frames", "")
            {
                isStorable = false
            };
            var selectionUI = prefabFactory.CreateTextField(_selectionJSON);

            var markSelectionStartUI = prefabFactory.CreateButton("Mark Selection Start");
            markSelectionStartUI.button.onClick.AddListener(MarkSelectionStart);

            var markSelectionEndUI = prefabFactory.CreateButton("Mark Selection End");
            markSelectionEndUI.button.onClick.AddListener(MarkSelectionEnd);
        }

        private void InitChangeCurveUI()
        {
            _changeCurveJSON = new JSONStorableStringChooser(StorableNames.ChangeCurve, CurveTypeValues.DisplayCurveTypes, "", "Change Curve", ChangeCurve);
            var curveTypeUI = prefabFactory.CreateScrollablePopup(_changeCurveJSON);
            curveTypeUI.popupPanelHeight = 340f;
        }

        private void InitDeleteUI()
        {
            var deleteSelectedUI = prefabFactory.CreateButton("Delete Selected");
            deleteSelectedUI.button.onClick.AddListener(() => CopyDeleteSelected(false, true));
        }

        #region Callbacks

        private void MarkSelectionStart()
        {
            _selectionStart = animation.clipTime;
            if (_selectionEnd < _selectionStart) _selectionEnd = _selectionStart;
            SelectionModified();
        }

        private void MarkSelectionEnd()
        {
            _selectionEnd = animation.clipTime;
            if (_selectionStart > _selectionEnd) _selectionStart = _selectionEnd;
            SelectionModified();
        }

        private void SelectionModified()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Selected range: {_selectionStart:0.000}s-{_selectionEnd:0.000}s of {current.animationLength:0.000}s");
            var involvedKeyframes = 0;
            foreach (var target in current.GetAllOrSelectedTargets().OfType<IAnimationTargetWithCurves>())
            {
                var leadCurve = target.GetLeadCurve();
                for (var key = 0; key < leadCurve.length; key++)
                {
                    var keyTime = leadCurve[key].time;
                    if (keyTime >= _selectionStart && keyTime <= _selectionEnd)
                        involvedKeyframes++;
                }
                if (involvedKeyframes > 0)
                    sb.AppendLine($"- {target.name}: {involvedKeyframes} keyframes");
            }
            _selectionJSON.val = sb.ToString();
        }

        public void CopyDeleteSelected(bool copy, bool delete)
        {
            plugin.clipboard.Clear();
            plugin.clipboard.time = _selectionStart;
            // TODO: This ignores triggers
            foreach (var target in current.GetAllOrSelectedTargets().OfType<IAnimationTargetWithCurves>())
            {
                target.StartBulkUpdates();
                try
                {
                    var leadCurve = target.GetLeadCurve();
                    for (var key = leadCurve.length - 1; key >= 0; key--)
                    {
                        var keyTime = leadCurve[key].time;
                        if (keyTime >= _selectionStart && keyTime <= _selectionEnd)
                        {
                            if (copy)
                            {
                                plugin.clipboard.entries.Insert(0, current.Copy(keyTime));
                            }
                            if (delete && !keyTime.IsSameFrame(0) && !keyTime.IsSameFrame(current.animationLength))
                            {
                                target.DeleteFrameByKey(key);
                            }
                        }
                    }
                }
                finally
                {
                    target.EndBulkUpdates();
                }
            }
        }

        public void ChangeCurve(string val)
        {
            if (string.IsNullOrEmpty(val)) return;
            _changeCurveJSON.valNoCallback = "";

            foreach (var target in current.GetAllOrSelectedTargets().OfType<FreeControllerAnimationTarget>())
            {
                target.StartBulkUpdates();
                try
                {
                    var leadCurve = target.GetLeadCurve();
                    for (var key = leadCurve.length - 2; key > 0; key--)
                    {
                        var keyTime = leadCurve[key].time;
                        if (keyTime >= _selectionStart && keyTime <= _selectionEnd)
                        {
                            target.ChangeCurve(keyTime, val);
                        }
                    }
                }
                finally
                {
                    target.EndBulkUpdates();
                }
            }
        }

        #endregion

        public void OnTargetsSelectionChanged()
        {
            var selectedControllers = string.Join(",", current.GetAllOrSelectedTargets().Select(t => t.name).ToArray());
            if (_selectedControllers != selectedControllers)
            {
                SelectionModified();
                _selectedControllers = selectedControllers;
            }
        }

        protected override void OnCurrentAnimationChanged(AtomAnimation.CurrentAnimationChangedEventArgs args)
        {
            base.OnCurrentAnimationChanged(args);

            args.before.onTargetsSelectionChanged.RemoveListener(OnTargetsSelectionChanged);
            args.after.onTargetsSelectionChanged.AddListener(OnTargetsSelectionChanged);

            if (current.animationLength < _selectionEnd)
            {
                _selectionEnd = current.animationLength;
                if (_selectionStart > _selectionEnd) _selectionStart = _selectionEnd;
            }

            SelectionModified();
        }

        public override void OnDestroy()
        {
            base.OnDestroy();

            current.onTargetsSelectionChanged.RemoveListener(OnTargetsSelectionChanged);
        }
    }
}

