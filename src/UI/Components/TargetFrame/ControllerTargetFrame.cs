using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VamTimeline
{
    public class ControllerTargetFrame : TargetFrameBase<FreeControllerAnimationTarget>
    {
        protected override float expandSize => 140f;
        private LineDrawer _line;
        private readonly List<GameObject> _handles = new List<GameObject>();

        protected override void CreateCustom()
        {
            plugin.animationEditContext.onTargetsSelectionChanged.AddListener(OnSelectedChanged);
            target.onAnimationKeyframesRebuilt.AddListener(OnAnimationKeyframesRebuilt);
            OnSelectedChanged();
        }

        private void OnSelectedChanged()
        {
            if (!plugin.animationEditContext.showPaths) return;

            var selected = plugin.animationEditContext.IsSelected(target);
            if (!selected && _line != null)
            {
                Destroy(_line);
                foreach (var t in _handles)
                    Destroy(t);
                _handles.Clear();
                _line = null;
            }
            else if (selected && _line == null)
            {
                _line = CreateLine();
                UpdateLine();
            }
        }

        private void OnAnimationKeyframesRebuilt()
        {
            if (!plugin.animationEditContext.IsSelected(target)) return;
            if (_line != null) UpdateLine();
        }

        private LineDrawer CreateLine()
        {
            var go = new GameObject();
            var parent = target.GetParent();
            if (parent == null) return null;
            go.transform.SetParent(parent, false);

            var line = go.AddComponent<LineDrawer>();
            line.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(new Color(0f, 0.2f, 0.8f), 0f), new GradientColorKey(new Color(0.6f, 0.65f, 0.95f), 1f) }
            };
            line.material = GetPreviewMaterial(Color.white);

            return line;
        }

        private void UpdateLine()
        {
            const float pointsPerSecond = 32f;
            var pointsCount = Mathf.CeilToInt(target.x.GetKeyframeByKey(target.x.length - 1).time * pointsPerSecond) + (clip.loop ? 2 : 1);
            var points = new Vector3[pointsCount];

            for (var t = 0; t < pointsCount - (clip.loop ? 1 : 0); t++)
            {
                points[t] = target.EvaluatePosition(t / pointsPerSecond);
            }
            if (clip.loop)
                points[pointsCount - 1] = points[0];

            _line.points = points;

            var handlesCount = target.x.length - (clip.loop ? 1 : 0);
            if (_handles.Count == handlesCount)
            {
                for (var t = 0; t < handlesCount; t++)
                {
                    var handle = _handles[t];
                    handle.GetComponent<Renderer>().material.color = _line.colorGradient.Evaluate(t / clip.animationLength);
                    handle.transform.localPosition = target.EvaluatePosition(target.x.GetKeyframeByKey(t).time);
                }
            }
            else
            {
                foreach (var t in _handles)
                    Destroy(t);
                _handles.Clear();

                for (var t = 0; t < handlesCount; t++)
                {
                    var handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    handle.GetComponent<Renderer>().material = GetPreviewMaterial(_line.colorGradient.Evaluate(t / clip.animationLength));
                    foreach (var c in handle.GetComponents<Collider>()) { c.enabled = false; Destroy(c); }
                    handle.transform.SetParent(_line.transform, false);
                    handle.transform.localScale = Vector3.one * 0.01f;
                    handle.transform.localPosition = target.EvaluatePosition(target.x.GetKeyframeByKey(t).time);
                    _handles.Add(handle);
                }
            }
        }

        private static Material GetPreviewMaterial(Color color)
        {
            var mat = new Material(Shader.Find("Battlehub/RTGizmos/Handles")) { color = color };
            mat.SetFloat("_Offset", 1f);
            mat.SetFloat("_MinAlpha", 1f);
            mat.SetPass(0);
            return mat;
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
                var pos = target.controller.transform.position;
                valueText.text = $"x: {pos.x:0.000} y: {pos.y:0.000} z: {pos.z:0.000}";
            }
        }

        protected override void ToggleKeyframeImpl(float time, bool enable)
        {
            if (enable)
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

        private void SetControllerKeyframe(float time, FreeControllerAnimationTarget target)
        {
            var key = plugin.animationEditContext.SetKeyframeToCurrentTransform(target, time);
            var keyframe = target.x.keys[key];
            if (keyframe.curveType == CurveTypeValues.CopyPrevious)
                target.ChangeCurve(time, CurveTypeValues.SmoothLocal);
        }

        public override void OnDestroy()
        {
            plugin.animationEditContext.onTargetsSelectionChanged.RemoveListener(OnSelectedChanged);
            if (_line != null) Destroy(_line.gameObject);
            foreach (var t in _handles)
                Destroy(t);
            _handles.Clear();
            base.OnDestroy();
        }
    }
}
