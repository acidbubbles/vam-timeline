using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VamTimeline.Tests.Framework;

namespace VamTimeline.Tests.Specs
{
    public class ResizeAnimationOperationTests : ITestClass
    {
        public IEnumerable<Test> GetTests()
        {
            yield return new Test(nameof(StretchLongerFreeController), StretchLongerFreeController);
            yield return new Test(nameof(StretchShorterFreeController), StretchShorterFreeController);
            yield return new Test(nameof(StretchLongerFloatParam), StretchLongerFloatParam);
            yield return new Test(nameof(StretchShorterFloatParam), StretchShorterFloatParam);
            yield return new Test(nameof(StretchLongerTrigger), StretchLongerTrigger);
            yield return new Test(nameof(StretchShorterTrigger), StretchShorterTrigger);

            yield return new Test(nameof(CropOrExtendEndLongerFreeController), CropOrExtendEndLongerFreeController);
            yield return new Test(nameof(CropOrExtendEndShorterFreeController), CropOrExtendEndShorterFreeController);
            yield return new Test(nameof(CropOrExtendEndLongerFloatParam), CropOrExtendEndLongerFloatParam);
            yield return new Test(nameof(CropOrExtendEndShorterFloatParam), CropOrExtendEndShorterFloatParam);
            yield return new Test(nameof(CropOrExtendEndLongerTrigger), CropOrExtendEndLongerTrigger);
            yield return new Test(nameof(CropOrExtendEndShorterTrigger), CropOrExtendEndShorterTrigger);

            yield return new Test(nameof(CropOrExtendBeginLongerFreeController), CropOrExtendBeginLongerFreeController);
            yield return new Test(nameof(CropOrExtendBeginShorterFreeController), CropOrExtendBeginShorterFreeController);
            yield return new Test(nameof(CropOrExtendBeginLongerFloatParam), CropOrExtendBeginLongerFloatParam);
            yield return new Test(nameof(CropOrExtendBeginShorterFloatParam), CropOrExtendBeginShorterFloatParam);
            yield return new Test(nameof(CropOrExtendBeginLongerTrigger), CropOrExtendBeginLongerTrigger);
            yield return new Test(nameof(CropOrExtendBeginShorterTrigger), CropOrExtendBeginShorterTrigger);
        }

        #region Stretch

        public IEnumerable StretchLongerFreeController(TestContext context)
        {
            var clip = context.animation.clips[0];
            var target = GivenThreeKeyframesFreeController(context, clip);

            new ResizeAnimationOperation(clip).Stretch(4f);

            context.Assert(target.x.keys.Select(k => k.time), new[] { 0f, 2f, 4f }, "Keyframes after resize");
            context.Assert(target.settings.Select(k => k.Key).OrderBy(k => k), new[] { 0, 2000, 4000 }, "Settings after resize");
            yield break;
        }

        public IEnumerable StretchShorterFreeController(TestContext context)
        {
            var clip = context.animation.clips[0];
            var target = GivenThreeKeyframesFreeController(context, clip);

            new ResizeAnimationOperation(clip).Stretch(1f);

            context.Assert(target.x.keys.Select(k => k.time), new[] { 0f, 0.5f, 1f }, "Keyframes after resize");
            context.Assert(target.settings.Select(k => k.Key).OrderBy(k => k), new[] { 0, 500, 1000 }, "Settings after resize");
            yield break;
        }

        public IEnumerable StretchLongerFloatParam(TestContext context)
        {
            var clip = context.animation.clips[0];
            var target = GivenThreeKeyframesFloatParam(context, clip);

            new ResizeAnimationOperation(clip).Stretch(4f);

            context.Assert(target.value.keys.Select(k => k.time), new[] { 0f, 2f, 4f }, "Keyframes after resize");
            yield break;
        }

        public IEnumerable StretchShorterFloatParam(TestContext context)
        {
            var clip = context.animation.clips[0];
            var target = GivenThreeKeyframesFloatParam(context, clip);

            new ResizeAnimationOperation(clip).Stretch(1f);

            context.Assert(target.value.keys.Select(k => k.time), new[] { 0f, 0.5f, 1f }, "Keyframes after resize");
            yield break;
        }

        public IEnumerable StretchLongerTrigger(TestContext context)
        {
            var clip = context.animation.clips[0];
            var target = GivenThreeKeyframesTrigger(context, clip);

            new ResizeAnimationOperation(clip).Stretch(4f);

            context.Assert(target.triggersMap.Select(k => k.Key).OrderBy(k => k), new[] { 0, 2000, 4000 }, "Map after resize");
            yield break;
        }

