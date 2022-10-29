using System;
using System.Collections.Generic;
using UnityEngine;

namespace VamTimeline
{
    public class ControllerLineDrawer3D : MonoBehaviour
    {
        private static readonly Shader _gizmoShader = Shader.Find("Battlehub/RTGizmos/Handles");
        private FreeControllerV3AnimationTarget _target;
        private int _lastHandlesCount;
        private readonly List<Transform> _handles = new List<Transform>();
        private LineDrawer3D _line;

        public static ControllerLineDrawer3D CreateLine(FreeControllerV3AnimationTarget target)
        {
            var go = new GameObject();
            // ReSharper disable once Unity.NoNullCoalescing
            var parent = target.GetPositionParentRB()?.transform ?? target.animatableRef.controller.control.parent;
            go.transform.SetParent(parent.transform, false);

            var line = go.AddComponent<LineDrawer3D>();
            line.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(new Color(0f, 0.2f, 0.8f), 0f), new GradientColorKey(new Color(0.6f, 0.65f, 0.95f), 1f) }
            };
            line.material = new Material(_gizmoShader);
            AssignPreviewMaterial(line.material, new Color(1, 1, 1, 0.3f));

            var that = go.AddComponent<ControllerLineDrawer3D>();
            that._target = target;
            that._line = line;

            return that;
        }

        public void UpdateLine()
        {
            CancelInvoke(nameof(UpdateLineAsync));
            Invoke(nameof(UpdateLineAsync), 0.001f);
        }

        private void UpdateLineAsync()
        {
            const float pointsPerSecond = 32f;
            var pointsToDraw = Mathf.CeilToInt(_target.x.GetKeyframeByKey(_target.x.length - 1).time * pointsPerSecond) + 1;
            var points = new List<Vector3>(pointsToDraw + 1);

            var lastPoint = Vector3.positiveInfinity;
            const float minMagnitude = 0.0001f;
            for (var t = 0; t < pointsToDraw; t++)
            {
                var point = _target.EvaluatePosition(t / pointsPerSecond);
                if (Vector3.SqrMagnitude(lastPoint - point) < minMagnitude)
                    continue;
                points.Add(point);
                lastPoint = point;
            }

            var leadCurve = _target.GetLeadCurve();
            var animationLength = leadCurve.duration;
            var loop = _target.GetKeyframePosition(0) == _target.GetKeyframePosition(leadCurve.length - 1);
            points.Add(loop ? points[0] : _target.EvaluatePosition(animationLength));

            _line.points = points.ToArray();

            var handlesCount = _target.x.length - (loop ? 1 : 0);
            if (_lastHandlesCount == handlesCount)
            {
                // TODO: This is incorrect, if we move keyframes in a way that would make the handle visible, the handle won't show up
                for (var t = 0; t < _handles.Count; t++)
                {
                    var handle = _handles[t];
                    handle.GetComponent<Renderer>().material.color = _line.colorGradient.Evaluate(t / animationLength);
                    handle.transform.localPosition = _target.EvaluatePosition(_target.x.GetKeyframeByKey(t).time);
                }
            }
            else
            {
                _lastHandlesCount = handlesCount;

                CancelInvoke(nameof(ActivateLine));

                // TODO: Instead of doing Clear, trim excess and set localPosition on existing, only add missing handles
                for (var i = 0; i < _handles.Count; i++) Destroy(_handles[i].gameObject);
                _handles.Clear();
                const int maxKeyframes = 100;

                if (handlesCount < maxKeyframes)
                {
                    _handles.Capacity = Math.Max(handlesCount / 2, 1);

                    var handleScale = Vector3.one * 0.001f;
                    var lastHandle = handlesCount - 1;
                    const float minHandleMagnitude = 0.0005f;

                    var lastLocalPosition = Vector3.positiveInfinity;
                    for (var t = 0; t < handlesCount; t++)
                    {
                        var localPosition = _target.EvaluatePosition(_target.x.GetKeyframeByKey(t).time);
                        if (t != lastHandle && Vector3.SqrMagnitude(lastLocalPosition - localPosition) < minHandleMagnitude)
                            continue;
                        var handle = Instantiate(TimelinePrefabs.cube, _line.transform, false);
                        AssignPreviewMaterial(handle.GetComponent<Renderer>().material, _line.colorGradient.Evaluate(t / animationLength));
                        handle.localScale = handleScale;
                        handle.localPosition = localPosition;
                        _handles.Add(handle);
                        lastLocalPosition = localPosition;
                    }

                    Invoke(nameof(ActivateLine), 0.01f);
                }
                else
                {
                    _handles.Capacity = 0;
                }
            }
        }

        private void ActivateLine()
        {
            if (_handles.Count == 0 || _handles[0] == null) return;

            // NOTE: When activating _immediately_,  it causes the model to react weirdly sometimes.
            for (var i = 0; i < _handles.Count; i++)
            {
                var handle = _handles[i];
                handle.gameObject.SetActive(true);
            }
        }

        private void OnDestroy()
        {
            _handles.Clear();
            _target = null;
        }

        private static void AssignPreviewMaterial(Material material, Color color)
        {
            if (material == null) throw new NullReferenceException("No mat");
            if (material.shader != _gizmoShader)
                material.shader = _gizmoShader;
            material.color = color;
            material.SetFloat("_Offset", 1f);
            material.SetFloat("_MinAlpha", 1f);
            material.SetPass(0);
        }
    }
}
