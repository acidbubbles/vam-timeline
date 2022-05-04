using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VamTimeline
{
    public class FreeControllerV2AnimationTargetFrameComponent : AnimationTargetFrameComponentBase<FreeControllerV3AnimationTarget>
    {
        private static readonly Shader _gizmoShader = Shader.Find("Battlehub/RTGizmos/Handles");

        protected override bool enableValueText => true;
        protected override bool enableLabel => true;

        protected override float expandSize => 140f;
        private LineDrawer _line;
        private int _lastHandlesCount;
        private readonly List<Transform> _handles = new List<Transform>();

        protected override void CreateCustom()
        {
            target.animatableRef.onSelectedChanged.AddListener(OnSelectedChanged);
            target.onAnimationKeyframesRebuilt.AddListener(OnAnimationKeyframesRebuilt);
            OnSelectedChanged();
        }

        private void OnSelectedChanged()
        {
            if (!plugin.animationEditContext.showPaths) return;

            if (!target.selected && _line != null)
            {
                Destroy(_line.gameObject);
                foreach (var t in _handles)
                    Destroy(t.gameObject);
                _handles.Clear();
                _line = null;
            }
            else if (target.selected && _line == null)
            {
                if (target.animatableRef.owned || target.animatableRef.controller != null)
                {
                    _line = CreateLine();
                    UpdateLine();
                }
            }
        }

        private void OnAnimationKeyframesRebuilt()
        {
            if (!target.selected) return;
            if (_line != null) UpdateLine();
        }

        private LineDrawer CreateLine()
        {
            var go = new GameObject();
            // ReSharper disable once Unity.NoNullCoalescing
            var parent = target.GetPositionParentRB()?.transform ?? target.animatableRef.controller.control.parent;
            go.transform.SetParent(parent.transform, false);

            var line = go.AddComponent<LineDrawer>();
            line.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(new Color(0f, 0.2f, 0.8f), 0f), new GradientColorKey(new Color(0.6f, 0.65f, 0.95f), 1f) }
            };
            line.material = new Material(_gizmoShader);
            AssignPreviewMaterial(line.material, new Color(1, 1, 1, 0.3f));

            return line;
        }

        private void UpdateLine()
        {
            CancelInvoke(nameof(UpdateLineAsync));
            Invoke(nameof(UpdateLineAsync), 0.001f);
        }

        private void UpdateLineAsync()
        {
            const float pointsPerSecond = 32f;
            var pointsToDraw = Mathf.CeilToInt(target.x.GetKeyframeByKey(target.x.length - 1).time * pointsPerSecond) + 1;
            var points = new List<Vector3>(pointsToDraw + 1);

            var lastPoint = Vector3.positiveInfinity;
            const float minMagnitude = 0.0001f;
            for (var t = 0; t < pointsToDraw; t++)
            {
                var point = target.EvaluatePosition(t / pointsPerSecond);
                if (Vector3.SqrMagnitude(lastPoint - point) < minMagnitude)
                    continue;
                points.Add(point);
                lastPoint = point;
            }
            points.Add(clip.loop ? points[0] : target.EvaluatePosition(clip.animationLength));

            _line.points = points.ToArray();

            var handlesCount = target.x.length - (clip.loop ? 1 : 0);
            if (_lastHandlesCount == handlesCount)
            {
                // TODO: This is incorrect, if we move keyframes in a way that would make the handle visible, the handle won't show up
                for (var t = 0; t < _handles.Count; t++)
                {
                    var handle = _handles[t];
                    handle.GetComponent<Renderer>().material.color = _line.colorGradient.Evaluate(t / clip.animationLength);
                    handle.transform.localPosition = target.EvaluatePosition(target.x.GetKeyframeByKey(t).time);
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
                        var localPosition = target.EvaluatePosition(target.x.GetKeyframeByKey(t).time);
                        if (t != lastHandle && Vector3.SqrMagnitude(lastLocalPosition - localPosition) < minHandleMagnitude)
                            continue;
                        var handle = Instantiate(TimelinePrefabs.cube, _line.transform, false);
                        AssignPreviewMaterial(handle.GetComponent<Renderer>().material, _line.colorGradient.Evaluate(t / clip.animationLength));
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

        protected override void CreateExpandPanel(RectTransform container)
        {
            var group = container.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = 4f;
            group.padding = new RectOffset(8, 8, 8, 8);
            group.childAlignment = TextAnchor.MiddleCenter;

            var row1 = new GameObject();
            row1.transform.SetParent(group.transform, false);
            row1.AddComponent<LayoutElement>().preferredHeight = 70f;
            row1.AddComponent<HorizontalLayoutGroup>();

            CreateExpandButton(row1.transform, "Select", () => target.SelectInVam());

            CreateExpandButton(row1.transform, "Parenting & more", () =>
            {
                plugin.ChangeScreen(ControllerTargetSettingsScreen.ScreenName, target.name);
            });

            var row2 = new GameObject();
            row2.transform.SetParent(group.transform, false);
            row2.AddComponent<HorizontalLayoutGroup>();

            var posJSON = new JSONStorableBool("Position", target.controlPosition, val => target.controlPosition = val);
            CreateExpandToggle(row2.transform, posJSON);
            var rotJSON = new JSONStorableBool("Rotation", target.controlRotation, val => target.controlRotation = val);
            CreateExpandToggle(row2.transform, rotJSON);

        }

        public override void SetTime(float time, bool stopped)
        {
            base.SetTime(time, stopped);

            if (stopped)
            {
                if (!target.animatableRef.owned && target.animatableRef.controller == null)
                {
                    valueText.text = "[Deleted]";
                }
                else
                {
                    var pos = target.animatableRef.controller.transform.position;
                    valueText.text = $"x: {pos.x:0.000} y: {pos.y:0.000} z: {pos.z:0.000}";
                }
            }
        }

        protected override void ToggleKeyframeImpl(float time, bool on, bool mustBeOn)
        {
            if (on)
            {
                if (plugin.animationEditContext.autoKeyframeAllControllers)
                {
                    foreach (var target1 in clip.targetControllers)
                        SetControllerKeyframe(time, target1);
                }
                else
                {
                    SetControllerKeyframe(time, target);
                }
            }
            else
            {
                if (plugin.animationEditContext.autoKeyframeAllControllers)
                {
                    foreach (var target1 in clip.targetControllers)
                        target1.DeleteFrame(time);
                }
                else
                {
                    target.DeleteFrame(time);
                }
            }
        }

        private void SetControllerKeyframe(float time, FreeControllerV3AnimationTarget target)
        {
            var key = plugin.animationEditContext.SetKeyframeToCurrentTransform(target, time);
            var keyframe = target.x.keys[key];
            if (keyframe.curveType == CurveTypeValues.CopyPrevious)
                target.ChangeCurveByTime(time, CurveTypeValues.SmoothLocal);
        }

        public override void OnDestroy()
        {
            target.animatableRef.onSelectedChanged.RemoveListener(OnSelectedChanged);
            if (_line != null) Destroy(_line.gameObject);
            foreach (var t in _handles)
                Destroy(t.gameObject);
            _handles.Clear();
            base.OnDestroy();
        }
    }
}
