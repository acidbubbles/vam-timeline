using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace VamTimeline
{
    public class ScreenTabs : MonoBehaviour
    {
        public class TabSelectedEvent : UnityEvent<string> { }

        public static ScreenTabs Create(Transform parent, Transform buttonPrefab)
        {
            var go = new GameObject();
            go.transform.SetParent(parent, false);

            go.AddComponent<LayoutElement>().minHeight = 60f;

            var group = go.AddComponent<HorizontalLayoutGroup>();
            group.spacing = 4f;
            group.childForceExpandWidth = true;
            group.childControlHeight = false;

            var tabs = go.AddComponent<ScreenTabs>();
            tabs.buttonPrefab = buttonPrefab;

            return tabs;
        }

        public TabSelectedEvent onTabSelected = new TabSelectedEvent();
        public List<UIDynamicButton> tabs = new List<UIDynamicButton>();
        public Transform buttonPrefab;

        public UIDynamicButton Add(string name)
        {
            var rt = Instantiate(buttonPrefab);
            rt.SetParent(transform, false);

            var btn = rt.gameObject.GetComponent<UIDynamicButton>();
            btn.label = name;

            btn.button.onClick.AddListener(() =>
            {
                Select(name);
                onTabSelected.Invoke(name);
            });

            tabs.Add(btn);

            return btn;
        }

        public void Select(string screenName)
        {
            foreach (var btn in tabs)
            {
                btn.button.interactable = btn.label != screenName;
            }
        }
    }
}
