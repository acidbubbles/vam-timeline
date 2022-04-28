using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using AssetBundles;
using UnityEngine;
using UnityEngine.UI;

namespace VamTimeline
{
    public class VamPrefabFactory : MonoBehaviour
    {
        // ReSharper disable MemberCanBePrivate.Global
        public static RectTransform triggerActionsPrefab;
        public static RectTransform triggerActionMiniPrefab;
        public static RectTransform triggerActionDiscretePrefab;
        public static RectTransform triggerActionTransitionPrefab;
        public static RectTransform scrollbarPrefab;
        public static RectTransform buttonPrefab;
        // ReSharper restore MemberCanBePrivate.Global

        public static IEnumerator LoadUIAssets()
        {
            foreach (var x in LoadUIAsset("z_ui2", "TriggerActionsPanel", prefab => triggerActionsPrefab = prefab)) yield return x;
            foreach (var x in LoadUIAsset("z_ui2", "TriggerActionMiniPanel", prefab => triggerActionMiniPrefab = prefab)) yield return x;
            foreach (var x in LoadUIAsset("z_ui2", "TriggerActionDiscretePanel", prefab => triggerActionDiscretePrefab = prefab)) yield return x;
            foreach (var x in LoadUIAsset("z_ui2", "TriggerActionTransitionPanel", prefab => triggerActionTransitionPrefab = prefab)) yield return x;
            foreach (var x in LoadUIAsset("z_ui2", "DynamicTextField", prefab => scrollbarPrefab = prefab.GetComponentInChildren<ScrollRect>().verticalScrollbar.gameObject.GetComponent<RectTransform>())) yield return x;
            foreach (var x in LoadUIAsset("z_ui2", "DynamicButton", prefab => buttonPrefab = prefab)) yield return x;
        }

        private static IEnumerable LoadUIAsset(string assetBundleName, string assetName, Action<RectTransform> assign)
        {
            var request = AssetBundleManager.LoadAssetAsync(assetBundleName, assetName, typeof(GameObject));
            if (request == null) throw new NullReferenceException($"Request for {assetName} in {assetBundleName} assetbundle failed: Null request.");
            yield return request;
            var go = request.GetAsset<GameObject>();
            if (go == null) throw new NullReferenceException($"Request for {assetName} in {assetBundleName} assetbundle failed: Null GameObject.");
            var prefab = go.GetComponent<RectTransform>();
            if (prefab == null) throw new NullReferenceException($"Request for {assetName} in {assetBundleName} assetbundle failed: Null RectTransform.");
            assign(prefab);
        }

        public static RectTransform CreateScrollRect(GameObject gameObject)
        {
            var scrollView = CreateScrollView(gameObject);
            var viewport = CreateViewport(scrollView);
            var content = CreateContent(viewport);
            var scrollbar = CreateScrollbar(scrollView);
            var scrollRect = scrollView.GetComponent<ScrollRect>();
            scrollRect.viewport = viewport.GetComponent<RectTransform>();
            scrollRect.content = content.GetComponent<RectTransform>();
            scrollRect.verticalScrollbar = scrollbar;
            scrollRect.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
            return content.GetComponent<RectTransform>();
        }

