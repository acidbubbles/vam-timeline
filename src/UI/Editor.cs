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

            var leftPanel = CreatePanel(go.transform, 0f, 0.5f, 0f);
            var rightPanel = CreatePanel(go.transform, 0.5f, 1f, 1f);

            var editor = go.AddComponent<Editor>();
            editor.leftPanel = leftPanel;
            editor.rightPanel = rightPanel;

            return editor;
        }

        private static GameObject CreatePanel(Transform transform, float xl, float xr, float xa)
        {
            var panel = new GameObject();
            panel.transform.SetParent(transform, false);

            var rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(xl, 1f);
            rect.anchorMax = new Vector2(xr, 1f);
            rect.anchoredPosition = new Vector2(xa, 1f);
            rect.pivot = new Vector2(xa, 1f);
            rect.sizeDelta = new Vector2(-5f, 0f);

            var verticalLayoutGroup = panel.AddComponent<VerticalLayoutGroup>();
            verticalLayoutGroup.childControlWidth = true;
            verticalLayoutGroup.childForceExpandWidth = false;
            verticalLayoutGroup.spacing = 10f;

            return panel;
        }

        public bool locked
        {
            get { return _controlPanel.locked; }
            set { _controlPanel.locked = value; _screensManager.UpdateLocked(value); }
        }

        public GameObject leftPanel;
        public GameObject rightPanel;
        private AnimationControlPanel _controlPanel;
        private IAtomPlugin _plugin;
        private ScreensManager _screensManager;
        private VamPrefabFactory _leftPanelPrefabFactory;
        private Curves _curves;
        private CurveTypePopup _curveType;

        public void Bind(IAtomPlugin plugin)
        {
            _plugin = plugin;

            _leftPanelPrefabFactory = leftPanel.AddComponent<VamPrefabFactory>();
            _leftPanelPrefabFactory.plugin = plugin;

            _controlPanel = CreateControlPanel(leftPanel);
            _controlPanel.Bind(plugin);

            InitChangeCurveTypeUI();

            InitCurvesUI();

            InitClipboardUI();

            InitAutoKeyframeUI();

            _screensManager = CreateScreensManager(rightPanel);
            _screensManager.Bind(plugin);
        }

        private static AnimationControlPanel CreateControlPanel(GameObject panel)
        {
            var go = new GameObject();
            go.transform.SetParent(panel.transform, false);

            var layout = go.AddComponent<LayoutElement>();
            layout.minHeight = 900f;

            return AnimationControlPanel.Configure(go);
        }

        private void InitChangeCurveTypeUI()
        {
            _curveType = CurveTypePopup.Create(_leftPanelPrefabFactory);
        }

        private void InitCurvesUI()
        {
            var spacerUI = _leftPanelPrefabFactory.CreateSpacer();
            spacerUI.height = 300f;

            _curves = spacerUI.gameObject.AddComponent<Curves>();
        }

        protected void InitClipboardUI()
        {
            var container = _leftPanelPrefabFactory.CreateSpacer();
            container.height = 60f;

            var group = container.gameObject.AddComponent<GridLayoutGroup>();
            group.constraint = GridLayoutGroup.Constraint.Flexible;
            group.constraintCount = 3;
            group.spacing = Vector2.zero;
            group.cellSize = new Vector2(512f / 3f, 50f);
            group.childAlignment = TextAnchor.MiddleCenter;

            {
                var btn = Instantiate(_plugin.manager.configurableButtonPrefab).GetComponent<UIDynamicButton>();
                btn.gameObject.transform.SetParent(group.transform, false);
                btn.label = "Cut";
                btn.button.onClick.AddListener(() => _plugin.cutJSON.actionCallback());
            }

            {
                var btn = Instantiate(_plugin.manager.configurableButtonPrefab).GetComponent<UIDynamicButton>();
                btn.gameObject.transform.SetParent(group.transform, false);
                btn.label = "Copy";
                btn.button.onClick.AddListener(() => _plugin.copyJSON.actionCallback());
            }

            {
                var btn = Instantiate(_plugin.manager.configurableButtonPrefab).GetComponent<UIDynamicButton>();
                btn.gameObject.transform.SetParent(group.transform, false);
                btn.label = "Paste";
                btn.button.onClick.AddListener(() => _plugin.pasteJSON.actionCallback());
            }
        }

        private void InitAutoKeyframeUI()
        {
            var autoKeyframeAllControllersUI = _leftPanelPrefabFactory.CreateToggle(_plugin.autoKeyframeAllControllersJSON);
        }

        private ScreensManager CreateScreensManager(GameObject panel)
        {
            var go = new GameObject();
            go.transform.SetParent(panel.transform, false);

            var layout = go.AddComponent<LayoutElement>();

            return ScreensManager.Configure(go);
        }

        public void Bind(AtomAnimation animation)
        {
            _controlPanel.Bind(animation);
            _curveType.Bind(animation);
            _curves.Bind(animation);
        }
    }
}
