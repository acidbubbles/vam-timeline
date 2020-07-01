using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    public class CurveTypePopup : MonoBehaviour
    {
        private const string _noKeyframeCurveType = "(No Keyframe)";
        private const string _loopCurveType = "(Loop)";

        public static CurveTypePopup Create(VamPrefabFactory prefabFactory)
        {
            var curveTypeJSON = new JSONStorableStringChooser(StorableNames.ChangeCurve, CurveTypeValues.DisplayCurveTypes, "", "Change Curve");
            var curveTypeUI = prefabFactory.CreateScrollablePopup(curveTypeJSON);
            curveTypeUI.popupPanelHeight = 300f;

            var curveTypePopup = curveTypeUI.gameObject.AddComponent<CurveTypePopup>();
            curveTypePopup.curveTypeJSON = curveTypeJSON;
            curveTypePopup.curveTypeUI = curveTypeUI;

            return curveTypePopup;
        }

        public JSONStorableStringChooser curveTypeJSON;
        public UIDynamicPopup curveTypeUI;
        private AtomAnimation _animation;
        private AtomAnimationClip _current => _animation.current;
        private bool _listening;

        public void Bind(AtomAnimation animation)
        {
            _animation = animation;
            curveTypeJSON.setCallbackFunction = ChangeCurve;
            OnEnable();
        }

        private void ChangeCurve(string curveType)
        {
            if (string.IsNullOrEmpty(curveType) || curveType.StartsWith("("))
            {
                RefreshCurrentCurveType(_animation.clipTime);
                return;
            }
            float time = _animation.clipTime.Snap();
            if (time.IsSameFrame(0) && curveType == CurveTypeValues.CopyPrevious)
            {
                RefreshCurrentCurveType(_animation.clipTime);
                return;
            }
            if (_animation.isPlaying) return;
            if (_current.loop && (time.IsSameFrame(0) || time.IsSameFrame(_current.animationLength)))
            {
                RefreshCurrentCurveType(_animation.clipTime);
                return;
            }

            foreach (var target in _current.targetControllers)
                target.ChangeCurve(time, curveType);

            RefreshCurrentCurveType(_animation.clipTime);
        }

        private void RefreshCurrentCurveType(float currentClipTime)
        {
            if (curveTypeJSON == null) return;

            var time = currentClipTime.Snap();
            if (_current.loop && (time.IsSameFrame(0) || time.IsSameFrame(_current.animationLength)))
            {
                curveTypeJSON.valNoCallback = _loopCurveType;
                return;
            }
            var ms = time.ToMilliseconds();
            var curveTypes = new HashSet<string>();
            foreach (var target in _current.GetAllOrSelectedTargets().OfType<FreeControllerAnimationTarget>())
            {
                KeyframeSettings v;
                if (!target.settings.TryGetValue(ms, out v)) continue;
                curveTypes.Add(v.curveType);
            }
            if (curveTypes.Count == 0)
                curveTypeJSON.valNoCallback = _noKeyframeCurveType;
            else if (curveTypes.Count == 1)
                curveTypeJSON.valNoCallback = curveTypes.First().ToString();
            else
                curveTypeJSON.valNoCallback = "(" + string.Join("/", curveTypes.ToArray()) + ")";
        }

        private void OnTimeChanged(AtomAnimation.TimeChangedEventArgs args)
        {
            RefreshCurrentCurveType(args.currentClipTime);
        }

        private void OnTargetsSelectionChanged()
        {
            curveTypeUI.popup.topButton.interactable = _current.GetAllOrSelectedTargets().OfType<FreeControllerAnimationTarget>().Count() > 0;
        }

        public void OnEnable()
        {
            if (_listening || _animation == null) return;
            _listening = true;
            _animation.onTimeChanged.AddListener(OnTimeChanged);
            _animation.onTargetsSelectionChanged.AddListener(OnTargetsSelectionChanged);
            OnTimeChanged(_animation.timeArgs);
        }

        public void OnDisable()
        {
            if (!_listening || _animation == null) return;
            _animation.onTimeChanged.RemoveListener(OnTimeChanged);
            _animation.onTargetsSelectionChanged.RemoveListener(OnTargetsSelectionChanged);
            _listening = false;
        }
    }
}