        private static GameObject CreateScrollView(GameObject parent)
        {
            var go = new GameObject("Scroll View");
            go.transform.SetParent(parent.transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.StretchParent();

            var scroll = go.AddComponent<ScrollRect>();
            scroll.horizontal = false;

            return go;
        }

        private static Scrollbar CreateScrollbar(GameObject scrollView)
        {
            var vs = Instantiate(scrollbarPrefab, scrollView.transform, false);
            return vs.GetComponent<Scrollbar>();
        }

        private static GameObject CreateViewport(GameObject scrollView)
        {
            var go = new GameObject("Viewport");
            go.transform.SetParent(scrollView.transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.StretchParent();
            rect.pivot = new Vector2(0, 1);

            var image = go.AddComponent<Image>();
            image.raycastTarget = true;

            var mask = go.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            return go;
        }

        private static GameObject CreateContent(GameObject viewport)
        {
            var go = new GameObject("Content");
            go.transform.SetParent(viewport.transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.StretchTop();
            rect.pivot = new Vector2(0, 1);

            var layout = go.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 10f;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childAlignment = TextAnchor.UpperLeft;

            var fit = go.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            return go;
        }

        private static readonly Font _font = (Font)Resources.GetBuiltinResource(typeof(Font), "Arial.ttf");

        public IAtomPlugin plugin;
        private GameObject _currentContainer;
        private readonly List<JSONStorableParam> _storables = new List<JSONStorableParam>();

        public UIDynamic CreateSpacer()
        {
            var ui = Instantiate(plugin.manager.configurableSpacerPrefab).GetComponent<UIDynamic>();
            ui.gameObject.transform.SetParent(transform, false);
            ui.height = 20f;
            return ui;
        }

        public UIDynamicSlider CreateSlider(JSONStorableFloat jsf)
        {
            RegisterStorable(jsf);
            var ui = Instantiate(plugin.manager.configurableSliderPrefab).GetComponent<UIDynamicSlider>();
            ui.gameObject.transform.SetParent(transform, false);
            ui.Configure(jsf.name, jsf.min, jsf.max, jsf.val, jsf.constrained, "F2", true, !jsf.constrained);
            jsf.slider = ui.slider;
            return ui;
        }

        public UIDynamicButton CreateButton(string label)
        {
            var ui = Instantiate(plugin.manager.configurableButtonPrefab).GetComponent<UIDynamicButton>();
            ui.gameObject.transform.SetParent(transform, false);
            ui.label = label;
            return ui;
        }

        public UIDynamicToggle CreateToggle(JSONStorableBool jsb)
        {
            RegisterStorable(jsb);
            var ui = Instantiate(plugin.manager.configurableTogglePrefab).GetComponent<UIDynamicToggle>();
            ui.gameObject.transform.SetParent(transform, false);
            ui.label = jsb.name;
            jsb.toggle = ui.toggle;
            return ui;
        }

        public UIDynamicTextField CreateTextField(JSONStorableString jss)
        {
            RegisterStorable(jss);
            var ui = Instantiate(plugin.manager.configurableTextFieldPrefab).GetComponent<UIDynamicTextField>();
            ui.gameObject.transform.SetParent(transform, false);
            jss.dynamicText = ui;
            return ui;
        }

        public UIDynamicPopup CreateMicroPopup(JSONStorableStringChooser jsc, float popupPanelHeight = 350f)
        {
            RegisterStorable(jsc);
            var prefab = plugin.manager.configurablePopupPrefab;

            var ui = Instantiate(prefab).GetComponent<UIDynamicPopup>();
            ui.gameObject.transform.SetParent(transform, false);
            ui.label = jsc.name;
            jsc.popup = ui.popup;

            ui.popup.labelText.alignment = TextAnchor.MiddleLeft;
            ui.labelWidth = 120f;
            ui.popup.labelText.fontSize = 24;
            var labelTextRect = ui.popup.labelText.GetComponent<RectTransform>();
            labelTextRect.offsetMin += new Vector2(10f, 0f);
            ui.popup.labelText.GetComponent<RectTransform>().anchorMax = new Vector2(0.04f, 0.91f);

            var popupLayout = ui.popup.GetComponent<LayoutElement>();
            popupLayout.minHeight = 60f;
            popupLayout.preferredHeight = 60f;

            var topButtonRect = ui.popup.topButton.GetComponent<RectTransform>();
            topButtonRect.offsetMin = new Vector2(200f, 5f);
            topButtonRect.offsetMax = new Vector2(-80f, -5f);

            {
                var btn = Instantiate(plugin.manager.configurableButtonPrefab, ui.transform, false);
                Destroy(btn.GetComponent<LayoutElement>());
                btn.GetComponent<UIDynamicButton>().label = "<";
                btn.GetComponent<UIDynamicButton>().button.onClick.AddListener(() =>
                {
                    ui.popup.SetPreviousValue();
                });
                var btnRect = btn.GetComponent<RectTransform>();
                btnRect.anchoredPosition = new Vector2(0f, 0f);
                btnRect.sizeDelta = new Vector2(70f, -10f);
                btnRect.offsetMin += new Vector2(160f, 0f);
                btnRect.offsetMax += new Vector2(160f, 0f);
                btnRect.anchorMin = new Vector2(0f, 0f);
                btnRect.anchorMax = new Vector2(0f, 1f);
            }

            {
                var btn = Instantiate(plugin.manager.configurableButtonPrefab, ui.transform, false);
                Destroy(btn.GetComponent<LayoutElement>());
                btn.GetComponent<UIDynamicButton>().label = ">";
                btn.GetComponent<UIDynamicButton>().button.onClick.AddListener(() =>
                {
                    ui.popup.SetNextValue();
                });
                var btnRect = btn.GetComponent<RectTransform>();
                btnRect.anchoredPosition = new Vector2(0f, 0f);
                btnRect.sizeDelta = new Vector2(70f, -10f);
                btnRect.offsetMin += new Vector2(-40f, 0f);
                btnRect.offsetMax += new Vector2(-40f, 0f);
                btnRect.anchorMin = new Vector2(1f, 0f);
                btnRect.anchorMax = new Vector2(1f, 1f);
            }

            ui.popupPanelHeight = popupPanelHeight;

            return ui;
        }

        public UIDynamicPopup CreatePopup(JSONStorableStringChooser jsc, bool filterable, bool navButtons, float popupPanelHeight = 350f, bool upwards = false)
        {
            RegisterStorable(jsc);
            // ReSharper disable once JoinDeclarationAndInitializer
            Transform prefab;
#if (VAM_GT_1_20)
            if (filterable && plugin.manager.configurableFilterablePopupPrefab != null)
            {
                prefab = plugin.manager.configurableFilterablePopupPrefab;
            }
            else
            {
                prefab = plugin.manager.configurableScrollablePopupPrefab;
                filterable = false;
            }
#else
            prefab = plugin.manager.configurableScrollablePopupPrefab;
#endif

            var ui = Instantiate(prefab).GetComponent<UIDynamicPopup>();
            ui.gameObject.transform.SetParent(transform, false);
            ui.label = jsc.name;
            jsc.popup = ui.popup;

            if (navButtons)
            {
                ui.popup.labelText.alignment = TextAnchor.UpperCenter;
                var labelTextRect = ui.popup.labelText.GetComponent<RectTransform>();
                float btnAnchorOffsetMaxY;
                if (filterable)
                {
                    ui.popup.labelText.GetComponent<RectTransform>().anchorMax = new Vector2(0.03f, 0.91f);
                    btnAnchorOffsetMaxY = 70;
                }
                else
                {
                    ui.popup.labelText.fontSize = 24;
                    labelTextRect.anchorMax = new Vector2(0.03f, 0.95f);
                    btnAnchorOffsetMaxY = 65f;
                }

                {
                    var btn = Instantiate(plugin.manager.configurableButtonPrefab, ui.transform, false);
                    Destroy(btn.GetComponent<LayoutElement>());
                    btn.GetComponent<UIDynamicButton>().label = "<";
                    btn.GetComponent<UIDynamicButton>().button.onClick.AddListener(() =>
                    {
                        ui.popup.SetPreviousValue();
                    });
                    var prevBtnRect = btn.GetComponent<RectTransform>();
                    prevBtnRect.pivot = new Vector2(0, 0);
                    prevBtnRect.anchoredPosition = new Vector2(10f, 0);
                    prevBtnRect.sizeDelta = new Vector2(0f, 0f);
                    prevBtnRect.offsetMin = new Vector2(5f, 5f);
                    prevBtnRect.offsetMax = new Vector2(80f, btnAnchorOffsetMaxY);
                    prevBtnRect.anchorMin = new Vector2(0f, 0f);
                    prevBtnRect.anchorMax = new Vector2(0f, 0f);
                }

                {
                    var btn = Instantiate(plugin.manager.configurableButtonPrefab, ui.transform, false);
                    Destroy(btn.GetComponent<LayoutElement>());
                    btn.GetComponent<UIDynamicButton>().label = ">";
                    btn.GetComponent<UIDynamicButton>().button.onClick.AddListener(() =>
                    {
                        ui.popup.SetNextValue();
                    });
                    var prevBtnRect = btn.GetComponent<RectTransform>();
                    prevBtnRect.pivot = new Vector2(0, 0);
                    prevBtnRect.anchoredPosition = new Vector2(10f, 0);
                    prevBtnRect.sizeDelta = new Vector2(0f, 0f);
                    prevBtnRect.offsetMin = new Vector2(82f, 5f);
                    prevBtnRect.offsetMax = new Vector2(157f, btnAnchorOffsetMaxY);
                    prevBtnRect.anchorMin = new Vector2(0f, 0f);
                    prevBtnRect.anchorMax = new Vector2(0f, 0f);
                }
            }

            ui.popupPanelHeight = popupPanelHeight;

            if (upwards)
            {
                ui.popup.popupPanel.offsetMin += new Vector2(0, ui.popupPanelHeight + 60);
                ui.popup.popupPanel.offsetMax += new Vector2(0, ui.popupPanelHeight + 60);
            }

            return ui;
        }

        public UIDynamicTextField CreateTextInput(JSONStorableString jss)
        {
            RegisterStorable(jss);

            var container = new GameObject();
            container.transform.SetParent(transform, false);
            {
                var rect = container.AddComponent<RectTransform>();
                rect.pivot = new Vector2(0, 1);

                var layout = container.AddComponent<LayoutElement>();
                layout.preferredHeight = 70f;
                layout.flexibleWidth = 1f;
            }

            var textfield = CreateTextInput(jss, plugin.manager.configurableTextFieldPrefab, container.transform);

            var title = new GameObject();
            title.transform.SetParent(container.transform, false);
            {
                var rect = title.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, 1);
                rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = new Vector2(0, 0f);
                rect.sizeDelta = new Vector2(0, 30f);

                var text = title.AddComponent<Text>();
                text.font = textfield.UItext.font;
                text.text = jss.name;
                text.fontSize = 24;
                text.color = new Color(0.85f, 0.8f, 0.82f);
            }

            return textfield;
        }

        public static UIDynamicTextField CreateTextInput(JSONStorableString jss, Transform configurableTextFieldPrefab, Transform container)
        {
            var textfield = Instantiate(configurableTextFieldPrefab).GetComponent<UIDynamicTextField>();
            textfield.gameObject.transform.SetParent(container, false);
            {
                jss.dynamicText = textfield;

                textfield.backgroundColor = Color.white;

                var input = textfield.gameObject.AddComponent<InputField>();
                input.textComponent = textfield.UItext;
                jss.inputField = input;

                Destroy(textfield.GetComponent<LayoutElement>());

                var rect = textfield.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, 1);
                rect.anchorMax = new Vector2(1, 1);
                rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = new Vector2(0, -30f);
                rect.sizeDelta = new Vector2(0, 40f);
            }
            return textfield;
        }

        public Text CreateHeader(string val, int level)
        {
            var headerUI = CreateSpacer();
            headerUI.height = 40f;

            var text = headerUI.gameObject.AddComponent<Text>();
            text.text = val;
            text.font = _font;
            switch (level)
            {
                case 1:
                    text.fontSize = 30;
                    text.fontStyle = FontStyle.Bold;
                    text.color = new Color(0.95f, 0.9f, 0.92f);
                    break;
                case 2:
                    text.fontSize = 28;
                    text.fontStyle = FontStyle.Bold;
                    text.color = new Color(0.85f, 0.8f, 0.82f);
                    break;
            }

            return text;
        }

        public void ClearConfirm()
        {
            Destroy(_currentContainer);
            _currentContainer = null;
        }

        public void CreateConfirm(string message, Action fn)
        {
            if (_currentContainer != null) throw new InvalidOperationException("Only one container can be shown at a time.");

            var container = new GameObject();
            container.transform.SetParent(transform.parent.parent, false);

            {
                var rect = container.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 0f);
                rect.anchorMax = new Vector2(1f, 1f);

                var bg = container.AddComponent<Image>();
                bg.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
            }

            var group = new GameObject();
            group.transform.SetParent(container.transform, false);

            {
                var rect = group.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0f, 0.5f);
                rect.anchorMax = new Vector2(1f, 0.5f);
                rect.sizeDelta = new Vector2(0f, 200f);

                var layout = group.AddComponent<VerticalLayoutGroup>();
                layout.childControlHeight = true;
                layout.childForceExpandHeight = true;
                layout.spacing = 10f;
            }