        public IEnumerable StretchShorterTrigger(TestContext context)
        {
            var clip = context.animation.clips[0];
            var target = GivenThreeKeyframesTrigger(context, clip);

            new ResizeAnimationOperation(clip).Stretch(1f);

            context.Assert(target.triggersMap.Select(k => k.Key).OrderBy(k => k), new[] { 0, 500, 1000 }, "Map after resize");
            yield break;
        }

        #endregion

        #region CropOrExtendEnd

        public IEnumerable CropOrExtendEndLongerFreeController(TestContext context)
        {
            var clip = context.animation.clips[0];
            var target = GivenThreeKeyframesFreeController(context, clip);

            new ResizeAnimationOperation(clip).CropOrExtendEnd(4f);

            context.Assert(target.x.keys.Select(k => k.time), new[] { 0f, 1f, 2f, 4f }, "Keyframes after resize");
            context.Assert(target.settings.Select(k => k.Key).OrderBy(k => k), new[] { 0, 1000, 2000, 4000 }, "Settings after resize");
            yield break;
        }

        public IEnumerable CropOrExtendEndShorterFreeController(TestContext context)
        {
            var clip = context.animation.clips[0];
            var target = GivenThreeKeyframesFreeController(context, clip);

            new ResizeAnimationOperation(clip).CropOrExtendEnd(1f);

            context.Assert(target.x.keys.Select(k => k.time), new[] { 0f, 1f }, "Keyframes after resize");
            context.Assert(target.settings.Select(k => k.Key).OrderBy(k => k), new[] { 0, 1000 }, "Settings after resize");
            yield break;
        }

        public IEnumerable CropOrExtendEndLongerFloatParam(TestContext context)
        {
            var clip = context.animation.clips[0];
            var target = GivenThreeKeyframesFloatParam(context, clip);

            new ResizeAnimationOperation(clip).CropOrExtendEnd(4f);

            context.Assert(target.value.keys.Select(k => k.time), new[] { 0f, 1f, 2f, 4f }, "Keyframes after resize");
            yield break;
        }

        public IEnumerable CropOrExtendEndShorterFloatParam(TestContext context)
        {
            var clip = context.animation.clips[0];
            var target = GivenThreeKeyframesFloatParam(context, clip);

            new ResizeAnimationOperation(clip).CropOrExtendEnd(1f);

            context.Assert(target.value.keys.Select(k => k.time), new[] { 0f, 1f }, "Keyframes after resize");
            yield break;
        }

        public IEnumerable CropOrExtendEndLongerTrigger(TestContext context)
        {
            var clip = context.animation.clips[0];
            var target = GivenThreeKeyframesTrigger(context, clip);

            new ResizeAnimationOperation(clip).CropOrExtendEnd(4f);

            context.Assert(target.triggersMap.Select(k => k.Key).OrderBy(k => k), new[] { 0, 1000, 2000, 4000 }, "Map after resize");
            yield break;
        }

        public IEnumerable CropOrExtendEndShorterTrigger(TestContext context)
        {
            var clip = context.animation.clips[0];
            var target = GivenThreeKeyframesTrigger(context, clip);

            new ResizeAnimationOperation(clip).CropOrExtendEnd(1f);

            context.Assert(target.triggersMap.Select(k => k.Key).OrderBy(k => k), new[] { 0, 1000 }, "Map after resize");
            yield break;
        }

        #endregion

        #region CropOrExtendBegin

        public IEnumerable CropOrExtendBeginLongerFreeController(TestContext context)
        {
            var clip = context.animation.clips[0];
            var target = GivenThreeKeyframesFreeController(context, clip);

            new ResizeAnimationOperation(clip).CropOrExtendAt(4f, 0f);

            context.Assert(target.x.keys.Select(k => k.time), new[] { 0f, 2f, 3f, 4f }, "Keyframes after resize");
            context.Assert(target.settings.Select(k => k.Key).OrderBy(k => k), new[] { 0, 2000, 3000, 4000 }, "Settings after resize");
            yield break;
        }

        public IEnumerable CropOrExtendBeginShorterFreeController(TestContext context)
        {
            var clip = context.animation.clips[0];
            var target = GivenThreeKeyframesFreeController(context, clip);

            new ResizeAnimationOperation(clip).CropOrExtendAt(1f, 0f);

            context.Assert(target.x.keys.Select(k => k.time), new[] { 0f, 1f }, "Keyframes after resize");
            context.Assert(target.settings.Select(k => k.Key).OrderBy(k => k), new[] { 0, 1000 }, "Settings after resize");
            yield break;
        }

