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

        public override string name => ScreenName;

        private JSONStorableString _selectionJSON;
        private string _selectedControllers;
        private float _selectionStart = 0;
        private float _selectionEnd = 0;
        private JSONStorableStringChooser _changeCurveJSON;

        public BulkScreen(IAtomPlugin plugin)
            : base(plugin)
        {

        }
        public override void Init()
        {
            base.Init();

            // Left side

            InitBulkClipboardUI(false);

            // Right side

            InitSelectionUI(true);

            CreateSpacer(true);

            InitChangeCurveUI(true);

            CreateSpacer(true);

            InitDeleteUI(true);

            // Init

            _selectionStart = 0f;
            _selectionEnd = current.animationLength;
            current.onTargetsSelectionChanged.AddListener(OnTargetsSelectionChanged);
            OnTargetsSelectionChanged();
        }

        protected void InitBulkClipboardUI(bool rightSide)
        {
            var cutUI = plugin.CreateButton("Cut / Delete Frame(s)", rightSide);
            cutUI.button.onClick.AddListener(() => CopyDeleteSelected(true, true));
            RegisterComponent(cutUI);

            var copyUI = plugin.CreateButton("Copy Frame(s)", rightSide);
            copyUI.button.onClick.AddListener(() => CopyDeleteSelected(true, false));
            RegisterComponent(copyUI);

            var pasteUI = plugin.CreateButton("Paste Frame(s)", rightSide);
            pasteUI.button.onClick.AddListener(() => plugin.pasteJSON.actionCallback());
            RegisterComponent(pasteUI);
        }

        private void InitSelectionUI(bool rightSide)
        {
            _selectionJSON = new JSONStorableString("Selected Frames", "")
            {
                isStorable = false
            };
            RegisterStorable(_selectionJSON);
            var selectionUI = plugin.CreateTextField(_selectionJSON, rightSide);
            RegisterComponent(selectionUI);

            var markSelectionStartUI = plugin.CreateButton("Mark Selection Start", rightSide);
            markSelectionStartUI.button.onClick.AddListener(MarkSelectionStart);
            RegisterComponent(markSelectionStartUI);

            var markSelectionEndUI = plugin.CreateButton("Mark Selection End", rightSide);
            markSelectionEndUI.button.onClick.AddListener(MarkSelectionEnd);
            RegisterComponent(markSelectionEndUI);
        }

        private void InitChangeCurveUI(bool rightSide)
        {
            _changeCurveJSON = new JSONStorableStringChooser(StorableNames.ChangeCurve, CurveTypeValues.DisplayCurveTypes, "", "Change Curve", ChangeCurve);
            RegisterStorable(_changeCurveJSON);
            var curveTypeUI = plugin.CreateScrollablePopup(_changeCurveJSON, rightSide);
            curveTypeUI.popupPanelHeight = 340f;
            RegisterComponent(curveTypeUI);
        }

        private void InitDeleteUI(bool rightSide)
        {
            var deleteSelectedUI = plugin.CreateButton("Delete Selected", rightSide);
            deleteSelectedUI.button.onClick.AddListener(() => CopyDeleteSelected(false, true));
            RegisterComponent(deleteSelectedUI);
        }

        #region Callbacks

        private void MarkSelectionStart()
        {
            _selectionStart = plugin.animation.Time;
            if (_selectionEnd < _selectionStart) _selectionEnd = _selectionStart;
            SelectionModified();
        }

        private void MarkSelectionEnd()
        {
            _selectionEnd = plugin.animation.Time;
            if (_selectionStart > _selectionEnd) _selectionStart = _selectionEnd;
            SelectionModified();
        }

        private void SelectionModified()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Selected range: {_selectionStart:0.000}s-{_selectionEnd:0.000}s of {current.animationLength:0.000}s");
            var involvedKeyframes = 0;
            foreach (var target in current.GetAllOrSelectedTargets())
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
            foreach (var target in current.GetAllOrSelectedTargets())
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

            args.Before.onTargetsSelectionChanged.RemoveListener(OnTargetsSelectionChanged);
            args.After.onTargetsSelectionChanged.AddListener(OnTargetsSelectionChanged);

            if (current.animationLength < _selectionEnd)
            {
                _selectionEnd = current.animationLength;
                if (_selectionStart > _selectionEnd) _selectionStart = _selectionEnd;
            }

            SelectionModified();
        }

        public override void Dispose()
        {
            base.Dispose();

            current.onTargetsSelectionChanged.RemoveListener(OnTargetsSelectionChanged);
        }
    }
}

