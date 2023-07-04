using System;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace VamTimeline
{
    public class SimpleTrigger : TriggerHandler
    {
        private readonly string _startName;
        private readonly string _stopName;
        public Trigger trigger { get; }

        public SimpleTrigger(string startName, string stopName)
        {
            _startName = startName;
            _stopName = stopName;

            trigger = new Trigger
            {
                handler = this
            };
            SuperController.singleton.onAtomUIDRenameHandlers += OnAtomRename;
        }

        public void RemoveTrigger(Trigger _)
        {
        }

        public void DuplicateTrigger(Trigger _)
        {
        }

        public RectTransform CreateTriggerActionsUI()
        {
            var rt = Object.Instantiate(VamPrefabFactory.triggerActionsPrefab);

            var content = rt.Find("Content");
            var transitionTab = content.Find("Tab2");
            transitionTab.parent = null;
            Object.Destroy(transitionTab);
            var startTab = content.Find("Tab1");
            startTab.GetComponentInChildren<Text>().text = _startName;
            var endTab = content.Find("Tab3");
            if (_stopName != null)
            {
                var endTabRect = endTab.GetComponent<RectTransform>();
                endTabRect.offsetMin = new Vector2(264, endTabRect.offsetMin.y);
                endTabRect.offsetMax = new Vector2(560, endTabRect.offsetMax.y);
                endTab.GetComponentInChildren<Text>().text = _stopName;
            }
            else
            {
                endTab.gameObject.SetActive(false);
            }

            return rt;
        }

        public RectTransform CreateTriggerActionMiniUI()
        {
            var rt = Object.Instantiate(VamPrefabFactory.triggerActionMiniPrefab);
            return rt;
        }

        public RectTransform CreateTriggerActionDiscreteUI()
        {
            var rt = Object.Instantiate(VamPrefabFactory.triggerActionDiscretePrefab);
            return rt;
        }

        public RectTransform CreateTriggerActionTransitionUI()
        {
            return null;
        }

        public void RemoveTriggerActionUI(RectTransform rt)
        {
            if (rt != null) Object.Destroy(rt.gameObject);
        }

        private void OnAtomRename(string oldname, string newname)
        {
            trigger.SyncAtomNames();
        }

        public void Dispose()
        {
            SuperController.singleton.onAtomUIDRenameHandlers -= OnAtomRename;
        }

        public void SetActive(bool active)
        {
            try
            {
                trigger.active = active;
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline: Error while activating global trigger: {exc}");
            }
        }

        public void Trigger()
        {
            try
            {
                trigger.active = true;
                trigger.active = false;
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline: Error while activating global trigger: {exc}");
            }
        }

        public void Update()
        {
            trigger.Update();
        }
    }
}
