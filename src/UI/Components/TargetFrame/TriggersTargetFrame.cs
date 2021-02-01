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
        private AtomAnimationTrigger _trigger;

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
                if (!ReferenceEquals(_trigger, null) && _trigger.startTime != time)
                    CloseTriggersPanel();

                AtomAnimationTrigger trigger;
                var ms = plugin.animationEditContext.clipTime.ToMilliseconds();
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
                target.triggersMap.ContainsKey(plugin.animationEditContext.clipTime.ToMilliseconds()) ? "Edit Triggers" : "Create Trigger",
                EditTriggers);
        }

        private void EditTriggers()
        {
            if (!plugin.animationEditContext.CanEdit()) return;

            _trigger = GetOrCreateTriggerAtCurrentTime();

            _trigger.handler = this;
            _trigger.triggerActionsParent = popupParent;
            _trigger.atom = plugin.containingAtom;
            _trigger.InitTriggerUI();
            _trigger.OpenTriggerActionsPanel();
            // When already open but in the wrong parent:
            _trigger.triggerActionsPanel.transform.SetParent(popupParent, false);
            // When open behind another atom's panel in Controller:
            _trigger.triggerActionsPanel.SetAsLastSibling();
        }

        private AtomAnimationTrigger GetOrCreateTriggerAtCurrentTime()
        {
            AtomAnimationTrigger trigger;
            var ms = plugin.animationEditContext.clipTime.Snap().ToMilliseconds();
            if (!target.triggersMap.TryGetValue(ms, out trigger))
            {
                trigger = new AtomAnimationTrigger();
                target.SetKeyframe(ms, trigger);
            }
            return trigger;
        }

        public void OnDisable()
        {
            CloseTriggersPanel();
        }

        private void CloseTriggersPanel()
        {
            if (_trigger == null) return;
            if (_trigger.triggerActionsPanel != null)
                _trigger.triggerActionsPanel.gameObject.SetActive(false);
            _trigger = null;
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
            if (rt != null) Destroy(rt.gameObject);
        }

        #endregion
    }
}
