using System;
using System.Collections;
using System.Collections.Generic;
using AssetBundles;
using UnityEngine;
using UnityEngine.UI;

namespace VamTimeline
{
    public class VamPrefabFactory : MonoBehaviour
    {
        public static RectTransform triggerActionsPrefab;
        public static RectTransform triggerActionMiniPrefab;
        public static RectTransform triggerActionDiscretePrefab;
        public static RectTransform triggerActionTransitionPrefab;
        public static RectTransform scrollbarPrefab;
        public static RectTransform buttonPrefab;

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
            AssetBundleLoadAssetOperation request = AssetBundleManager.LoadAssetAsync(assetBundleName, assetName, typeof(GameObject));
            if (request == null) throw new NullReferenceException($"Request for {assetName} in {assetBundleName} assetbundle failed: Null request.");
            yield return request;
            GameObject go = request.GetAsset<GameObject>();
            if (go == null) throw new NullReferenceException($"Request for {assetName} in {assetBundleName} assetbundle failed: Null GameObject.");
            var prefab = go.GetComponent<RectTransform>();
            if (prefab == null) throw new NullReferenceException($"Request for {assetName} in {assetBundleName} assetbundle failed: Null RectTansform.");
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
            var vs = Instantiate(scrollbarPrefab);
            vs.transform.SetParent(scrollView.transform, false);
            return vs.GetComponent<Scrollbar>();
        }

        private static GameObject CreateViewport(GameObject scrollView)
        {
            var go = new GameObject("Viewport");
            go.transform.SetParent(scrollView.transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.StretchParent();

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

        public IAtomPlugin plugin;
        private readonly List<JSONStorableParam> _storables = new List<JSONStorableParam>();

        public VamPrefabFactory()
        {
        }

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

        public UIDynamicPopup CreateScrollablePopup(JSONStorableStringChooser jsc)
        {
            RegisterStorable(jsc);
            var ui = Instantiate(plugin.manager.configurableScrollablePopupPrefab).GetComponent<UIDynamicPopup>();
            ui.gameObject.transform.SetParent(transform, false);
            ui.label = jsc.name;
            jsc.popup = ui.popup;
            return ui;
        }

        public UIDynamicTextField CreateTextInput(JSONStorableString jss)
        {
            var textfield = CreateTextField(jss);
            textfield.height = 20f;
            textfield.backgroundColor = Color.white;
            var input = textfield.gameObject.AddComponent<InputField>();
            var rect = input.GetComponent<RectTransform>().sizeDelta = new Vector2(1f, 0.4f);
            input.textComponent = textfield.UItext;
            jss.inputField = input;
            return textfield;
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
                    SuperController.LogError($"VamTimeline: Cannot remove component {component}");
            }
        }

        public void RemovePopup(JSONStorableStringChooser jsc, UIDynamicPopup component = null)
        {
            if (jsc.popup != null) { jsc.popup = null; _storables.Remove(jsc); }
            if (component != null) Destroy(component);
        }

        public void RemoveSlider(JSONStorableFloat jsf, UIDynamicSlider component = null)
        {
            if (jsf.slider != null) { jsf.slider = null; _storables.Remove(jsf); }
            if (component != null) Destroy(component);
        }

        public void RemoveTextField(JSONStorableString jss, UIDynamicTextField component = null)
        {
            if (jss.dynamicText != null) { jss.dynamicText = null; _storables.Remove(jss); }
            if (component != null) Destroy(component);
        }

        public void RemoveToggle(JSONStorableBool jsb, UIDynamicToggle component = null)
        {
            if (jsb.toggle != null) { jsb.toggle = null; _storables.Remove(jsb); }
            if (component != null) Destroy(component);
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
                    SuperController.LogError($"Storable {v.name} of atom {plugin.containingAtom.name} was not correctly unregistered.");
            }
            else if (v is JSONStorableFloat)
            {
                if (((JSONStorableFloat)v).slider != null)
                    SuperController.LogError($"Storable {v.name} of atom {plugin.containingAtom.name} was not correctly unregistered.");
            }
            else if (v is JSONStorableString)
            {
                if (((JSONStorableString)v).inputField != null)
                    SuperController.LogError($"Storable {v.name} of atom {plugin.containingAtom.name} was not correctly unregistered.");
            }
            else if (v is JSONStorableBool)
            {
                if (((JSONStorableBool)v).toggle != null)
                    SuperController.LogError($"Storable {v.name} of atom {plugin.containingAtom.name} was not correctly unregistered.");
            }
        }
    }
}
