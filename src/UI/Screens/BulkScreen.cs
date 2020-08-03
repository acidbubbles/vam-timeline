using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace VamTimeline
{
    public class BulkScreen : ScreenBase
    {
        public const string ScreenName = "Bulk";

        private const string _offsetControllerUILabel = "Start offset controllers mode...";
        private const string _offsetControllerUIOfsettingLabel = "Apply recorded offset...";

        private static bool _offsetting;
        private static string _lastOffsetMode;
        private static string _lastAnim = null;
        private static float _lastLength = -1f;
        private static float _lastStart = -1f;
        private static float _lastEnd = -1f;
        private static AtomClipboardEntry _offsetSnapshot;

        public override string screenId => ScreenName;

        private JSONStorableFloat _startJSON;
        private JSONStorableFloat _endJSON;
        private JSONStorableString _selectionJSON;
        private JSONStorableStringChooser _changeCurveJSON;
        private JSONStorableStringChooser _offsetModeJSON;
        private UIDynamicButton _offsetControllerUI;
        private const string _changePivotMode = "Change pivot";
        private const string _offsetMode = "Offset";

        public BulkScreen()
            : base()
        {
        }

        public override void Init(IAtomPlugin plugin)
        {
            base.Init(plugin);

            CreateChangeScreenButton("<b><</b> <i>Back</i>", MoreScreen.ScreenName);

            InitSelectionUI();

            InitBulkClipboardUI();

            InitChangeCurveUI();

            InitOffsetUI();

            // Init

            _startJSON.valNoCallback = _lastStart == -1f ? 0f : Mathf.Min(_lastStart, current.animationLength);
            _endJSON.valNoCallback = _lastEnd == -1f ? current.animationLength : Mathf.Min(_lastEnd, current.animationLength);
            if (_endJSON.val <= _startJSON.val || _lastAnim != current.animationName || _lastLength != current.animationLength)
            {
                _startJSON.valNoCallback = 0f;
                _endJSON.valNoCallback = current.animationLength;
            }
            current.onTargetsSelectionChanged.AddListener(OnTargetsSelectionChanged);
            OnTargetsSelectionChanged();
        }

        private void InitOffsetUI()
        {
            _offsetModeJSON = new JSONStorableStringChooser("Offset mode", new List<string> { _changePivotMode, _offsetMode }, _lastOffsetMode ?? _changePivotMode, "Offset mode", (string val) => _lastOffsetMode = val);
            var offsetModeUI = prefabFactory.CreatePopup(_offsetModeJSON, false);
            offsetModeUI.popupPanelHeight = 160f;

            _offsetControllerUI = prefabFactory.CreateButton(_offsetting ? _offsetControllerUIOfsettingLabel : _offsetControllerUILabel);
            _offsetControllerUI.button.onClick.AddListener(() => OffsetController());
        }

        protected void InitBulkClipboardUI()
        {
            var deleteUI = prefabFactory.CreateButton("Delete frame(s)");
            deleteUI.button.onClick.AddListener(() => CopyDeleteSelected(false, true));

            var cutUI = prefabFactory.CreateButton("Cut frame(s)");
            cutUI.button.onClick.AddListener(() => CopyDeleteSelected(true, true));

            var copyUI = prefabFactory.CreateButton("Copy frame(s)");
            copyUI.button.onClick.AddListener(() => CopyDeleteSelected(true, false));

            var pasteUI = prefabFactory.CreateButton("Paste frame(s)");
            pasteUI.button.onClick.AddListener(() => plugin.pasteJSON.actionCallback());
        }

        private void InitSelectionUI()
        {
            _startJSON = new JSONStorableFloat("Selection starts at", 0f, (float val) =>
            {
                var closest = current.GetAllOrSelectedTargets().Select(t => t.GetTimeClosestTo(val)).OrderBy(t => Mathf.Abs(val - t)).First();
                _startJSON.valNoCallback = closest;
                if (_startJSON.val > _endJSON.val) _endJSON.valNoCallback = _startJSON.val;
                SelectionModified();

            }, 0f, current.animationLength);
            prefabFactory.CreateSlider(_startJSON);

            _endJSON = new JSONStorableFloat("Selection ends at", 0f, (float val) =>
            {
                var closest = current.GetAllOrSelectedTargets().Select(t => t.GetTimeClosestTo(val)).OrderBy(t => Mathf.Abs(val - t)).First();
                _endJSON.valNoCallback = closest;
                if (_endJSON.val < _startJSON.val) _startJSON.valNoCallback = _endJSON.val;
                SelectionModified();
            }, 0f, current.animationLength);
            prefabFactory.CreateSlider(_endJSON);

            var markSelectionStartUI = prefabFactory.CreateButton("Start at current time");
            markSelectionStartUI.button.onClick.AddListener(() => _startJSON.val = current.clipTime);

            var markSelectionEndUI = prefabFactory.CreateButton("End at current time");
            markSelectionEndUI.button.onClick.AddListener(() => _endJSON.val = animation.clipTime);

            _selectionJSON = new JSONStorableString("Selected frames", "")
            {
                isStorable = false
            };
            var selectionUI = prefabFactory.CreateTextField(_selectionJSON);
            selectionUI.height = 100f;
        }

        private void InitChangeCurveUI()
        {
            _changeCurveJSON = new JSONStorableStringChooser("Change curve", CurveTypeValues.labels.Select(l => l.Value).ToList(), "", "Change curve", ChangeCurve);
            var curveTypeUI = prefabFactory.CreatePopup(_changeCurveJSON, false);
            curveTypeUI.popupPanelHeight = 280f;
        }

        #region Callbacks

        private void SelectionModified()
        {
            var sb = new StringBuilder();
            foreach (var target in current.GetAllOrSelectedTargets())
            {
                var involvedKeyframes = 0;
                var keyframes = target.GetAllKeyframesTime();
                for (var key = 0; key < keyframes.Length; key++)
                {
                    var keyTime = keyframes[key];
                    if (keyTime >= _startJSON.valNoCallback && keyTime <= _endJSON.valNoCallback)
                        involvedKeyframes++;
                }
                if (involvedKeyframes > 0)
                    sb.AppendLine($"{target.name}: {involvedKeyframes} keyframes");
            }
            _selectionJSON.val = sb.ToString();
            _lastStart = _startJSON.val;
            _lastEnd = _endJSON.val;
            _lastAnim = current.animationName;
            _lastLength = current.animationLength;
        }

        public void CopyDeleteSelected(bool copy, bool delete)
        {
            plugin.clipboard.Clear();
            plugin.clipboard.time = _startJSON.valNoCallback;
            foreach (var target in current.GetAllOrSelectedTargets())
            {
                target.StartBulkUpdates();
                try
                {
                    var keyframes = target.GetAllKeyframesTime();
                    for (var key = keyframes.Length - 1; key >= 0; key--)
                    {
                        var keyTime = keyframes[key];
                        if (keyTime < _startJSON.val || keyTime > _endJSON.val) continue;

                        if (copy)
                        {
                            plugin.clipboard.entries.Insert(0, current.Copy(keyTime, current.GetAllOrSelectedTargets()));
                        }
                        if (delete && !keyTime.IsSameFrame(0) && !keyTime.IsSameFrame(current.animationLength))
                        {
                            target.DeleteFrame(keyTime);
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

            foreach (var target in current.GetAllOrSelectedTargets().OfType<ICurveAnimationTarget>())
            {
                target.StartBulkUpdates();
                try
                {
                    var leadCurve = target.GetLeadCurve();
                    for (var key = 0; key < leadCurve.length; key++)
                    {
                        var keyTime = leadCurve.GetKeyframe(key).time;
                        if (keyTime >= _startJSON.valNoCallback && keyTime <= _endJSON.valNoCallback)
                        {
                            target.ChangeCurve(keyTime, CurveTypeValues.ToInt(val), current.loop);
                        }
                    }
                }
                finally
                {
                    target.EndBulkUpdates();
                }
            }
        }

        private void OffsetController()
        {
            if (animation.isPlaying) return;

            if (_offsetting)
                ApplyOffset();
            else
                StartRecordOffset();
        }

        private void StartRecordOffset()
        {
            if (current.clipTime < _startJSON.val || current.clipTime > _endJSON.val)
            {
                SuperController.LogError($"Timeline: Cannot offset, current time is outside of the bounds of the selection");
                return;
            }
            _offsetSnapshot = current.Copy(current.clipTime, current.GetAllOrSelectedTargets().OfType<FreeControllerAnimationTarget>().Cast<IAtomAnimationTarget>());
            if (_offsetSnapshot.controllers.Count == 0)
            {
                _offsetSnapshot = null;
                SuperController.LogError($"Timeline: Cannot offset, no keyframes were found at time {current.clipTime}.");
                return;
            }
            if (_offsetSnapshot.controllers.Select(c => current.targetControllers.First(t => t.controller == c.controller)).Any(t => !t.EnsureParentAvailable(false)))
            {
                _offsetSnapshot = null;
                return;
            }

            _offsetControllerUI.label = _offsetControllerUIOfsettingLabel;
            _offsetting = true;
        }

        private void ApplyOffset()
        {
            _offsetting = false;
            _offsetControllerUI.label = _offsetControllerUILabel;

            if (animation.clipTime != _offsetSnapshot.time)
            {
                SuperController.LogError($"Timeline: Time changed. Please move controllers within a single frame. Original time: {_offsetSnapshot.time}, current time: {animation.clipTime}");
                return;
            }

            foreach (var snap in _offsetSnapshot.controllers)
            {
                var target = current.targetControllers.First(t => t.controller == snap.controller);
                if (!target.EnsureParentAvailable(false)) continue;
                var rb = target.GetLinkedRigidbody();

                Vector3 pivot;
                Vector3 positionDelta;
                Quaternion rotationDelta;

                {
                    var positionBefore = new Vector3(snap.snapshot.x.value, snap.snapshot.y.value, snap.snapshot.z.value);
                    var rotationBefore = new Quaternion(snap.snapshot.rotX.value, snap.snapshot.rotY.value, snap.snapshot.rotZ.value, snap.snapshot.rotW.value);

                    var positionAfter = rb == null ? snap.controller.control.localPosition : rb.transform.InverseTransformPoint(snap.controller.transform.position);
                    var rotationAfter = rb == null ? snap.controller.control.localRotation : Quaternion.Inverse(rb.rotation) * snap.controller.transform.rotation;

                    pivot = positionBefore;
                    positionDelta = positionAfter - positionBefore;
                    rotationDelta = Quaternion.Inverse(rotationBefore) * rotationAfter;
                }

                foreach (var key in target.GetAllKeyframesKeys())
                {
                    var time = target.GetKeyframeTime(key);
                    if (time < _startJSON.valNoCallback || time > _endJSON.valNoCallback) continue;
                    // Do not double-apply
                    if (time == _offsetSnapshot.time) continue;

                    var positionBefore = target.GetKeyframePosition(key);
                    var rotationBefore = target.GetKeyframeRotation(key);

                    if (_offsetModeJSON.val == _changePivotMode)
                    {
                        var positionAfter = rotationDelta * (positionBefore - pivot) + pivot + positionDelta;
                        target.SetKeyframeByKey(key, positionAfter, rotationDelta * rotationBefore);
                    }
                    else if (_offsetModeJSON.val == _offsetMode)
                    {
                        target.SetKeyframeByKey(key, positionBefore + positionDelta, rotationDelta * rotationBefore);
                    }
                    else
                    {
                        throw new NotImplementedException($"Offset mode '{_offsetModeJSON.val}' is not implemented");
                    }
                }
            }
        }

        #endregion

        public void OnTargetsSelectionChanged()
        {
            SelectionModified();
        }

        protected override void OnCurrentAnimationChanged(AtomAnimation.CurrentAnimationChangedEventArgs args)
        {
            base.OnCurrentAnimationChanged(args);

            args.before.onTargetsSelectionChanged.RemoveListener(OnTargetsSelectionChanged);
            args.after.onTargetsSelectionChanged.AddListener(OnTargetsSelectionChanged);

            if (current.animationLength < _endJSON.valNoCallback)
            {
                _endJSON.valNoCallback = current.animationLength;
                if (_startJSON.valNoCallback > _endJSON.valNoCallback) _startJSON.valNoCallback = _endJSON.valNoCallback;
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

