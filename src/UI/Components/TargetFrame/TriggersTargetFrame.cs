using System;
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
    public class TriggersTargetFrame : TargetFrameBase<TriggersAnimationTarget>, TriggerHandler
    {
        public TriggersTargetFrame()
            : base()
        {
        }

        protected override void CreateCustom()
        {
        }

        public override void SetTime(float time, bool stopped)
        {
            base.SetTime(time, stopped);

            if (stopped)
            {
                Trigger trigger;
                var ms = plugin.animation.clipTime.ToMilliseconds();
                if (target.keyframes.TryGetValue(ms, out trigger))
                {
                    valueText.text = $"{trigger.displayName} Trigger";
                }
                else
                {
                    valueText.text = "-";
                }
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
                GetOrCreateTriggerAtCurrentTime();
            }
            else
            {
                target.DeleteFrame(time);
            }
        }

        protected override void CreateExpandPanel(RectTransform container)
        {
            var group = container.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = 4f;
            group.padding = new RectOffset(8, 8, 8, 8);
            group.childAlignment = TextAnchor.MiddleCenter;

            CreateExpandButton(group.transform, "Edit Triggers", EditTriggers);
        }

        private void EditTriggers()
        {
            Trigger trigger = GetOrCreateTriggerAtCurrentTime();

            trigger.triggerActionsParent = plugin.UITransform;
            trigger.handler = this;
            trigger.OpenTriggerActionsPanel();
        }

        private Trigger GetOrCreateTriggerAtCurrentTime()
        {
            Trigger trigger;
            var ms = plugin.animation.clipTime.ToMilliseconds();
            if (!target.keyframes.TryGetValue(ms, out trigger))
            {
                // TODO: Assign a display name?
                trigger = new Trigger();
                target.SetKeyframe(ms, trigger);
            }
            return trigger;
        }

        #region Trigger handler

        void TriggerHandler.RemoveTrigger(Trigger t)
        {
            throw new NotImplementedException();
        }

        void TriggerHandler.DuplicateTrigger(Trigger t)
        {
            throw new NotImplementedException();
        }

        RectTransform TriggerHandler.CreateTriggerActionsUI()
        {
            return Instantiate(VamPrefabFactory.triggerActionsPrefab);
        }

        RectTransform TriggerHandler.CreateTriggerActionMiniUI()
        {
            return Instantiate(VamPrefabFactory.triggerActionMiniPrefab);
        }

        RectTransform TriggerHandler.CreateTriggerActionDiscreteUI()
        {
            return Instantiate(VamPrefabFactory.triggerActionDiscretePrefab);
        }

        RectTransform TriggerHandler.CreateTriggerActionTransitionUI()
        {
            return Instantiate(VamPrefabFactory.triggerActionTransitionPrefab);
        }

        void TriggerHandler.RemoveTriggerActionUI(RectTransform rt)
        {
            Destroy(rt?.gameObject);
        }

        #endregion
    }
}
