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

        public readonly TabSelectedEvent onTabSelected = new TabSelectedEvent();
        public List<UIDynamicButton> tabs = new List<UIDynamicButton>();
        public Transform buttonPrefab;

        public UIDynamicButton Add(string name, Color color, string label = null, float preferredWidth = 0)
        {
            var rt = Instantiate(buttonPrefab, transform, false);

            var btn = rt.gameObject.GetComponent<UIDynamicButton>();
            btn.name = name;
            btn.label = label ?? name;
            btn.buttonColor = color;
            btn.buttonText.fontSize = 26;

            if (preferredWidth > 0)
            {
                var layout = btn.gameObject.GetComponent<LayoutElement>();
                layout.minWidth = preferredWidth;
                layout.preferredWidth = preferredWidth;
            }

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
                btn.button.interactable = btn.name != screenName;
            }
        }
    }
}
