using System;
using System.Linq;
using Jakobian.Core;

internal static partial class Program
{
    private static void AdmissionChecks()
    {
        var bindings = Enumerable.Range(0, 22).ToArray();
        foreach (var state in new[] { WorldCompatibility.Compatible, WorldCompatibility.Additive, WorldCompatibility.LegacyUnverified })
        {
            var result = AdmissionPolicy.Evaluate(state, true, new[] { 0, 1, 17, 21 }, bindings);
            Assert(result.Allowed && result.WorldStatus == state, "supported load allowed without relabeling state");
            Assert((result.Reason == AdmissionReason.AllowedLegacyUnverified) == (state == WorldCompatibility.LegacyUnverified), "legacy is permission not compatibility proof");
        }
        foreach (var state in new[] { WorldCompatibility.Incompatible, WorldCompatibility.InvalidRecord, WorldCompatibility.UnsupportedSchema, WorldCompatibility.UnsupportedRecord, WorldCompatibility.Unavailable, (WorldCompatibility)100 })
            Assert(!AdmissionPolicy.Evaluate(state, true, new int[0], bindings).Allowed, "unknown/unsafe world denied");
        Assert(!default(AdmissionDecision).Allowed, "default result never admits");
        Assert(!AdmissionPolicy.Evaluate(WorldCompatibility.Compatible, false, new int[0], bindings).Allowed, "detached capability denied even without vessels");
        Assert(!AdmissionPolicy.Evaluate(WorldCompatibility.LegacyUnverified, true, new[] { 22 }, bindings).Allowed, "legacy missing native index denied");
        Assert(!AdmissionPolicy.Evaluate(WorldCompatibility.Compatible, true, new[] { -1 }, bindings).Allowed, "negative native reference denied");
        Assert(!AdmissionPolicy.Evaluate(WorldCompatibility.Compatible, true, new int[0], new[] { 1, 1 }).Allowed, "ambiguous binding denied");
        Assert(!AdmissionPolicy.Evaluate(WorldCompatibility.Compatible, true, null, bindings).Allowed, "missing reference collection denied");
        Assert(AdmissionPolicy.Evaluate(WorldCompatibility.LegacyUnverified, true, new int[0], new int[0]).Allowed, "empty native startup can proceed");
        Assert(AdmissionPolicy.Evaluate(WorldCompatibility.Compatible, true, Enumerable.Repeat(1, AdmissionPolicy.MaximumVessels + 1).ToArray(), bindings).Reason == AdmissionReason.ReferenceBudgetExceeded, "inspection bound enforced");
        var random = new Random(20261027);
        for (int i = 0; i < 1000; i++)
        {
            int index = random.Next(-100, 200);
            Assert(AdmissionPolicy.Evaluate(WorldCompatibility.LegacyUnverified, true, new[] { index }, bindings).Allowed == (index >= 0 && index < 22), "independent native membership oracle");
        }
    }
}
