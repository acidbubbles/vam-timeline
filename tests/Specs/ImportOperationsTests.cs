using System.Collections;
using System.Collections.Generic;
using System.Linq;
using VamTimeline.Tests.Framework;

namespace VamTimeline.Tests.Specs
{
    public class ImportOperationsTests : ITestClass
    {
        public IEnumerable<Test> GetTests()
        {
            yield return new Test(nameof(ImportOperationsTests.OverwriteEmptyClip), OverwriteEmptyClip);
        }

        public IEnumerable OverwriteEmptyClip(TestContext context)
        {
            var existing = context.animation.clips.Single();
            var clip = new AtomAnimationClip(existing.animationName, "new layer");

            new ImportOperations(context.animation).ImportClips(new[] { clip });

            context.Assert(context.animation.clips.Count, 1, "When the animation is empty, replace it");
            context.Assert(clip.animationLayer, "new layer", "The imported animation layer is used");
            yield break;
        }

        public IEnumerable AddToLayer(TestContext context)
        {
            var existing = WithStorable(context, context.animation.clips.Single(), "floatparam1");
            var clip = WithStorable(context, new AtomAnimationClip(existing.animationName, existing.animationLayer), "floatparam1");

            new ImportOperations(context.animation).ImportClips(new[] { clip });

            context.Assert(context.animation.clips.Count, 2, "The animation is added");
            context.Assert(context.animation.EnumerateLayers().Count(), 1, "They all share the same layer");
            yield break;
        }

        private AtomAnimationClip WithStorable(TestContext context, AtomAnimationClip clip, string name)
        {
            var storable = context.gameObject.GetComponent<JSONStorable>() ?? context.gameObject.AddComponent<JSONStorable>();
            var target = clip.Add(new FloatParamAnimationTarget(storable, new JSONStorableFloat(name, 0, 0, 1)));
            target.AddEdgeFramesIfMissing(clip.animationLength);
            return clip;
        }
    }
}
