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
            yield return new Test(nameof(ImportOperationsTests.MatchToLayer), MatchToLayer);
            yield return new Test(nameof(ImportOperationsTests.AddMissingTargets), AddMissingTargets);
        }

        public IEnumerable OverwriteEmptyClip(TestContext context)
        {
            var existing = context.animation.clips.Single();
            var clip = new AtomAnimationClip(existing.animationName, "New Layer");

            new ImportOperations(context.animation, true).ImportClips(new[] { clip });

            context.Assert(context.animation.clips.Count, 1, "When the animation is empty, replace it");
            context.Assert(clip.animationLayer, "Main Layer", "The existing animation layer is used");
            yield break;
        }

        public IEnumerable MatchToLayer(TestContext context)
        {
            var existing = WithStorable(context, context.animation.clips.Single(), "floatparam1");
            var clip = WithStorable(context, new AtomAnimationClip(existing.animationName, "some other layer name"), "floatparam1");

            new ImportOperations(context.animation, true).ImportClips(new[] { clip });

            context.Assert(context.animation.clips.Count, 2, "The animation is added");
            context.Assert(context.animation.EnumerateLayers().Count(), 1, "They all share the same layer");
            yield break;
        }

        public IEnumerable AddMissingTargets(TestContext context)
        {
            var existing = WithStorable(context, context.animation.clips.Single(), "floatparam1");
            WithStorable(context, existing, "floatparam2");
            var clip = WithStorable(context, new AtomAnimationClip(existing.animationName, "any"), "floatparam1");

            new ImportOperations(context.animation, true).ImportClips(new[] { clip });

            context.Assert(context.animation.clips.Count, 2, "The animation is added");
            context.Assert(context.animation.EnumerateLayers().Count(), 1, "They all share the same layer");
            context.Assert(clip.targetFloatParams.Count, 2, "Added the float param");
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