        public IEnumerable CropOrExtendBeginLongerFloatParam(TestContext context)
        {
            var clip = context.animation.clips[0];
            var target = GivenThreeKeyframesFloatParam(context, clip);

            new ResizeAnimationOperation(clip).CropOrExtendAt(4f, 0f);

            context.Assert(target.value.keys.Select(k => k.time), new[] { 0f, 2f, 3f, 4f }, "Keyframes after resize");
            yield break;
        }

        public IEnumerable CropOrExtendBeginShorterFloatParam(TestContext context)
        {
            var clip = context.animation.clips[0];
            var target = GivenThreeKeyframesFloatParam(context, clip);

            new ResizeAnimationOperation(clip).CropOrExtendAt(1f, 0f);

            context.Assert(target.value.keys.Select(k => k.time), new[] { 0f, 1f }, "Keyframes after resize");
            yield break;
        }

        public IEnumerable CropOrExtendBeginLongerTrigger(TestContext context)
        {
            var clip = context.animation.clips[0];
            var target = GivenThreeKeyframesTrigger(context, clip);

            new ResizeAnimationOperation(clip).CropOrExtendAt(4f, 0f);

            context.Assert(target.triggersMap.Select(k => k.Key).OrderBy(k => k), new[] { 0, 2000, 3000, 4000 }, "Map after resize");
            yield break;
        }

        public IEnumerable CropOrExtendBeginShorterTrigger(TestContext context)
        {
            var clip = context.animation.clips[0];
            var target = GivenThreeKeyframesTrigger(context, clip);

            new ResizeAnimationOperation(clip).CropOrExtendAt(1f, 0f);

            context.Assert(target.triggersMap.Select(k => k.Key).OrderBy(k => k), new[] { 0, 1000 }, "Map after resize");
            yield break;
        }

        #endregion

        #region Setup

        private static FreeControllerAnimationTarget GivenThreeKeyframesFreeController(TestContext context, AtomAnimationClip clip)
        {
            var controller = new GameObject("Test Controller");
            controller.SetActive(false);
            controller.transform.SetParent(context.gameObject.transform, false);
            var fc = controller.AddComponent<FreeControllerV3>();
            fc.UITransforms = new Transform[0];
            fc.UITransformsPlayMode = new Transform[0];
            var target = clip.Add(fc);
            context.Assert(clip.animationLength, 2f, "Default animation length");
            target.SetKeyframe(0f, Vector3.zero, Quaternion.identity);
            target.SetKeyframe(1f, Vector3.one, Quaternion.identity);
            target.SetKeyframe(2f, Vector3.zero, Quaternion.identity);
            context.animation.RebuildAnimationNow();
            context.Assert(target.x.keys.Select(k => k.time), new[] { 0f, 1f, 2f }, "Keyframes before resize");
            context.Assert(target.settings.Select(k => k.Key).OrderBy(k => k), new[] { 0, 1000, 2000 }, "Settings before resize");
            return target;
        }

        private static FloatParamAnimationTarget GivenThreeKeyframesFloatParam(TestContext context, AtomAnimationClip clip)
        {
            var target = clip.Add(new JSONStorable(), new JSONStorableFloat("Test", 0, 0, 1));
            context.Assert(clip.animationLength, 2f, "Default animation length");
            target.SetKeyframe(0f, 0f);
            target.SetKeyframe(1f, 1f);
            target.SetKeyframe(2f, 0f);
            context.animation.RebuildAnimationNow();
            context.Assert(target.value.keys.Select(k => k.time), new[] { 0f, 1f, 2f }, "Keyframes before resize");
            return target;
        }

        private static TriggersAnimationTarget GivenThreeKeyframesTrigger(TestContext context, AtomAnimationClip clip)
        {
            var target = clip.Add(new TriggersAnimationTarget());
            context.Assert(clip.animationLength, 2f, "Default animation length");
            target.SetKeyframe(0f, new AtomAnimationTrigger());
            target.SetKeyframe(1f, new AtomAnimationTrigger());
            target.SetKeyframe(2f, new AtomAnimationTrigger());
            context.animation.RebuildAnimationNow();
            context.Assert(target.triggersMap.Select(k => k.Key).OrderBy(k => k), new[] { 0, 1000, 2000 }, "Map before resize");
            return target;
        }

        #endregion
    }
}
