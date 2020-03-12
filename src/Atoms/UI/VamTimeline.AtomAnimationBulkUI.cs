using System;
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
    public class AtomAnimationBulkUI : AtomAnimationBaseUI
    {
        public const string ScreenName = "Bulk Operations";
        public override string Name => ScreenName;
        private JSONStorableString _selectionJSON;
        private string _selectedControllers;
        private float _selectionStart = 0;
        private float _selectionEnd = 0;

        public AtomAnimationBulkUI(IAtomPlugin plugin)
            : base(plugin)
        {

        }
        public override void Init()
        {
            base.Init();

            // Left side

            InitAnimationSelectorUI(false);

            InitPlaybackUI(false);

            InitFrameNavUI(false);

            // Right side

            InitDisplayUI(true);

            InitSelectionUI(true);

            CreateSpacer(true);

            InitOperationsUI(true);

            // Init

            _selectionStart = 0f;
            _selectionEnd = Plugin.Animation.Current.AnimationLength;
            AnimationFrameUpdated();
        }

        private void InitSelectionUI(bool rightSide)
        {
            _selectionJSON = new JSONStorableString("Selected Frames", "")
            {
                isStorable = false
            };
            Plugin.CreateTextField(_selectionJSON, rightSide);
            _linkedStorables.Add(_selectionJSON);

            var markSelectionStartUI = Plugin.CreateButton("Mark Selection Start", rightSide);
            markSelectionStartUI.button.onClick.AddListener(MarkSelectionStart);
            _components.Add(markSelectionStartUI);

            var markSelectionEndUI = Plugin.CreateButton("Mark Selection End", rightSide);
            markSelectionEndUI.button.onClick.AddListener(MarkSelectionEnd);
            _components.Add(markSelectionEndUI);
        }

        private void InitOperationsUI(bool rightSide)
        {
            var deleteSelectedUI = Plugin.CreateButton("Delete Selected", rightSide);
            deleteSelectedUI.button.onClick.AddListener(DeleteSelected);
            _components.Add(deleteSelectedUI);
        }

        #region Callbacks

        private void MarkSelectionStart()
        {
            _selectionStart = Plugin.Animation.Time;
            if (_selectionEnd < _selectionStart) _selectionEnd = _selectionStart;
            SelectionModified();
        }

        private void MarkSelectionEnd()
        {
            _selectionEnd = Plugin.Animation.Time;
            if (_selectionStart > _selectionEnd) _selectionStart = _selectionEnd;
            SelectionModified();
        }

        private void SelectionModified()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Selected range: {_selectionStart:0.000}s-{_selectionEnd:0.000}s of {Plugin.Animation.Current.AnimationLength:0.000}s");
            var involvedKeyframes = 0;
            foreach (var target in Plugin.Animation.Current.GetAllOrSelectedTargets())
            {
                var leadCurve = target.GetLeadCurve();
                for (var key = 1; key < leadCurve.length - 1; key++)
                {
                    var keyTime = leadCurve[key].time;
                    if (keyTime >= _selectionStart && keyTime <= _selectionEnd)
                        involvedKeyframes++;
                }
                if (involvedKeyframes > 0)
                    sb.AppendLine($"- {target.Name}: {involvedKeyframes} keyframes");
            }
            _selectionJSON.val = sb.ToString();
        }

        public void DeleteSelected()
        {
            var deletedFrames = 0;
            foreach (var target in Plugin.Animation.Current.GetAllOrSelectedTargets())
            {
                var leadCurve = target.GetLeadCurve();
                for (var key = leadCurve.length - 2; key > 0; key--)
                {
                    var keyTime = leadCurve[key].time;
                    if (keyTime >= _selectionStart && keyTime <= _selectionEnd)
                    {
                        target.DeleteFrameByKey(key);
                        deletedFrames++;
                    }
                }
            }
            Plugin.Animation.RebuildAnimation();
            Plugin.AnimationModified();
        }

        #endregion

        public override void AnimationFrameUpdated()
        {
            base.AnimationFrameUpdated();

            var selectedControllers = string.Join(",", Plugin.Animation.Current.GetAllOrSelectedTargets().Select(t => t.Name).ToArray());
            if (_selectedControllers != selectedControllers)
            {
                SelectionModified();
                _selectedControllers = selectedControllers;
            }
        }

        public override void AnimationModified()
        {
            base.AnimationModified();

            var current = Plugin.Animation.Current;
            if (current.AnimationLength < _selectionEnd)
            {
                _selectionEnd = current.AnimationLength;
                if (_selectionStart > _selectionEnd) _selectionStart = _selectionEnd;
            }
            SelectionModified();
        }
    }
}

