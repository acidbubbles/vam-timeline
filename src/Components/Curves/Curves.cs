using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public class Curves : MonoBehaviour
    {
        private readonly CurvesStyle _style = new CurvesStyle();
        private readonly RectTransform _scrubberRect;
        private readonly GameObject _noCurves;
        private readonly CurvesLines _lines;
        private float _animationLength;
        private AtomAnimation _animation;
        private IAnimationTargetWithCurves _target;
        private float _time;

        public Curves()
        {
            gameObject.AddComponent<Canvas>();
            gameObject.AddComponent<GraphicRaycaster>();

            var image = gameObject.AddComponent<Image>();
            image.raycastTarget = false;

            var mask = gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            CreateBackground(_style.BackgroundColor);

            _noCurves = CreateNoCurvesText();

            _lines = CreateCurvesLines();
            _scrubberRect = CreateScrubber();
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

            return rect;
        }

        private CurvesLines CreateCurvesLines()
        {
            var go = new GameObject();
            go.transform.SetParent(transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.StretchParent();

            var lines = go.AddComponent<CurvesLines>();
            lines.style = _style;
            lines.raycastTarget = false;

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
            text.text = "Select a single target in the dope sheet to see the curves";
            text.font = _style.Font;
            text.fontSize = 28;
            text.color = _style.FontColor;
            text.alignment = TextAnchor.MiddleCenter;
            text.raycastTarget = false;

            return go;
        }

        public void Bind(AtomAnimation animation, IAnimationTargetWithCurves target)
        {
            Unbind();

            _animation = animation;
            if (target != null)
            {
                _target = target;
                _target.AnimationKeyframesModified.AddListener(OnAnimationCurveModified);
                BindCurves();
                _noCurves.SetActive(false);
                _scrubberRect.gameObject.SetActive(true);
                _time = -1f;
            }
            else
            {
                _animationLength = 0f;
                _noCurves.SetActive(true);
                _scrubberRect.gameObject.SetActive(false);
            }
        }

        private void DrawCurveLines()
        {
            _lines.SetVerticesDirty();
        }

        private void BindCurves()
        {
            _lines.ClearCurves();
            var lead = _target.GetLeadCurve();
            _animationLength = lead[lead.length - 1].time;
            if (_target is FreeControllerAnimationTarget)
            {
                var targetController = (FreeControllerAnimationTarget)_target;

                // To display rotation, we have to build custom curves. But it's not that useful.
                // var rotVX = new Keyframe[targetController.RotX.length];
                // var rotVY = new Keyframe[targetController.RotY.length];
                // var rotVZ = new Keyframe[targetController.RotZ.length];
                // for (var t = 0; t < targetController.RotW.length; t++)
                // {
                //     Keyframe keyX = targetController.RotX[t];
                //     Keyframe keyY = targetController.RotY[t];
                //     Keyframe keyZ = targetController.RotZ[t];
                //     Keyframe keyW = targetController.RotW[t];
                //     var rot = new Quaternion(
                //         keyX.value,
                //         keyY.value,
                //         keyZ.value,
                //         keyW.value
                //     );
                //     var eulerAngles = rot.eulerAngles;
                //     rotVX[t] = new Keyframe(keyW.time, eulerAngles.x);
                //     rotVY[t] = new Keyframe(keyW.time, eulerAngles.y);
                //     rotVZ[t] = new Keyframe(keyW.time, eulerAngles.z);
                // }
                // _lines.AddCurve(new Color(1.0f, 0.8f, 0.8f), new AnimationCurve(rotVX));
                // _lines.AddCurve(new Color(0.8f, 1.0f, 0.8f), new AnimationCurve(rotVY));
                // _lines.AddCurve(new Color(0.8f, 0.8f, 1.0f), new AnimationCurve(rotVZ));

                _lines.range = EstimateRange(targetController.X, targetController.Y, targetController.Z);
                _lines.AddCurve(Color.red, targetController.X);
                _lines.AddCurve(Color.green, targetController.Y);
                _lines.AddCurve(Color.blue, targetController.Z);
            }
            else if (_target is FloatParamAnimationTarget)
            {
                var targetParam = (FloatParamAnimationTarget)_target;
                _lines.range = EstimateRange(targetParam.Value);
                _lines.AddCurve(Color.white, targetParam.Value);
            }

            _lines.SetVerticesDirty();
        }

        private Vector2 EstimateRange(params AnimationCurve[] curves)
        {
            var boundsEvalPrecision = 20f; // Check how many points to detect highest value
            var minY = float.MaxValue;
            var maxY = float.MinValue;
            var maxX = curves[0][curves.Length - 1].time;
            var boundsTestStep = maxX / boundsEvalPrecision;
            foreach (var curve in curves)
            {
                if (curve.length == 0) continue;
                for (var time = 0f; time < maxX; time += boundsTestStep)
                {
                    var value = curve.Evaluate(time);
                    minY = Mathf.Min(minY, value);
                    maxY = Mathf.Max(maxY, value);
                }
            }
            return new Vector2(minY, maxY);
        }

        private void Unbind()
        {
            if (_target != null)
            {
                _lines.ClearCurves();
                _target.AnimationKeyframesModified.RemoveListener(OnAnimationCurveModified);
                _animation = null;
                _target = null;
                _lines.SetVerticesDirty();
            }
        }

        private void OnAnimationCurveModified()
        {
            StartCoroutine(DelayedUpdate());
        }

        private IEnumerator DelayedUpdate()
        {
            // Allow curve refresh;
            yield return 0;
            yield return 0;
            if (_target != null)
                DrawCurveLines();
        }

        public void Update()
        {
            if (_animation == null) return;
            if (_animation.Time == _time) return;

            _time = _animation.Time;
            var ratio = Mathf.Clamp01(_animation.Time / _animationLength);
            _scrubberRect.anchorMin = new Vector2(ratio, 0);
            _scrubberRect.anchorMax = new Vector2(ratio, 1);
        }

        public void OnDestroy()
        {
            _target?.AnimationKeyframesModified.RemoveListener(OnAnimationCurveModified);
        }
    }
}
