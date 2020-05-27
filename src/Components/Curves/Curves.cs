using System.Collections.Generic;
using System.Linq;
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
        private readonly GameObject _noCurves;
        private readonly CurvesLines _lines;

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

        public void Bind(List<AnimationCurve> curves)
        {
            _noCurves.SetActive(curves == null);
            // TODO: Assign target instead to drive colors
            // TODO: Update when the curve is changed (event)
            _lines.curves = curves?.Select(c => new KeyValuePair<Color, AnimationCurve>(Color.black, c)).ToList();
        }
    }
}
