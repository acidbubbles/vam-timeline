using UnityEngine;
using UnityEngine.UI;

namespace VamTimeline
{
    public class Editor : MonoBehaviour
    {
        public const float RightPanelExpandedWidth = 500f;
        public const float RightPanelCollapsedWidth = 0f;

        public static Editor AddTo(Transform transform)
        {
            var go = new GameObject();
            go.transform.SetParent(transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.StretchTop();
            rect.sizeDelta = new Vector2(0, 750);
            rect.pivot = new Vector2(0, 1);

            return Configure(go);
        }

        public static Editor Configure(GameObject go)
        {
            go.AddComponent<VerticalLayoutGroup>();

            var tabs = ScreenTabs.Create(go.transform, VamPrefabFactory.buttonPrefab);

            var panels = new GameObject();
            panels.transform.SetParent(go.transform, false);

            var panelsGroup = panels.AddComponent<HorizontalLayoutGroup>();
            panelsGroup.spacing = 10f;
            panelsGroup.childControlWidth = true;
            panelsGroup.childForceExpandWidth = false;
            panelsGroup.childControlHeight = true;
            panelsGroup.childForceExpandHeight = false;

            var leftPanel = CreatePanel(panels.transform, 0f, 1f);
            var rightPanel = CreatePanel(panels.transform, RightPanelExpandedWidth, 0f);

            var editor = go.AddComponent<Editor>();
            editor.tabs = tabs;
            editor.leftPanel = leftPanel;
            editor.rightPanel = rightPanel;

            return editor;
        }

        private static GameObject CreatePanel(Transform transform, float preferredWidth, float flexibleWidth)
        {
            var go = new GameObject();
            go.transform.SetParent(transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.pivot = new Vector2(0, 1f);

            var layout = go.AddComponent<LayoutElement>();
            layout.minWidth = 0;
            layout.preferredWidth = preferredWidth;
            layout.flexibleWidth = flexibleWidth;
            layout.minHeight = 100;

            var verticalLayoutGroup = go.AddComponent<VerticalLayoutGroup>();
            verticalLayoutGroup.childControlWidth = true;
            verticalLayoutGroup.childForceExpandWidth = false;
            verticalLayoutGroup.childControlHeight = true;
            verticalLayoutGroup.childForceExpandHeight = false;
            verticalLayoutGroup.spacing = 10f;

            return go;
        }

        public AtomAnimationEditContext animationEditContext;
        public ScreenTabs tabs;
        public GameObject leftPanel;
        public GameObject rightPanel;
        public ScreensManager screensManager;
        public Transform popupParent;
        private AnimationControlPanel _controlPanel;
        private IAtomPlugin _plugin;
        private VamPrefabFactory _leftPanelPrefabFactory;
        private Curves _curves;
        private CurveTypePopup _curveType;
        private bool _expanded = true;
        private UIDynamicButton _expandButton;

        public void Bind(IAtomPlugin plugin, string defaultScreen = null)
        {
            _plugin = plugin;

            _leftPanelPrefabFactory = leftPanel.AddComponent<VamPrefabFactory>();
            _leftPanelPrefabFactory.plugin = plugin;

            _controlPanel = CreateControlPanel(leftPanel);
            _controlPanel.Bind(plugin);

            InitClipboardUI();

            InitChangeCurveTypeUI();

            _curves = InitCurvesUI();

            tabs.Add(AnimationsScreen.ScreenName, new Color(0.780f, 0.780f, 0.899f));
            tabs.Add(AddAnimationScreen.ScreenName, new Color(0.780f, 0.780f, 0.899f), "+", 60f);
            tabs.Add(TargetsScreen.ScreenName, new Color(0.780f, 0.899f, 0.780f));
            tabs.Add(AddRemoveTargetsScreen.ScreenName, new Color(0.780f, 0.899f, 0.780f), "+", 60f);
            tabs.Add(EditAnimationScreen.ScreenName, new Color(0.899f, 0.780f, 0.780f));
            tabs.Add(SequencingScreen.ScreenName, new Color(0.899f, 0.780f, 0.780f));
            tabs.Add(MoreScreen.ScreenName, new Color(0.889f, 0.780f, 0.889f));
            _expandButton = tabs.Add("Collapse >", new Color(0.839f, 0.839f, 0.839f));
            InitToggleRightPanelButton(_expandButton);
            tabs.onTabSelected.AddListener(screenName =>
            {
                screensManager.ChangeScreen(screenName, null);
                Expand(true);
            });

            screensManager = InitScreensManager(rightPanel);
            screensManager.onScreenChanged.AddListener(args => tabs.Select(args.screenName));
            screensManager.Bind(plugin, defaultScreen);
        }

        private static AnimationControlPanel CreateControlPanel(GameObject panel)
        {
            var go = new GameObject();
            go.transform.SetParent(panel.transform, false);

            var layout = go.AddComponent<LayoutElement>();
            layout.flexibleHeight = 100f;

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
            layout.preferredHeight = 200f;
            layout.flexibleWidth = 1f;

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
                btn.label = "Del";
                btn.button.onClick.AddListener(() => _plugin.deleteJSON.actionCallback());
            }

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

        private void InitToggleRightPanelButton(UIDynamicButton btn)
        {
            btn.button.onClick.RemoveAllListeners();
            btn.button.onClick.AddListener(() => Expand(!_expanded));
        }

        private void Expand(bool open)
        {
            if (!open && _expanded)
            {
                _expanded = false;
                screensManager.enabled = false;
                rightPanel.GetComponent<LayoutElement>().preferredWidth = RightPanelCollapsedWidth;
                _expandButton.label = "< Expand";
            }
            else if (open && !_expanded)
            {
                _expanded = true;
                screensManager.enabled = true;
                rightPanel.GetComponent<LayoutElement>().preferredWidth = RightPanelExpandedWidth;
                _expandButton.label = "Collapse >";
            }
        }

        private ScreensManager InitScreensManager(GameObject panel)
        {
            var go = new GameObject();
            go.transform.SetParent(panel.transform, false);

            var layout = go.AddComponent<LayoutElement>();
            layout.preferredHeight = 1138f;
            layout.flexibleWidth = 1;

            var sm = ScreensManager.Configure(go);
            sm.popupParent = popupParent;
            return sm;
        }

        public void Bind(AtomAnimationEditContext ctx)
        {
            animationEditContext = ctx;

            _controlPanel.Bind(ctx);
            _curveType.Bind(ctx);
            _curves.Bind(ctx);
        }
    }
}
