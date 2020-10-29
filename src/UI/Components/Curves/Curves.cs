using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace VamTimeline
{
    public class Curves : MonoBehaviour
    {
        private const int _maxCurves = 9;

        private readonly CurvesStyle _style = new CurvesStyle();
        private readonly RectTransform _scrubberLineRect;
        private readonly GameObject _noCurves;
        private readonly GameObject _linesContainer;
        private readonly IList<ICurveAnimationTarget> _targets = new List<ICurveAnimationTarget>();
        private readonly IList<CurvesLines> _lines = new List<CurvesLines>();
        private float _animationLength;
        private AtomAnimationEditContext _animationEditContext;
        private float _clipTime;

        public Curves()
        {
            CreateBackground(_style.BackgroundColor);

            _noCurves = CreateNoCurvesText();

            _linesContainer = CreateLinesContainer();
            _scrubberLineRect = CreateScrubber();
        }

        private GameObject CreateBackground(Color color)
        {
            var go = new GameObject();
            go.transform.SetParent(transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.StretchParent();

            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;

            return go;
        }

        private RectTransform CreateScrubber()
        {
            var go = new GameObject("Scrubber");
            go.transform.SetParent(transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.StretchParent();

            var line = new GameObject("Scrubber Line");
            line.transform.SetParent(go.transform, false);

            var lineRect = line.AddComponent<RectTransform>();
            lineRect.StretchCenter();
            lineRect.sizeDelta = new Vector2(_style.ScrubberSize, 0);

            var image = line.AddComponent<Image>();
            image.color = _style.ScrubberColor;
            image.raycastTarget = false;

            return lineRect;
        }

        public GameObject CreateLinesContainer()
        {
            var go = new GameObject();
            go.transform.SetParent(transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.StretchParent();

            var group = go.AddComponent<VerticalLayoutGroup>();
            group.childForceExpandHeight = true;

            return go;
        }

        private CurvesLines CreateCurvesLines(GameObject parent, Color color, string label)
        {
            var go = new GameObject();
            go.transform.SetParent(parent.transform, false);

            var image = go.AddComponent<Image>();
            image.color = Color.yellow;
            image.raycastTarget = false;

            var mask = go.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var layout = go.AddComponent<LayoutElement>();
            layout.flexibleWidth = 1;
            layout.flexibleHeight = 1;

            var linesContainer = new GameObject();
            linesContainer.transform.SetParent(go.transform, false);

            var linesRect = linesContainer.AddComponent<RectTransform>();
            linesRect.StretchParent();

            var lines = linesContainer.AddComponent<CurvesLines>();
            lines.style = _style;
            lines.raycastTarget = false;

            var textContainer = new GameObject();
            textContainer.transform.SetParent(go.transform, false);

            var textRect = textContainer.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 0);
            textRect.anchorMax = new Vector2(1, 0);
            textRect.anchoredPosition = new Vector2(5f, 5f);
            textRect.pivot = new Vector2(0, 0);
            textRect.sizeDelta = new Vector2(0, 30);

            var textText = textContainer.AddComponent<Text>();
            textText.raycastTarget = false;
            textText.alignment = TextAnchor.LowerLeft;
            textText.font = _style.Font;
            textText.color = Color.Lerp(color, Color.white, .9f);
            textText.text = label;
            textText.fontSize = 20;

            return lines;
        }

        private GameObject CreateNoCurvesText()
        {
            var go = new GameObject();
            go.transform.SetParent(transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.StretchParent();
            rect.offsetMin = new Vector2(20f, 0);
            rect.offsetMax = new Vector2(-20f, 0);

            var text = go.AddComponent<Text>();
            text.text = "Select targets in the dope sheet to see their curves";
            text.font = _style.Font;
            text.fontSize = 28;
            text.color = _style.FontColor;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;

            return go;
        }

        public void Bind(AtomAnimationEditContext animationEditContext)
        {
            if (_animationEditContext != null) throw new InvalidOperationException("Cannot bind to animation twice");
            _animationEditContext = animationEditContext;
            _animationEditContext.onTargetsSelectionChanged.AddListener(OnTargetsSelectionChanged);
            OnTargetsSelectionChanged();
        }

        private void OnTargetsSelectionChanged()
        {
            Bind(_animationEditContext.current.GetAllCurveTargets().Count() == 1
                ? _animationEditContext.current.GetAllCurveTargets().ToList()
                : _animationEditContext.GetSelectedTargets().OfType<ICurveAnimationTarget>().ToList()
            );
        }

        private void Bind(IList<ICurveAnimationTarget> targets)
        {
            Unbind();

            if ((targets?.Count ?? 0) > 0)
            {
                foreach (var target in targets)
                {
                    BindCurves(target);
                }
                _noCurves.SetActive(false);
                _scrubberLineRect.transform.parent.gameObject.SetActive(true);
                _clipTime = -1f;
            }
            else
            {
                _animationLength = 0f;
                _noCurves.SetActive(true);
                _scrubberLineRect.transform.parent.gameObject.SetActive(false);
            }
        }

        private void BindCurves(ICurveAnimationTarget target)
        {
            var lead = target.GetLeadCurve();
            _animationLength = lead.length >= 2 ? lead.GetKeyframeByKey(lead.length - 1).time : 0f;
            _targets.Add(target);
            target.onAnimationKeyframesRebuilt.AddListener(OnAnimationKeyframesRebuilt);
            if (target is FreeControllerAnimationTarget)
            {
                var t = (FreeControllerAnimationTarget)target;
                if (_lines.Count > _maxCurves - 3) return;
                BindCurve(t.x, _style.CurveLineColorX, $"{target.GetShortName()} x");
                BindCurve(t.y, _style.CurveLineColorY, $"{target.GetShortName()} y");
                BindCurve(t.z, _style.CurveLineColorZ, $"{target.GetShortName()} z");
                // To display rotation as euleur angles, we have to build custom curves. But it's not that useful.
                // var rotVX = new VamKeyframe[t.rotX.length];
                // var rotVY = new VamKeyframe[t.rotY.length];
                // var rotVZ = new VamKeyframe[t.rotZ.length];
                // for (var time = 0; time < t.rotW.length; time++)
                // {
                //     VamKeyframe keyX = t.rotX[time];
                //     VamKeyframe keyY = t.rotY[time];
                //     VamKeyframe keyZ = t.rotZ[time];
                //     VamKeyframe keyW = t.rotW[time];
                //     var rot = new Quaternion(
                //         keyX.value,
                //         keyY.value,
                //         keyZ.value,
                //         keyW.value
                //     );
                //     var eulerAngles = rot.eulerAngles;
                //     rotVX[time] = new VamKeyframe(keyW.time, eulerAngles.x);
                //     rotVY[time] = new VamKeyframe(keyW.time, eulerAngles.y);
                //     rotVZ[time] = new VamKeyframe(keyW.time, eulerAngles.z);
                // }
                // VamAnimationCurve rotVXCurve = new VamAnimationCurve(rotVX);
                // VamAnimationCurve rotVYCurve = new VamAnimationCurve(rotVY);
                // VamAnimationCurve rotVZCurve = new VamAnimationCurve(rotVZ);
                // BindCurve(rotVXCurve, new Color(1.0f, 0.8f, 0.8f), $"{target.GetShortName()} rot x");
                // BindCurve(rotVYCurve, new Color(0.8f, 1.0f, 0.8f), $"{target.GetShortName()} rot y");
                // BindCurve(rotVZCurve, new Color(0.8f, 0.8f, 1.0f), $"{target.GetShortName()} rot z");
            }
            else if (target is FloatParamAnimationTarget)
            {
                if (_lines.Count > _maxCurves - 1) return;
                var t = (FloatParamAnimationTarget)target;
                BindCurve(t.value, _style.CurveLineColorFloat, target.GetShortName());
            }
            else
            {
                return;
            }
        }

        private void BindCurve(BezierAnimationCurve lead, Color color, string label)
        {
            var lines = CreateCurvesLines(_linesContainer, color, label);
            _lines.Add(lines);
            lines.AddCurve(color, lead);
            lines.SetVerticesDirty();
        }

        private void Unbind()
        {
            foreach (var t in _targets)
                t.onAnimationKeyframesRebuilt.RemoveListener(OnAnimationKeyframesRebuilt);
            _targets.Clear();
            foreach (var l in _lines)
                Destroy(l.gameObject.transform.parent.gameObject);
            _lines.Clear();
        }

        private void OnAnimationKeyframesRebuilt()
        {
            StartCoroutine(DrawCurveLinesDeferred());
        }

        private IEnumerator DrawCurveLinesDeferred()
        {
            // Allow curve refresh;
            yield return 0;
            yield return 0;
            foreach (var l in _lines)
            {
                l.SetVerticesDirty();
            }
        }

        public void Update()
        {
            if (_animationEditContext == null) return;
            if (_animationEditContext.clipTime == _clipTime) return;
            if (UIPerformance.ShouldSkip()) return;

            _clipTime = _animationEditContext.clipTime;
            var ratio = Mathf.Clamp01(_animationEditContext.clipTime / _animationLength);
            _scrubberLineRect.anchorMin = new Vector2(ratio, 0);
            _scrubberLineRect.anchorMax = new Vector2(ratio, 1);
        }

        public void OnDestroy()
        {
            if (_animationEditContext != null)
                _animationEditContext.onTargetsSelectionChanged.RemoveListener(OnTargetsSelectionChanged);

            foreach (var t in _targets)
                t.onAnimationKeyframesRebuilt.RemoveListener(OnAnimationKeyframesRebuilt);
        }
    }
}
