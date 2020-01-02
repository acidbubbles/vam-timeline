using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace VamTimeline
{
    /// <summary>
    /// VaM Timeline
    /// By Acidbubbles
    /// Animation timeline with keyframes
    /// Source: https://github.com/acidbubbles/vam-timeline
    /// </summary>
    public abstract class PluginImplBase<TAnimation> where TAnimation : class, IAnimation
    {
        protected readonly IAnimationPlugin _plugin;

        // State
        private IAnimationSerializer<TAnimation> _serializer;
        protected TAnimation _animation;
        private bool _restoring;

        // Save
        private JSONStorableString _saveJSON;

        protected PluginImplBase(IAnimationPlugin plugin)
        {
            _plugin = plugin;
        }

        #region Initialization

        public void RegisterSerializer(IAnimationSerializer<TAnimation> serializer)
        {
            _serializer = serializer;
        }

        public void InitCommonStorables()
        {
            _saveJSON = new JSONStorableString(StorableNames.Save, "", (string v) => RestoreState(v));
            _plugin.RegisterString(_saveJSON);
        }

        protected IEnumerator CreateAnimationIfNoneIsLoaded()
        {
            if (_animation != null) yield break;
            yield return new WaitForEndOfFrame();
            RestoreState(_saveJSON.val);
        }

        #endregion

        #region Load / Save

        public void RestoreState(string json)
        {
            if (_restoring) return;
            _restoring = true;

            try
            {
                if (_animation != null)
                    _animation = null;

                if (!string.IsNullOrEmpty(json))
                {
                    _animation = _serializer.DeserializeAnimation(json);
                }

                if (_animation == null)
                {
                    // TODO: Name the backup to avoid conflict between atom and morph animations
                    var backupStorableID = _plugin.ContainingAtom.GetStorableIDs().FirstOrDefault(s => s.EndsWith("VamTimeline.BackupPlugin"));
                    if (backupStorableID != null)
                    {
                        var backupStorable = _plugin.ContainingAtom.GetStorableByID(backupStorableID);
                        var backupJSON = backupStorable.GetStringJSONParam("Backup");
                        if (!string.IsNullOrEmpty(backupJSON.val))
                        {
                            SuperController.LogMessage("No save found but a backup was detected. Loading backup.");
                            _animation = _serializer.DeserializeAnimation(backupJSON.val);
                        }
                    }
                }

            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.PluginImplBase.RestoreState(1): " + exc);
            }

            try
            {
                if (_animation == null)
                    _animation = _serializer.CreateDefaultAnimation();

                _animation.Initialize();
                AnimationUpdated();
                ContextUpdated();
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.PluginImplBase.RestoreState(2): " + exc);
            }

            _restoring = false;
        }

        public void SaveState()
        {
            try
            {
                if (_restoring) return;
                if (_animation.IsEmpty()) return;

                var serialized = _serializer.SerializeAnimation(_animation);

                // if (serialized == _undoList.LastOrDefault())
                //     return;

                // TODO: Bring back undoes
                // if (!string.IsNullOrEmpty(_saveJSON.val))
                // {
                //     _undoList.Add(_saveJSON.val);
                //     if (_undoList.Count > MaxUndo) _undoList.RemoveAt(0);
                // }

                _saveJSON.valNoCallback = serialized;

                var backupStorableID = _plugin.ContainingAtom.GetStorableIDs().FirstOrDefault(s => s.EndsWith("VamTimeline.BackupPlugin"));
                if (backupStorableID != null)
                {
                    var backupStorable = _plugin.ContainingAtom.GetStorableByID(backupStorableID);
                    var backupJSON = backupStorable.GetStringJSONParam("Backup");
                    backupJSON.val = serialized;
                }
            }
            catch (Exception exc)
            {
                SuperController.LogError("VamTimeline.PluginImplBase.SaveState: " + exc);
            }
        }

        #endregion

        #region Sync

        protected abstract void ContextUpdated();
        protected abstract void AnimationUpdated();

        #endregion
    }
}