            {
                var text = CreateHeader(message, 2);
                text.alignment = TextAnchor.MiddleCenter;
                text.transform.SetParent(group.transform, false);
            }

            {
                var btn = CreateButton("Confirm");
                btn.transform.SetParent(group.transform, false);

                btn.buttonColor = new Color(1f, 0f, 0f);
                btn.textColor = new Color(1f, 1f, 1f);

                btn.button.onClick.AddListener(() =>
                {
                    _currentContainer = null;
                    Destroy(container);
                    fn();
                });
            }

            {
                var btn = CreateButton("Cancel");
                btn.transform.SetParent(group.transform, false);

                btn.button.onClick.AddListener(() =>
                {
                    _currentContainer = null;
                    Destroy(container);
                });
            }

            _currentContainer = container;
        }

        public void OnDestroy()
        {
            var clone = new JSONStorableParam[_storables.Count];
            _storables.CopyTo(clone);
            _storables.Clear();
            foreach (var component in clone)
            {
                if (component == null) continue;

                if (component is JSONStorableStringChooser)
                    RemovePopup((JSONStorableStringChooser)component);
                else if (component is JSONStorableFloat)
                    RemoveSlider((JSONStorableFloat)component);
                else if (component is JSONStorableString)
                    RemoveTextField((JSONStorableString)component);
                else if (component is JSONStorableBool)
                    RemoveToggle((JSONStorableBool)component);
                else
                    SuperController.LogError($"Timeline: Cannot remove component {component}");
            }
        }

