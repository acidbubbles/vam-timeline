
using System.Collections.Generic;
using Leap.Unity.Swizzle;
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
    public class DopeSheetCanvas : MaskableGraphic
    {
        private readonly List<TargetRow> _rows = new List<TargetRow>();

        private Vector2 _cameraPosition = Vector2.zero;
        private Matrix4x4 _viewMatrix = Matrix4x4.identity;
        private AtomAnimationClip _clip;

        public Vector2 cameraPosition
        {
            get { return _cameraPosition; }
            set
            {
                _cameraPosition = value;
                UpdateViewMatrix();
                SetVerticesDirty();
            }
        }

        protected override void Awake()
        {
            base.Awake();
            UpdateViewMatrix();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            var viewBounds = GetViewBounds();

            // TODO: Draw row separators and column (second / fourth of a second) separators

            foreach (var row in _rows)
                row.PopulateMesh(vh, _viewMatrix, viewBounds);

            // TODO: Draw scrubber (separate gameobject)
        }

        private void UpdateViewMatrix() => _viewMatrix = Matrix4x4.TRS(_cameraPosition, Quaternion.identity, new Vector3(100, 100, 1));

        public void Draw(AtomAnimationClip clip)
        {
            _clip = clip;
            var height = 0.2f;
            var index = 0;
            foreach (var target in clip.AllTargets)
            {
                var row = new TargetRow(target, height, index * height);
                _rows.Add(row);
                index++;
            }
            SetVerticesDirty();
        }

        private Rect GetViewBounds()
        {
            var viewMin = _viewMatrix.inverse.MultiplyPoint3x4(Vector2.zero).xy();
            var viewMax = _viewMatrix.inverse.MultiplyPoint3x4(rectTransform.sizeDelta).xy();
            return new Rect(viewMin, viewMax - viewMin);
        }
    }
}
