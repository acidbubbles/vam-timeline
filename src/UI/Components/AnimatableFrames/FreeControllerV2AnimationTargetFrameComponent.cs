using UnityEngine;
using UnityEngine.UI;

namespace VamTimeline
{
    public class FreeControllerV2AnimationTargetFrameComponent : AnimationTargetFrameComponentBase<FreeControllerV3AnimationTarget>
    {
        protected override bool enableValueText => true;
        protected override bool enableLabel => true;

        protected override float expandSize => 140f;

        protected override void CreateCustom()
        {
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
                plugin.ChangeScreen(ControllerTargetSettingsScreen.ScreenName, target.animatableRef.controller);
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
                    valueText.text = "[Missing]";
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
    }
}
