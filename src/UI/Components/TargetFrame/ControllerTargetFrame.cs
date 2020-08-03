using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace VamTimeline
{
    public class ControllerTargetFrame : TargetFrameBase<FreeControllerAnimationTarget>
    {
        private LineDrawer _line;
        private readonly List<GameObject> _handles = new List<GameObject>();

        public ControllerTargetFrame()
            : base()
        {
        }

        protected override void CreateCustom()
        {
            target.onSelectedChanged.AddListener(OnSelectedChanged);
            target.onAnimationKeyframesRebuilt.AddListener(OnAnimationKeyframesRebuilt);
            OnSelectedChanged();
        }

        private void OnSelectedChanged()
        {
            if (!target.selected && _line != null)
            {
                Destroy(_line);
                foreach (var t in _handles)
                    Destroy(t);
                _handles.Clear();
                _line = null;
            }
            else if (target.selected && _line == null)
            {
                _line = CreateLine();
                UpdateLine();
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
            go.transform.SetParent(target.GetParent(), false);

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
            var pointsPerSecond = 32f;
            var pointsCount = Mathf.CeilToInt(target.x.GetKeyframe(target.x.length - 1).time * pointsPerSecond) + (clip.loop ? 2 : 1);
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
                    handle.transform.localPosition = target.EvaluatePosition(target.x.GetKeyframe(t).time);
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
                    handle.transform.localPosition = target.EvaluatePosition(target.x.GetKeyframe(t).time);
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
            var group = container.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = 4f;
            group.padding = new RectOffset(8, 8, 8, 8);
            group.childAlignment = TextAnchor.MiddleCenter;

            CreateExpandButton(group.transform, "Select", () => SuperController.singleton.SelectController(target.controller));

            CreateExpandButton(group.transform, "Parent", () =>
            {
                // TODO: Instead make an argument to ChangeScreen
                ControllerParentScreen.target = target;
                plugin.ui.screensManager.ChangeScreen(ControllerParentScreen.ScreenName);
            });
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

        public override void ToggleKeyframe(bool enable)
        {
            if (plugin.animation.isPlaying) return;
            var time = plugin.animation.clipTime.Snap();
            if (time.IsSameFrame(0f) || time.IsSameFrame(clip.animationLength))
            {
                if (!enable)
                    SetToggle(true);
                return;
            }
            if (enable)
            {
                if (plugin.animation.autoKeyframeAllControllers)
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
                if (plugin.animation.autoKeyframeAllControllers)
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
            var key = plugin.animation.SetKeyframeToCurrentTransform(target, time);
            var keyframe = target.x.keys[key];
            if (keyframe.curveType == CurveTypeValues.CopyPrevious)
                target.ChangeCurve(time, CurveTypeValues.Smooth, clip.loop);
        }

        public override void OnDestroy()
        {
            target?.onSelectedChanged.RemoveListener(OnSelectedChanged);
            if (_line != null) Destroy(_line.gameObject);
            foreach (var t in _handles)
                Destroy(t);
            _handles.Clear();
            base.OnDestroy();
        }
    }
}
