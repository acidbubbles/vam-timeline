using System;
using UnityEngine;
using UnityEngine.UI;

namespace VamTimeline
{
    public class TriggersTrackAnimationTargetFrameComponent : AnimationTargetFrameComponentBase<TriggersTrackAnimationTarget>, TriggerHandler
    {
        protected override float expandSize => 116;
        protected override bool enableValueText => false;
        protected override bool enableLabel => false;

        public Transform popupParent;

        private UIDynamicButton _editTriggersButton;
        private CustomTrigger _trigger;

        private int _lastCount = -1;
        private string _hasTriggerLabel = "", _noTriggerLabel;
        private Color _hasTriggerColor, _noTriggerColor;

        protected override void CreateCustom()
        {
            CreateEditTriggerButton();
        }

        private void CreateEditTriggerButton()
        {
            _noTriggerLabel = $"{target.animatableRef.name} (N/A)";
            _editTriggersButton = CreateExpandButton(
                transform,
                "",
                EditTriggers);
            _hasTriggerColor = new Color(0.6f, 0.9f, 0.6f);
            _noTriggerColor = _editTriggersButton.buttonColor;
            SyncEditButton(clip.clipTime);
            Destroy(_editTriggersButton.GetComponent<LayoutElement>());
            var rect = _editTriggersButton.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.sizeDelta = new Vector2(-100f, 48f);
            rect.anchoredPosition = new Vector2(8f, -30f);

        }

        public override void SetTime(float time, bool stopped)
        {
            base.SetTime(time, stopped);

            if (stopped)
                SyncEditButton(time);
        }

        private void SyncEditButton(float time)
        {
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (!ReferenceEquals(_trigger, null) && _trigger.startTime != time)
                CloseTriggersPanel();

            if (_editTriggersButton != null)
            {
                var ms = time.ToMilliseconds();
                CustomTrigger trigger;
                if (target.triggersMap.TryGetValue(ms, out trigger))
                {
                    var count = trigger.Count();
                    if (count != _lastCount)
                    {
                        _hasTriggerLabel = $"{target.animatableRef.name} ({count})";
                        _lastCount = count;
                    }

                    _editTriggersButton.label = _hasTriggerLabel;
                    _editTriggersButton.buttonColor = _hasTriggerColor;
                }
                else
                {
                    _editTriggersButton.label = _noTriggerLabel;
                    _editTriggersButton.buttonColor = _noTriggerColor;
                }
            }
        }

        protected override void ToggleKeyframeImpl(float time, bool on, bool mustBeOn)
        {
            if (on)
            {
                GetOrCreateTriggerAtCurrentTime();
            }
            else
            {
                target.DeleteFrame(time);
            }
            SyncEditButton(time);
        }

        protected override void CreateExpandPanel(RectTransform container)
        {
            var group = container.gameObject.AddComponent<VerticalLayoutGroup>();
            group.spacing = 4f;
            group.padding = new RectOffset(8, 8, 8, 8);
            group.childAlignment = TextAnchor.MiddleCenter;

            var liveJSON = new JSONStorableBool("Live scrubbing (layer)", target.animatableRef.live, val => target.animatableRef.live = val);
            CreateExpandToggle(group.transform, liveJSON);

            var nameJSON = new JSONStorableString("Name", target.animatableRef.name);
            nameJSON.setCallbackFunction = val =>
            {
                target.animatableRef.SetName(val);
                foreach (var c in plugin.animation.clips)
                {
                    c.targetTriggers.Sort(new TriggersTrackAnimationTarget.Comparer());
                }
                _noTriggerLabel = $"{target.animatableRef.name} (N/A)";
                _lastCount = -1;
                SyncEditButton(clip.clipTime);
                clip.onTargetsListChanged.Invoke();
            };
            CreateExpandTextInput(group.transform, nameJSON);
        }

        private void EditTriggers()
        {
            if (!plugin.animationEditContext.CanEdit()) return;

            _trigger = GetOrCreateTriggerAtCurrentTime();

            _trigger.handler = this;
            _trigger.triggerActionsParent = popupParent;
            _trigger.atom = plugin.containingAtom;
            _trigger.InitTriggerUI();
            // NOTE: Because everything is protected/private in VaM, I cannot use CheckMissingReceiver
            _trigger.OpenTriggerActionsPanel();
            // When already open but in the wrong parent:
            _trigger.SetPanelParent(popupParent);

            _editTriggersButton.label = $"{target.animatableRef.name} (edited)";
        }

        private CustomTrigger GetOrCreateTriggerAtCurrentTime()
        {
            CustomTrigger trigger;
            var ms = plugin.animationEditContext.clipTime.Snap().ToMilliseconds();
            if (!target.triggersMap.TryGetValue(ms, out trigger))
            {
                trigger = target.CreateKeyframe(ms);
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
            _trigger.ClosePanel();
            _trigger = null;
        }

        #region Trigger handler

        void TriggerHandler.RemoveTrigger(Trigger t)
        {
            throw new NotImplementedException(nameof(TriggerHandler.RemoveTrigger));
        }

        void TriggerHandler.DuplicateTrigger(Trigger t)
        {
            throw new NotImplementedException(nameof(TriggerHandler.DuplicateTrigger));
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
