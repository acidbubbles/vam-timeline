using UnityEngine;
using UnityEngine.UI;

namespace VamTimeline
{
    public class Editor : MonoBehaviour
    {
        public static Editor AddTo(RectTransform transform)
        {
            var go = new GameObject();
            go.transform.SetParent(transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.pivot = new Vector2(0, 1);

            // var layout = go.AddComponent<LayoutElement>();
            // layout.minHeight = 900f;
            // layout.preferredWidth = 1060f;

            var group = go.AddComponent<HorizontalLayoutGroup>();
            group.spacing = 10f;

            var leftPanel = CreatePanel(go.transform);
            var rightPanel = CreatePanel(go.transform);

            var editor = go.AddComponent<Editor>();
            editor.leftPanel = leftPanel;
            editor.rightPanel = rightPanel;

            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return editor;
        }

        private static GameObject CreatePanel(Transform transform)
        {
            var go = new GameObject();
            go.transform.SetParent(transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.pivot = new Vector2(0, 1f);

            var verticalLayoutGroup = go.AddComponent<VerticalLayoutGroup>();
            verticalLayoutGroup.childControlWidth = true;
            verticalLayoutGroup.childForceExpandWidth = false;
            verticalLayoutGroup.childControlHeight = true;
            verticalLayoutGroup.childForceExpandHeight = false;
            verticalLayoutGroup.spacing = 10f;

            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return go;
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

            InitClipboardUI();

            InitChangeCurveTypeUI();

            _curves = InitCurvesUI();

            InitAutoKeyframeUI();

            _screensManager = CreateScreensManager(rightPanel);
            _screensManager.Bind(plugin);
        }

        private static AnimationControlPanel CreateControlPanel(GameObject panel)
        {
            var go = new GameObject();
            go.transform.SetParent(panel.transform, false);

            var layout = go.AddComponent<LayoutElement>();
            layout.minHeight = 680f;

            return AnimationControlPanel.Configure(go);
        }

        private void InitChangeCurveTypeUI()
        {
            _curveType = CurveTypePopup.Create(_leftPanelPrefabFactory);
        }

        private Curves InitCurvesUI()
        {
            var go = new GameObject();
            go.transform.SetParent(leftPanel.transform, false);

            var layout = go.AddComponent<LayoutElement>();
            layout.minHeight = 270f;
            layout.flexibleWidth = 100f;

            return go.AddComponent<Curves>();
        }

        protected void InitClipboardUI()
        {
            var container = _leftPanelPrefabFactory.CreateSpacer();
            container.height = 48f;

            var group = container.gameObject.AddComponent<HorizontalLayoutGroup>();
            group.spacing = 4f;
            group.childForceExpandWidth = true;
            group.childControlHeight = false;

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
