using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class CurveTypePopup : MonoBehaviour
    {
        private const string _noKeyframeCurveType = "(No Keyframe)";

        private readonly HashSet<string> _curveTypes = new HashSet<string>();

        public static CurveTypePopup Create(VamPrefabFactory prefabFactory)
        {
            var curveTypeJSON = new JSONStorableStringChooser("Change curve", CurveTypeValues.labels2, "", "Curve type");
            var curveTypeUI = prefabFactory.CreatePopup(curveTypeJSON, false, true);
            curveTypeUI.popupPanelHeight = 260f;

            var curveTypePopup = curveTypeUI.gameObject.AddComponent<CurveTypePopup>();
            curveTypePopup.curveTypeJSON = curveTypeJSON;
            curveTypePopup.curveTypeUI = curveTypeUI;

            return curveTypePopup;
        }

        public JSONStorableStringChooser curveTypeJSON;
        public UIDynamicPopup curveTypeUI;
        private AtomAnimationEditContext _animationEditContext;
        private bool _listening;

        public void Bind(AtomAnimationEditContext animationEditContext)
        {
            _animationEditContext = animationEditContext;
            curveTypeJSON.setCallbackFunction = ChangeCurve;
            OnEnable();
        }

        private void ChangeCurve(string val)
        {
            if (!_animationEditContext.CanEdit())
            {
                RefreshCurrentCurveType(_animationEditContext.clipTime);
                return;
            }

            if (string.IsNullOrEmpty(val) || val.StartsWith("("))
            {
                RefreshCurrentCurveType(_animationEditContext.clipTime);
                return;
            }
            float time = _animationEditContext.clipTime.Snap();

            var curveType = CurveTypeValues.ToInt(val);

            foreach (var target in _animationEditContext.GetAllOrSelectedTargets().OfType<ICurveAnimationTarget>())
                target.ChangeCurve(time, curveType);

            if (curveType == CurveTypeValues.CopyPrevious)
                _animationEditContext.Sample();

            RefreshCurrentCurveType(_animationEditContext.clipTime);
        }

        private void RefreshCurrentCurveType(float currentClipTime)
        {
            if (curveTypeJSON == null) return;

            var time = currentClipTime.Snap();
            var ms = time.ToMilliseconds();
            _curveTypes.Clear();
            foreach (var target in _animationEditContext.GetAllOrSelectedTargets().OfType<ICurveAnimationTarget>())
            {
                var curveType = target.GetKeyframeCurveType(time);
                if (curveType == BezierKeyframe.NullKeyframeCurveType) continue;
                _curveTypes.Add(CurveTypeValues.FromInt(curveType));
            }

            if (_curveTypes.Count == 0)
            {
                curveTypeJSON.valNoCallback = _noKeyframeCurveType;
                curveTypeUI.popup.topButton.interactable = false;
            }
            else if (_curveTypes.Count == 1)
            {
                curveTypeJSON.valNoCallback = _curveTypes.First().ToString();
                curveTypeUI.popup.topButton.interactable = true;
            }
            else
            {
                curveTypeJSON.valNoCallback = "(" + string.Join("/", _curveTypes.ToArray()) + ")";
                curveTypeUI.popup.topButton.interactable = true;
            }
        }

        private void OnTimeChanged(AtomAnimationEditContext.TimeChangedEventArgs args)
        {
            RefreshCurrentCurveType(args.currentClipTime);
        }

        private void OnTargetsSelectionChanged()
        {
            RefreshCurrentCurveType(_animationEditContext.clipTime);
        }

        private void OnAnimationRebuilt()
        {
            RefreshCurrentCurveType(_animationEditContext.clipTime);
        }

        public void OnEnable()
        {
            if (_listening || _animationEditContext == null) return;
            _listening = true;
            _animationEditContext.onTimeChanged.AddListener(OnTimeChanged);
            _animationEditContext.onTargetsSelectionChanged.AddListener(OnTargetsSelectionChanged);
            _animationEditContext.animation.onAnimationRebuilt.AddListener(OnAnimationRebuilt);
            OnTimeChanged(_animationEditContext.timeArgs);
        }

        public void OnDisable()
        {
            if (!_listening || _animationEditContext == null) return;
            _animationEditContext.onTimeChanged.RemoveListener(OnTimeChanged);
            _animationEditContext.onTargetsSelectionChanged.RemoveListener(OnTargetsSelectionChanged);
            _animationEditContext.animation.onAnimationRebuilt.RemoveListener(OnAnimationRebuilt);
            _listening = false;
        }
    }
}
