using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace VamTimeline
{
    public class ImportOperationTests : ITestClass
    {
        public IEnumerable<Test> GetTests()
        {
            yield return new Test(nameof(CanAddTarget_PerfectMatch), CanAddTarget_PerfectMatch);
            yield return new Test(nameof(CanAddTarget_PerfectMatch_ChooseNewSegment), CanAddTarget_PerfectMatch_ChooseNewSegment);
            yield return new Test(nameof(CanAddTarget_NewLayer), CanAddTarget_NewLayer);
        }

        // TODO: Non-segment import
        // TODO: Import from shared
        // TODO: Import into shared

        private IEnumerable CanAddTarget_PerfectMatch(TestContext context)
        {
            var ctx = new ImportTestContext(context);
            context.animation.RemoveClip(context.animation.clips[0]);
            {
                var clip = new AtomAnimationClip("Anim 1", "Layer 1", "Segment 1", context.logger);
                clip.Add(ctx.helper.GivenFreeController("C1")).AddEdgeFramesIfMissing(clip.animationLength);
                clip.Add(ctx.helper.GivenFloatParam("F1")).AddEdgeFramesIfMissing(clip.animationLength);
                clip.Add(ctx.helper.GivenTriggers(clip.animationLayerQualifiedId, "T1")).AddEdgeFramesIfMissing(clip.animationLength);
                context.animation.AddClip(clip);
            }
            {
                var clip = ctx.imported;
                clip.Add(ctx.helper.GivenFreeController("C1")).AddEdgeFramesIfMissing(clip.animationLength);
                clip.Add(ctx.helper.GivenFloatParam("F1")).AddEdgeFramesIfMissing(clip.animationLength);
                clip.Add(ctx.helper.GivenTriggers(clip.animationLayerQualifiedId, "T1")).AddEdgeFramesIfMissing(clip.animationLength);
            }

            ctx.PopulateValidChoices();

            context.Assert(ctx.okJSON.val, true, "OK");
            context.Assert(ctx.nameJSON.val, "Anim IMPORTED", "Name");
            context.Assert(ctx.layerJSON.val, "Layer 1", "Layer");
            context.Assert(ctx.layerJSON.choices, new[] { "Layer 1" }, "Layer choices");
            context.Assert(ctx.segmentJSON.val, "Segment 1", "Segment");
            context.Assert(ctx.segmentJSON.choices, new[] { "Segment 1", "Segment IMPORTED" }, "Segment choices");
            context.Assert(ctx.statusJSON.val, @"", "Status");

            ctx.ProcessImportedClip();

            context.Assert(ctx.imported.allowImport, true, "Allow import");
            context.Assert(ctx.imported.animationSegment, "Segment 1", "Processed segment name");
            context.Assert(ctx.imported.animationLayer, "Layer 1", "Processed layer name");
            context.Assert(ctx.imported.animationName, "Anim IMPORTED", "Processed animation name");

            ctx.ImportClips();

            AtomAnimationsClipsIndex.IndexedSegment segmentIndex;
            List<AtomAnimationClip> layerClips = null;
            context.Assert(context.animation.index.segmentNames, new[] { "Segment 1" }, "Segments once imported");
            context.Assert(context.animation.index.segmentsById.TryGetValue("Segment 1".ToId(), out segmentIndex), true, "Segment imported exists");
            context.Assert(segmentIndex?.layerNames, new[] { "Layer 1" }, "Layers once imported");
            context.Assert(segmentIndex?.layersMapById.TryGetValue("Layer 1".ToId(), out layerClips), true, "Layer imported exists");
            context.Assert(layerClips?.Select(c => c.animationName), new[] { "Anim 1", "Anim IMPORTED" }, "Layers once imported");

            yield break;
        }

        private IEnumerable CanAddTarget_PerfectMatch_ChooseNewSegment(TestContext context)
        {
            var ctx = new ImportTestContext(context);
            context.animation.RemoveClip(context.animation.clips[0]);
            {
                var clip = new AtomAnimationClip("Anim 1", "Layer 1", "Segment 1", context.logger);
                clip.Add(ctx.helper.GivenFreeController("C1")).AddEdgeFramesIfMissing(clip.animationLength);
                clip.Add(ctx.helper.GivenFloatParam("F1")).AddEdgeFramesIfMissing(clip.animationLength);
                clip.Add(ctx.helper.GivenTriggers(clip.animationLayerQualifiedId, "T1")).AddEdgeFramesIfMissing(clip.animationLength);
                context.animation.AddClip(clip);
            }
            {
                var clip = ctx.imported;
                clip.Add(ctx.helper.GivenFreeController("C1")).AddEdgeFramesIfMissing(clip.animationLength);
                clip.Add(ctx.helper.GivenFloatParam("F1")).AddEdgeFramesIfMissing(clip.animationLength);
                clip.Add(ctx.helper.GivenTriggers(clip.animationLayerQualifiedId, "T1")).AddEdgeFramesIfMissing(clip.animationLength);
            }

            ctx.PopulateValidChoices();
            ctx.segmentJSON.val = "Segment IMPORTED";

            context.Assert(ctx.okJSON.val, true, "OK");
            context.Assert(ctx.nameJSON.val, "Anim IMPORTED", "Name");
            context.Assert(ctx.layerJSON.val, "Layer IMPORTED", "Layer");
            context.Assert(ctx.layerJSON.choices, new[] { "Layer IMPORTED" }, "Layer choices");
            context.Assert(ctx.segmentJSON.val, "Segment IMPORTED", "Segment");
            context.Assert(ctx.segmentJSON.choices, new[] { "Segment 1", "Segment IMPORTED" }, "Segment choices");
            context.Assert(ctx.statusJSON.val, @"", "Status");

            ctx.ProcessImportedClip();

            context.Assert(ctx.imported.allowImport, true, "Allow import");
            context.Assert(ctx.imported.animationSegment, "Segment IMPORTED", "Processed segment name");
            context.Assert(ctx.imported.animationLayer, "Layer IMPORTED", "Processed layer name");
            context.Assert(ctx.imported.animationName, "Anim IMPORTED", "Processed animation name");

            ctx.ImportClips();

            AtomAnimationsClipsIndex.IndexedSegment segmentIndex;
            List<AtomAnimationClip> layerClips = null;
            context.Assert(context.animation.index.segmentNames, new[] { "Segment 1", "Segment IMPORTED" }, "Segments once imported");
            context.Assert(context.animation.index.segmentsById.TryGetValue("Segment IMPORTED".ToId(), out segmentIndex), true, "Segment imported exists");
            context.Assert(segmentIndex?.layerNames, new[] { "Layer IMPORTED" }, "Layers once imported");
            context.Assert(segmentIndex?.layersMapById.TryGetValue("Layer IMPORTED".ToId(), out layerClips), true, "Layer imported exists");
            context.Assert(layerClips?.Select(c => c.animationName), new[] { "Anim IMPORTED" }, "Layers once imported");

            yield break;
        }

        private IEnumerable CanAddTarget_NewLayer(TestContext context)
        {
            var ctx = new ImportTestContext(context);
            context.animation.RemoveClip(context.animation.clips[0]);
            {
                var clip = new AtomAnimationClip("Anim 1", "Layer 1", "Segment 1", context.logger);
                clip.Add(ctx.helper.GivenFreeController("C1")).AddEdgeFramesIfMissing(clip.animationLength);
                clip.Add(ctx.helper.GivenFloatParam("F1")).AddEdgeFramesIfMissing(clip.animationLength);
                clip.Add(ctx.helper.GivenTriggers(clip.animationLayerQualifiedId, "T1")).AddEdgeFramesIfMissing(clip.animationLength);
                context.animation.AddClip(clip);
            }
            {
                var clip = ctx.imported;
                clip.Add(ctx.helper.GivenFreeController("C2")).AddEdgeFramesIfMissing(clip.animationLength);
                clip.Add(ctx.helper.GivenFloatParam("F2")).AddEdgeFramesIfMissing(clip.animationLength);
                clip.Add(ctx.helper.GivenTriggers(clip.animationLayerQualifiedId, "T2")).AddEdgeFramesIfMissing(clip.animationLength);
            }

            ctx.PopulateValidChoices();

            context.Assert(ctx.okJSON.val, true, "OK");
            context.Assert(ctx.nameJSON.val, "Anim IMPORTED", "Name");
            context.Assert(ctx.layerJSON.val, "Layer IMPORTED", "Layer");
            context.Assert(ctx.layerJSON.choices, new[] { "Layer IMPORTED" }, "Layer choices");
            context.Assert(ctx.segmentJSON.val, "Segment IMPORTED", "Segment");
            context.Assert(ctx.segmentJSON.choices, new[] { "Segment IMPORTED" }, "Segment choices");
            context.Assert(ctx.statusJSON.val, @"", "Status");

            ctx.ProcessImportedClip();

            context.Assert(ctx.imported.allowImport, true, "Allow import");
            context.Assert(ctx.imported.animationSegment, "Segment IMPORTED", "Processed segment name");
            context.Assert(ctx.imported.animationLayer, "Layer IMPORTED", "Processed layer name");
            context.Assert(ctx.imported.animationName, "Anim IMPORTED", "Processed animation name");

            ctx.ImportClips();

            AtomAnimationsClipsIndex.IndexedSegment segmentIndex;
            List<AtomAnimationClip> layerClips = null;
            context.Assert(context.animation.index.segmentNames, new[] { "Segment 1", "Segment IMPORTED" }, "Segments once imported");
            context.Assert(context.animation.index.segmentsById.TryGetValue("Segment IMPORTED".ToId(), out segmentIndex), true, "Segment imported exists");
            context.Assert(segmentIndex?.layerNames, new[] { "Layer IMPORTED" }, "Layers once imported");
            context.Assert(segmentIndex?.layersMapById.TryGetValue("Layer IMPORTED".ToId(), out layerClips), true, "Layer imported exists");
            context.Assert(layerClips?.Select(c => c.animationName), new[] { "Anim IMPORTED" }, "Layers once imported");

            yield break;
        }

        private class ImportTestContext
        {
            private readonly ImportOperations _op;

            public readonly JSONStorableBool okJSON;
            public readonly JSONStorableStringChooser segmentJSON;
            public readonly JSONStorableStringChooser layerJSON;
            public readonly JSONStorableString nameJSON;
            public readonly JSONStorableString statusJSON;
            public readonly JSONStorableBool allowJSON;
            public readonly TargetsHelper helper;
            public readonly AtomAnimationClip imported;

            public ImportTestContext(TestContext context)
            {
                helper = new TargetsHelper(context);
                statusJSON = new JSONStorableString("Status", "");
                nameJSON = new JSONStorableString("Name", "", (string _) => PopulateValidChoices());
                layerJSON = new JSONStorableStringChooser("Layer", new List<string>(), "", "", (string _) => PopulateValidChoices());
                segmentJSON = new JSONStorableStringChooser("Layer", new List<string>(), "", "", (string _) => PopulateValidChoices());
                okJSON = new JSONStorableBool("Ok", false);
                allowJSON = new JSONStorableBool("Import", true);
                _op = new ImportOperations(context.animation);
                imported = new AtomAnimationClip("Anim IMPORTED", "Layer IMPORTED", "Segment IMPORTED", context.logger);

            }

            public void PopulateValidChoices()
            {
                _op.PopulateValidChoices(imported, statusJSON, nameJSON, layerJSON, segmentJSON, okJSON);
            }

            public void ProcessImportedClip()
            {
                ImportOperations.ProcessImportedClip(imported, statusJSON, nameJSON, layerJSON, segmentJSON, okJSON, allowJSON);
            }

            public void ImportClips()
            {
                _op.ImportClips(new List<AtomAnimationClip>(new[] { imported }));
            }
        }
    }
}
