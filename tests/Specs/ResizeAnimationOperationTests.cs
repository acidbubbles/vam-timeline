using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VamTimeline
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

            new ResizeAnimationOperations().Stretch(clip, 4f);

            context.AssertList(target.GetAllKeyframesTime(), new[] { 0f, 2f, 4f }, "Keyframes after resize");
            yield break;
        }

        public IEnumerable StretchShorterFreeController(TestContext context)
        {
            var clip = context.animation.clips[0];
            var target = GivenThreeKeyframesFreeController(context, clip);

            new ResizeAnimationOperations().Stretch(clip, 1f);

            context.AssertList(target.GetAllKeyframesTime(), new[] { 0f, 0.5f, 1f }, "Keyframes after resize");
            yield break;
        }

        public IEnumerable StretchLongerFloatParam(TestContext context)
        {
            var clip = context.animation.clips[0];
            var target = GivenThreeKeyframesFloatParam(context, clip);

            new ResizeAnimationOperations().Stretch(clip, 4f);

            context.AssertList(target.value.keys.Select(k => k.time), new[] { 0f, 2f, 4f }, "Keyframes after resize");
            yield break;
        }

        public IEnumerable StretchShorterFloatParam(TestContext context)
        {
            var clip = context.animation.clips[0];
            var target = GivenThreeKeyframesFloatParam(context, clip);

            new ResizeAnimationOperations().Stretch(clip, 1f);

            context.AssertList(target.value.keys.Select(k => k.time), new[] { 0f, 0.5f, 1f }, "Keyframes after resize");
            yield break;
        }

        public IEnumerable StretchLongerTrigger(TestContext context)
        {
            var clip = context.animation.clips[0];
            var target = GivenThreeKeyframesTrigger(context, clip);

            new ResizeAnimationOperations().Stretch(clip, 4f);

            context.AssertList(target.triggersMap.Select(k => k.Key).OrderBy(k => k), new[] { 0, 2000, 4000 }, "Map after resize");
            yield break;
        }

        public IEnumerable StretchShorterTrigger(TestContext context)
        {
            var clip = context.animation.clips[0];
            var target = GivenThreeKeyframesTrigger(context, clip);

            new ResizeAnimationOperations().Stretch(clip, 1f);

            context.AssertList(target.triggersMap.Select(k => k.Key).OrderBy(k => k), new[] { 0, 500, 1000 }, "Map after resize");
            yield break;
        }

        #endregion

        #region CropOrExtendEnd

        public IEnumerable CropOrExtendEndLongerFreeController(TestContext context)
        {
            var clip = context.animation.clips[0];
            var target = GivenThreeKeyframesFreeController(context, clip);

            new ResizeAnimationOperations().CropOrExtendEnd(clip, 4f);

            context.AssertList(target.GetAllKeyframesTime(), new[] { 0f, 1f, 2f, 4f }, "Keyframes after resize");
            yield break;
        }

        public IEnumerable CropOrExtendEndShorterFreeController(TestContext context)
        {
            var clip = context.animation.clips[0];
            var target = GivenThreeKeyframesFreeController(context, clip);

            new ResizeAnimationOperations().CropOrExtendEnd(clip, 1f);

            context.AssertList(target.GetAllKeyframesTime(), new[] { 0f, 1f }, "Keyframes after resize");
            yield break;
        }

        public IEnumerable CropOrExtendEndLongerFloatParam(TestContext context)
        {
            var clip = context.animation.clips[0];
            var target = GivenThreeKeyframesFloatParam(context, clip);

            new ResizeAnimationOperations().CropOrExtendEnd(clip, 4f);

            context.AssertList(target.value.keys.Select(k => k.time), new[] { 0f, 1f, 2f, 4f }, "Keyframes after resize");
            yield break;
        }

        public IEnumerable CropOrExtendEndShorterFloatParam(TestContext context)
        {
            var clip = context.animation.clips[0];
            var target = GivenThreeKeyframesFloatParam(context, clip);

            new ResizeAnimationOperations().CropOrExtendEnd(clip, 1f);

            context.AssertList(target.value.keys.Select(k => k.time), new[] { 0f, 1f }, "Keyframes after resize");
            yield break;
        }

        public IEnumerable CropOrExtendEndLongerTrigger(TestContext context)
        {
            var clip = context.animation.clips[0];
            var target = GivenThreeKeyframesTrigger(context, clip);

            new ResizeAnimationOperations().CropOrExtendEnd(clip, 4f);

            context.AssertList(target.triggersMap.Select(k => k.Key).OrderBy(k => k), new[] { 0, 1000, 2000, 4000 }, "Map after resize");
            yield break;
        }

        public IEnumerable CropOrExtendEndShorterTrigger(TestContext context)
        {
            var clip = context.animation.clips[0];
            var target = GivenThreeKeyframesTrigger(context, clip);

            new ResizeAnimationOperations().CropOrExtendEnd(clip, 1f);

            context.AssertList(target.triggersMap.Select(k => k.Key).OrderBy(k => k), new[] { 0, 1000 }, "Map after resize");
            yield break;
        }

        #endregion

        #region CropOrExtendBegin

        public IEnumerable CropOrExtendBeginLongerFreeController(TestContext context)
        {
            var clip = context.animation.clips[0];
            var target = GivenThreeKeyframesFreeController(context, clip);

            new ResizeAnimationOperations().CropOrExtendAt(clip, 4f, 0f);

            context.AssertList(target.GetAllKeyframesTime(), new[] { 0f, 3f, 4f }, "Keyframes after resize");
            yield break;
        }

        public IEnumerable CropOrExtendBeginShorterFreeController(TestContext context)
        {
            var clip = context.animation.clips[0];
            var target = GivenThreeKeyframesFreeController(context, clip);

            new ResizeAnimationOperations().CropOrExtendAt(clip, 1f, 0f);

            context.AssertList(target.GetAllKeyframesTime(), new[] { 0f, 1f }, "Keyframes after resize");
            yield break;
        }

        public IEnumerable CropOrExtendBeginLongerFloatParam(TestContext context)
        {
            var clip = context.animation.clips[0];
            var target = GivenThreeKeyframesFloatParam(context, clip);

            new ResizeAnimationOperations().CropOrExtendAt(clip, 4f, 0f);

            context.AssertList(target.value.keys.Select(k => k.time), new[] { 0f, 3f, 4f }, "Keyframes after resize");
            yield break;
        }

        public IEnumerable CropOrExtendBeginShorterFloatParam(TestContext context)
        {
            var clip = context.animation.clips[0];
            var target = GivenThreeKeyframesFloatParam(context, clip);

            new ResizeAnimationOperations().CropOrExtendAt(clip, 1f, 0f);

            context.AssertList(target.value.keys.Select(k => k.time), new[] { 0f, 1f }, "Keyframes after resize");
            yield break;
        }

        public IEnumerable CropOrExtendBeginLongerTrigger(TestContext context)
        {
            var clip = context.animation.clips[0];
            var target = GivenThreeKeyframesTrigger(context, clip);

            new ResizeAnimationOperations().CropOrExtendAt(clip, 4f, 0f);

            context.AssertList(target.triggersMap.Select(k => k.Key).OrderBy(k => k), new[] { 0, 3000, 4000 }, "Map after resize");
            yield break;
        }

        public IEnumerable CropOrExtendBeginShorterTrigger(TestContext context)
        {
            var clip = context.animation.clips[0];
            var target = GivenThreeKeyframesTrigger(context, clip);

            new ResizeAnimationOperations().CropOrExtendAt(clip, 1f, 0f);

            context.AssertList(target.triggersMap.Select(k => k.Key).OrderBy(k => k), new[] { 0, 1000 }, "Map after resize");
            yield break;
        }

        #endregion

        #region Setup

        private static FreeControllerV3AnimationTarget GivenThreeKeyframesFreeController(TestContext context, AtomAnimationClip clip)
        {
            var helper = new TargetsHelper(context);
            var target = clip.AddController(helper.GivenFreeController(), true, true);
            context.Assert(clip.animationLength, 2f, "Default animation length");
            target.SetKeyframeByTime(0f, Vector3.zero, Quaternion.identity);
            target.SetKeyframeByTime(1f, Vector3.one, Quaternion.identity);
            target.SetKeyframeByTime(2f, Vector3.zero, Quaternion.identity);
            context.animation.RebuildAnimationNow();
            context.AssertList(target.GetAllKeyframesTime(), new[] { 0f, 1f, 2f }, "Keyframes before resize");
            return target;
        }

        private static JSONStorableFloatAnimationTarget GivenThreeKeyframesFloatParam(TestContext context, AtomAnimationClip clip)
        {
            var helper = new TargetsHelper(context);
            var target = clip.AddFloatParam(helper.GivenFloatParam());
            context.Assert(clip.animationLength, 2f, "Default animation length");
            target.SetKeyframe(0f, 0f);
            target.SetKeyframe(1f, 1f);
            target.SetKeyframe(2f, 0f);
            context.animation.RebuildAnimationNow();
            context.AssertList(target.value.keys.Select(k => k.time), new[] { 0f, 1f, 2f }, "Keyframes before resize");
            return target;
        }

        private static TriggersTrackAnimationTarget GivenThreeKeyframesTrigger(TestContext context, AtomAnimationClip clip)
        {
            var helper = new TargetsHelper(context);
            var target = clip.AddTriggers(helper.GivenTriggers(clip.animationLayerQualifiedId));
            context.Assert(clip.animationLength, 2f, "Default animation length");
            target.CreateKeyframe(0f.ToMilliseconds());
            target.CreateKeyframe(1f.ToMilliseconds());
            target.CreateKeyframe(2f.ToMilliseconds());
            context.animation.RebuildAnimationNow();
            context.AssertList(target.triggersMap.Select(k => k.Key).OrderBy(k => k), new[] { 0, 1000, 2000 }, "Map before resize");
            return target;
        }

        #endregion
    }
}
