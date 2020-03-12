using System;
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

            InitSelectionUI();
        }

        private void InitSelectionUI()
        {
            _selectionJSON = new JSONStorableString("Selected Frames", "")
            {
                isStorable = false
            };
            Plugin.CreateTextField(_selectionJSON, true);
            _linkedStorables.Add(_selectionJSON);

            var markSelectionStartUI = Plugin.CreateButton("Mark Selection Start", true);
            markSelectionStartUI.button.onClick.AddListener(MarkSelectionStart);
            _components.Add(markSelectionStartUI);

            var markSelectionEndUI = Plugin.CreateButton("Mark Selection End", true);
            markSelectionEndUI.button.onClick.AddListener(MarkSelectionEnd);
            _components.Add(markSelectionEndUI);

            _selectionStart = 0f;
            _selectionEnd = Plugin.Animation.Current.AnimationLength;
            SelectionModified();
        }

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
            sb.AppendLine($"Selected {_selectionStart:0.000}-{_selectionEnd:0.000}s of {Plugin.Animation.Current.AnimationLength:0.000}s");
            var involvedKeyframes = 0;
            foreach (var target in Plugin.Animation.Current.GetAllOrSelectedTargets())
            {
                var leadCurve = target.GetLeadCurve();
                for (var key = 0; key < leadCurve.length; key++)
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

        public override void AnimationModified()
        {
            base.AnimationModified();

            var current = Plugin.Animation.Current;
            if (current.AnimationLength < _selectionEnd)
            {
                _selectionEnd = current.AnimationLength;
                if (_selectionStart > _selectionEnd) _selectionStart = _selectionEnd;
                SelectionModified();
            }
        }
    }
}

