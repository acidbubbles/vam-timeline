using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace VamTimeline
{
    public class TriggersTargetFrame : TargetFrameBase<TriggersAnimationTarget>, TriggerHandler
    {
        public Transform popupParent;

        private UIDynamicButton _editTriggersButton;

        public TriggersTargetFrame()
            : base()
        {
        }

        protected override void CreateCustom()
        {
            if (!expanded) StartCoroutine(ToggleExpandedDeferred());
        }

        private IEnumerator ToggleExpandedDeferred()
        {
            yield return 0;
            if (!expanded) ToggleExpanded();
        }

        public override void SetTime(float time, bool stopped)
        {
            base.SetTime(time, stopped);

            if (stopped)
            {
                AtomAnimationTrigger trigger;
                var ms = plugin.animation.clipTime.ToMilliseconds();
                if (target.triggersMap.TryGetValue(ms, out trigger))
                {
                    valueText.text = string.IsNullOrEmpty(trigger.displayName) ? "Has Triggers" : trigger.displayName;
                    if (_editTriggersButton != null) _editTriggersButton.label = "Edit Triggers";
                }
                else
                {
                    valueText.text = "-";
                    if (_editTriggersButton != null) _editTriggersButton.label = "Create Trigger";
                }
            }
        }

        protected override void ToggleKeyframeImpl(float time, bool enable)
        {
            if (enable)
            {
                GetOrCreateTriggerAtCurrentTime();
            }
            else
            {
                target.DeleteFrame(time);
            }
            SetTime(time, true);
        }

        protected override void CreateExpandPanel(RectTransform container)
        {
            var group = container.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = 4f;
            group.padding = new RectOffset(8, 8, 8, 8);
            group.childAlignment = TextAnchor.MiddleCenter;

            _editTriggersButton = CreateExpandButton(
                group.transform,
                target.triggersMap.ContainsKey(plugin.animation.clipTime.ToMilliseconds()) ? "Edit Triggers" : "Create Trigger",
                EditTriggers);
        }

        private void EditTriggers()
        {
            if (plugin.animation.isPlaying) return;

            var trigger = GetOrCreateTriggerAtCurrentTime();

            trigger.handler = this;
            trigger.triggerActionsParent = popupParent;
            trigger.atom = plugin.containingAtom;
            trigger.InitTriggerUI();
            trigger.OpenTriggerActionsPanel();
            // When already open but in the wrong parent:
            trigger.triggerActionsPanel.transform.SetParent(popupParent, false);
        }

        private AtomAnimationTrigger GetOrCreateTriggerAtCurrentTime()
        {
            AtomAnimationTrigger trigger;
            var ms = plugin.animation.clipTime.Snap().ToMilliseconds();
            if (!target.triggersMap.TryGetValue(ms, out trigger))
            {
                trigger = new AtomAnimationTrigger();
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
