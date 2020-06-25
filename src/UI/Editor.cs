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
    public class Editor : MonoBehaviour
    {
        public static Editor AddTo(RectTransform transform)
        {
            var go = new GameObject();
            go.transform.SetParent(transform, false);

            var layout = go.AddComponent<LayoutElement>();
            layout.minHeight = 900f;
            layout.preferredWidth = 1060f;

            var leftPanel = CreatePanel(go.transform, 0f, 0.5f);
            var rightPanel = CreatePanel(go.transform, 0.5f, 1f);

            var editor = go.AddComponent<Editor>();
            editor._leftPanel = leftPanel;
            editor._rightPanel = leftPanel;

            return editor;
        }

        private static GameObject CreatePanel(Transform transform, float xl, float xr)
        {
            var panel = new GameObject();
            panel.transform.SetParent(transform, false);

            var rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(xl, 1f);
            rect.anchorMax = new Vector2(xr, 1f);
            rect.anchoredPosition = new Vector2(xl, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.sizeDelta = new Vector2(0f, 0f);

            var bg = panel.AddComponent<Image>();
            bg.raycastTarget = false;
            bg.color = new Color(xl, xr, 0.5f);

            var verticalLayoutGroup = panel.AddComponent<VerticalLayoutGroup>();
            verticalLayoutGroup.childControlWidth = true;
            verticalLayoutGroup.childForceExpandWidth = false;

            return panel;
        }

        public bool locked
        {
            get { return _controlPanel.locked; }
            set { _controlPanel.locked = value; }
        }

        private AnimationControlPanel _controlPanel;
        private GameObject _leftPanel;
        private GameObject _rightPanel;

        public void Bind(IAtomPlugin plugin)
        {
            _controlPanel = CreateControlPanel(_leftPanel);
            _controlPanel.Bind(plugin);
        }

        private static AnimationControlPanel CreateControlPanel(GameObject panel)
        {
            var go = new GameObject();
            go.transform.SetParent(panel.transform, false);

            var layout = go.AddComponent<LayoutElement>();
            layout.minHeight = 900f;

            return AnimationControlPanel.Configure(go);
        }

        public void Bind(AtomAnimation animation)
        {
            _controlPanel.Bind(animation);
        }
    }
}
