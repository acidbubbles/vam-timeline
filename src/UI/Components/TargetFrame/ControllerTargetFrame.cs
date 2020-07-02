using UnityEngine;
using UnityEngine.UI;

namespace VamTimeline
{
    public class ControllerTargetFrame : TargetFrameBase<FreeControllerAnimationTarget>
    {
        private LineRenderer _line;
        private Shader _shader;

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

        private LineRenderer CreateLine()
        {
            var go = new GameObject();
            go.transform.SetParent(target.controller.control, false);

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            if (_shader == null) _shader = Shader.Find("Sprites/Default");
            line.material = new Material(_shader) { renderQueue = 4000 };
            line.widthMultiplier = 0.002f;
            line.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(new Color(0f, 0.2f, 0.8f), 0f), new GradientColorKey(new Color(0.4f, 0.4f, 0.9f), 1f) }
            };

            return line;
        }

        private void UpdateLine()
        {
            var pointsPerSecond = 16f;
            var pointsCount = Mathf.CeilToInt(target.x[target.x.length - 1].time * pointsPerSecond);
            var points = new Vector3[pointsCount];

            for (var t = 0; t < pointsCount; t++)
            {
                points[t] = target.EvaluatePosition(t / pointsPerSecond);
            }

            _line.positionCount = pointsCount;
            _line.SetPositions(points);
        }

        protected override void CreateExpandPanel(RectTransform container)
        {
            var group = container.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = 4f;
            group.padding = new RectOffset(8, 8, 8, 8);
            group.childAlignment = TextAnchor.MiddleCenter;

            CreateExpandButton(group.transform, "Select", () => SuperController.singleton.SelectController(target.controller));
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
                if (plugin.autoKeyframeAllControllersJSON.val)
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
                if (plugin.autoKeyframeAllControllersJSON.val)
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
            plugin.animation.SetKeyframeToCurrentTransform(target, time);
            if (target.settings[time.ToMilliseconds()]?.curveType == CurveTypeValues.CopyPrevious)
                target.ChangeCurve(time, CurveTypeValues.Smooth);
        }

        public override void OnDestroy()
        {
            target.onSelectedChanged.RemoveListener(OnSelectedChanged);
            Destroy(_line?.gameObject);
            base.OnDestroy();
        }
    }
}
