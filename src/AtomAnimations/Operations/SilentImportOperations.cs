using System;
using System.Linq;
using SimpleJSON;

namespace VamTimeline
{
    public class SilentImportOperations
    {
        private readonly Atom _containingAtom;
        private readonly AtomAnimation _animation;
        private readonly AtomAnimationSerializer _serializer;

         public SilentImportOperations(Atom containingAtom, AtomAnimation animation, AtomAnimationSerializer serializer)
        {
            _containingAtom = containingAtom;
            _animation = animation;
            _serializer = serializer;
        }

        public void Perform(string jsonContent, string conflictMode)
        {
            try
            {
                var json = JSON.Parse(jsonContent).AsObject;

                if (json["AtomType"]?.Value != _containingAtom.type)
                {
                    SuperController.LogError($"Timeline: Loaded animation for {json["AtomType"]} but current atom type is {_containingAtom.type}");
                    return;
                }

                var serializationVersion = json.HasKey("SerializeVersion") ? json["SerializeVersion"].AsInt : 0;
                var clipsJSON = json["Clips"].AsArray;
                if (clipsJSON == null || clipsJSON.Count == 0)
                {
                    SuperController.LogError("Timeline: No animations were found in the provided JSON.");
                    return;
                }

                _animation.index.StartBulkUpdates();
                try
                {
                    foreach (JSONClass clipJSON in clipsJSON)
                    {
                        var importedClip = _serializer.DeserializeClip(clipJSON, _animation.animatables, _animation.logger, serializationVersion);

                        var existingClip = _animation.GetClip(importedClip.animationSegment, importedClip.animationLayer, importedClip.animationName);

                        if (existingClip != null)
                        {
                            if (conflictMode == AtomPlugin.SilentImportConflictModes.Overwrite)
                            {
                                _animation.RemoveClip(existingClip);
                                _animation.AddClip(importedClip);
                            }
                            else if (conflictMode == AtomPlugin.SilentImportConflictModes.Rename)
                            {
                                importedClip.animationName = _animation.GetUniqueAnimationNameInLayer(existingClip);
                                _animation.AddClip(importedClip);
                            }
                            else // Skip is the default
                            {
                                // Do nothing
                            }
                        }
                        else
                        {
                            _animation.AddClip(importedClip);
                        }
                    }

                    if (_animation.clips.Count == 1 && _animation.clips[0].IsEmpty())
                        _animation.RemoveClip(_animation.clips[0]);

                    if (_animation.clips.Count == 0 || _animation.clips.All(c => c.animationSegmentId == AtomAnimationClip.SharedAnimationSegmentId))
                        _animation.CreateClip(AtomAnimationClip.DefaultAnimationName, AtomAnimationClip.DefaultAnimationLayer, AtomAnimationClip.DefaultAnimationSegment);
                }
                finally
                {
                    _animation.index.EndBulkUpdates();
                }

                _serializer.RestoreMissingTriggers(_animation);
                _animation.index.Rebuild();
                _animation.onClipsListChanged.Invoke();
            }
            catch (Exception exc)
            {
                SuperController.LogError($"Timeline: Silent import failed. {exc}");
            }
        }
    }
}