        public void RemovePopup(JSONStorableStringChooser jsc, UIDynamicPopup component = null)
        {
            if (jsc.popup != null) { jsc.popup = null; _storables.Remove(jsc); }
            if (component != null) Destroy(component.gameObject);
        }

        public void RemoveSlider(JSONStorableFloat jsf, UIDynamicSlider component = null)
        {
            if (jsf.slider != null) { jsf.slider = null; _storables.Remove(jsf); }
            if (component != null) Destroy(component.gameObject);
        }

        public void RemoveTextField(JSONStorableString jss, UIDynamicTextField component = null)
        {
            if (jss.dynamicText != null) { jss.dynamicText = null; _storables.Remove(jss); }
            if (component != null) Destroy(component.gameObject);
        }

        public void RemoveToggle(JSONStorableBool jsb, UIDynamicToggle component = null)
        {
            if (jsb.toggle != null) { jsb.toggle = null; _storables.Remove(jsb); }
            if (component != null) Destroy(component.gameObject);
        }

        private T RegisterStorable<T>(T v)
            where T : JSONStorableParam
        {
            _storables.Add(v);
            ValidateStorableFreeToBind(v);
            return v;
        }

        private void ValidateStorableFreeToBind(JSONStorableParam v)
        {
            if (v is JSONStorableStringChooser)
            {
                if (((JSONStorableStringChooser)v).popup != null)
                    SuperController.LogError($"Storable '{v.name}' of atom '{plugin.containingAtom.name}' was not correctly unregistered.");
            }
            else if (v is JSONStorableFloat)
            {
                if (((JSONStorableFloat)v).slider != null)
                    SuperController.LogError($"Storable '{v.name}' of atom '{plugin.containingAtom.name}' was not correctly unregistered.");
            }
            else if (v is JSONStorableString)
            {
                if (((JSONStorableString)v).inputField != null)
                    SuperController.LogError($"Storable '{v.name}' of atom '{plugin.containingAtom.name}' was not correctly unregistered.");
            }
            else if (v is JSONStorableBool)
            {
                if (((JSONStorableBool)v).toggle != null)
                    SuperController.LogError($"Storable '{v.name}' of atom '{plugin.containingAtom.name}' was not correctly unregistered.");
            }
        }
    }
}
