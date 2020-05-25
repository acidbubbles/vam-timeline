using System;
using System.Collections.Generic;
using CurveEditor.UI;
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
    public abstract class AtomAnimationBaseUI : IDisposable
    {
        public abstract string Name { get; }

        private readonly List<UIDynamic> _components = new List<UIDynamic>();
        private readonly List<JSONStorableParam> _storables = new List<JSONStorableParam>();
        protected IAtomPlugin Plugin;
        private UICurveEditor _curveUI;
        private UIDynamic _curveEditorContainer;
        private int _currentTargets = 0;
        private string _currentAnimation = null;
        private readonly List<StorableAnimationCurve> _curves = new List<StorableAnimationCurve>();

        protected AtomAnimationBaseUI(IAtomPlugin plugin)
        {
            Plugin = plugin;
        }

        public virtual void Init()
        {
        }

        public virtual void UpdatePlaying()
        {
            _curveUI?.SetScrubberPosition(Plugin.Animation.Time);
        }

        public virtual void AnimationModified()
        {
            var currentTargets = Plugin.Animation.Current.AllTargetsCount;
            var currentAnimation = Plugin.Animation.Current.AnimationName;
            if (currentTargets != _currentTargets || currentAnimation != _currentAnimation)
            {
                if (_curveUI != null)
                {
                    foreach (var curve in _curves)
                    {
                        _curveUI.RemoveCurve(curve);
                    }
                    _curves.Clear();

                    RegisterCurrentCurves();
                }
            }
            else
            {
                foreach (var curve in _curves)
                {
                    if (!curve.graphDirty) continue;
                    // TODO: Differenciate between new curve (change animation, new animation) and position change
                    UpdateCurveGraph(curve);
                    UpdateCurveBounds();
                    curve.graphDirty = false;
                }
            }
        }

        protected void UpdateCurveGraph(StorableAnimationCurve storable)
        {
            _curveUI.UpdateCurve(storable);
        }

        public virtual void AnimationFrameUpdated()
        {
            _curveUI?.SetScrubberPosition(Plugin.Animation.Time);
        }

        protected void InitClipboardUI(bool rightSide)
        {
            var cutUI = Plugin.CreateButton("Cut / Delete Frame", rightSide);
            cutUI.button.onClick.AddListener(() => Plugin.CutJSON.actionCallback());
            RegisterComponent(cutUI);

            var copyUI = Plugin.CreateButton("Copy Frame", rightSide);
            copyUI.button.onClick.AddListener(() => Plugin.CopyJSON.actionCallback());
            RegisterComponent(copyUI);

            var pasteUI = Plugin.CreateButton("Paste Frame", rightSide);
            pasteUI.button.onClick.AddListener(() => Plugin.PasteJSON.actionCallback());
            RegisterComponent(pasteUI);

            var text = pasteUI.GetComponentInChildren<Text>();
        }

        protected void InitDisplayUI(bool rightSide, float height = 260f)
        {
            if (Plugin.Animation == null || Plugin.Animation.Current == null) return;

            _curveEditorContainer = Plugin.CreateSpacer(rightSide);
            _curveEditorContainer.height = height;
            RegisterComponent(_curveEditorContainer);
            var curveEditorSettings = new UICurveEditorSettings
            {
                readOnly = true,
                showScrubbers = true,
                allowKeyboardShortcuts = false
            };
            _curveUI = new UICurveEditor(_curveEditorContainer, 520, _curveEditorContainer.height, buttons: new List<UIDynamicButton>(), settings: curveEditorSettings);
            RegisterCurrentCurves();
            RegisterComponent(_curveUI.container);
        }

        private void RegisterCurrentCurves()
        {
            // TODO: Only show the current target
            foreach (var controllerTarget in Plugin.Animation.Current.GetAllOrSelectedControllerTargets())
            {
                var x = _curves.AddAndRetreive(controllerTarget.StorableX);
                _curveUI.AddCurve(x, new CurveLineSettings().Colorize(Color.red));
                x.graphDirty = false;
                var y = _curves.AddAndRetreive(controllerTarget.StorableY);
                _curveUI.AddCurve(y, new CurveLineSettings().Colorize(Color.green));
                y.graphDirty = false;
                var z = _curves.AddAndRetreive(controllerTarget.StorableZ);
                _curveUI.AddCurve(z, new CurveLineSettings().Colorize(Color.blue));
                z.graphDirty = false;

                // Only showing the w component of the quaternion since it shows if there's rotation, and that's enough
                var rotW = _curves.AddAndRetreive(controllerTarget.StorableRotW);
                _curveUI.AddCurve(rotW, new CurveLineSettings().Colorize(Color.yellow));
                rotW.graphDirty = false;

                break;
            }
            // foreach (var floatParamTarget in Plugin.Animation.Current.GetAllOrSelectedFloatParamTargets())
            // {
            //     var v = _curves.AddAndRetreive(floatParamTarget.StorableValue);
            //     _curveUI.AddCurve(v, new CurveLineSettings().Colorize(new Color(0.6f, 0.6f, 0.6f)));
            //     v.graphDirty = false;
            // }
            UpdateCurveBounds();
            if (_curves.Count > 0)
                _curveUI.SetScrubberPosition(Plugin.Animation.Time);
            _currentTargets = Plugin.Animation.Current.AllTargetsCount;
            _currentAnimation = Plugin.Animation.Current.AnimationName;
        }

        private void UpdateCurveBounds()
        {
            _curveUI.SetViewToFit(new Vector4(0, 0.5f, 0, 0.5f));
            // var length = Plugin.Animation.Current.AnimationLength;

            // foreach (var curve in _curves)
            // {
            //     _curveUI.SetValueBounds(curve, new Rect(0, curve.min * 1.2f, length, (curve.max - curve.min) * 1.4f), true, true);
            // }
        }

        protected void CreateSpacer(bool rightSide)
        {
            var spacerUI = Plugin.CreateSpacer(rightSide);
            spacerUI.height = 30f;
            RegisterComponent(spacerUI);
        }

        protected T RegisterStorable<T>(T v)
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
                    SuperController.LogError($"Storable {v.name} of atom {Plugin.ContainingAtom.name} was not correctly unregistered.");
            }
            else if (v is JSONStorableFloat)
            {
                if (((JSONStorableFloat)v).slider != null)
                    SuperController.LogError($"Storable {v.name} of atom {Plugin.ContainingAtom.name} was not correctly unregistered.");
            }
            else if (v is JSONStorableString)
            {
                if (((JSONStorableString)v).inputField != null)
                    SuperController.LogError($"Storable {v.name} of atom {Plugin.ContainingAtom.name} was not correctly unregistered.");
            }
            else if (v is JSONStorableBool)
            {
                if (((JSONStorableBool)v).toggle != null)
                    SuperController.LogError($"Storable {v.name} of atom {Plugin.ContainingAtom.name} was not correctly unregistered.");
            }
        }

        protected T RegisterComponent<T>(T v)
            where T : UIDynamic
        {
            _components.Add(v);
            return v;
        }

        public virtual void Dispose()
        {
            foreach (var component in _storables)
            {
                if (component == null) continue;

                if (component is JSONStorableStringChooser)
                    Plugin.RemovePopup((JSONStorableStringChooser)component);
                else if (component is JSONStorableFloat)
                    Plugin.RemoveSlider((JSONStorableFloat)component);
                else if (component is JSONStorableString)
                    Plugin.RemoveTextField((JSONStorableString)component);
                else if (component is JSONStorableBool)
                    Plugin.RemoveToggle((JSONStorableBool)component);
                else
                    SuperController.LogError($"VamTimeline: Cannot remove component {component}");
            }
            _storables.Clear();

            foreach (var component in _components)
            {
                if (component is UIDynamicButton)
                    Plugin.RemoveButton((UIDynamicButton)component);
                else if (component is UIDynamicPopup)
                    Plugin.RemovePopup((UIDynamicPopup)component);
                else if (component is UIDynamicSlider)
                    Plugin.RemoveSlider((UIDynamicSlider)component);
                else if (component is UIDynamicTextField)
                    Plugin.RemoveTextField((UIDynamicTextField)component);
                else if (component is UIDynamicToggle)
                    Plugin.RemoveToggle((UIDynamicToggle)component);
                else
                    Plugin.RemoveSpacer(component);
            }
            _components.Clear();

            if (_curveUI != null)
            {
                _curveUI = null;
                _curves.Clear();
            }
        }
    }
}

