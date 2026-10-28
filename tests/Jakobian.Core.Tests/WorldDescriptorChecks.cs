using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jakobian.Core;

internal static partial class Program
{
    private static void WorldDescriptorChecks()
    {
        WorldBodyDescriptor Body(string id = "test/one", string name = "One", int index = 17, string field = null, string value = null)
        {
            var p = new Dictionary<string, string> { { "provider", "test/radial/v1" }, { "generator", "radial-mapped/v1" },
                { "shape.x", "1000" }, { "map.height.sha256", WorldBodyDescriptor.HashBytes(new byte[] { 1, 2, 3 }) },
                { "gravity.mu", "50000000" }, { "spin.period", "21600" }, { "orbit.parent", "Kerbin" } };
            if (field != null) p[field] = value;
            return new WorldBodyDescriptor(id, name, index, p.Select(x => new WorldProperty(x.Key, x.Value)));
        }
        var one = Body(); var two = Body("test/two", "Two", 18);
        var prior = new WorldCatalog(new[] { one });
        Assert(WorldBodyDescriptor.HashBytes(System.Text.Encoding.ASCII.GetBytes("abc")) == "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", "SHA256 independent known vector");
        Assert(WorldReconciliation.Compare(prior, new WorldCatalog(new[] { Body() })).Status == WorldCompatibility.Compatible, "exact descriptors match");
        Assert(WorldReconciliation.Compare(null, prior).Status == WorldCompatibility.LegacyUnverified, "legacy not silently adopted");
        Assert(WorldReconciliation.Compare(prior, null).Status == WorldCompatibility.Unavailable, "unavailable catalog not compatible");
        foreach (string field in new[] { "provider", "generator", "shape.x", "map.height.sha256", "gravity.mu", "spin.period", "orbit.parent", "future.parameter" })
        {
            var changed = new WorldCatalog(new[] { Body(field: field, value: "changed") });
            var result = WorldReconciliation.Compare(prior, changed);
            Assert(result.Status == WorldCompatibility.Incompatible && !result.CanRecordAdditions, "changed field incompatible: " + field);
            Assert(result.Differences.Any(d => d.Kind == WorldDifferenceKind.Property && d.Field == field), "field diagnostic: " + field);
            Assert(prior.Bodies[0].Fingerprint == one.Fingerprint, "comparison does not mutate saved descriptor");
        }
        foreach (var changed in new[] { Body(name: "Renamed"), Body(index: 19) })
            Assert(WorldReconciliation.Compare(prior, new WorldCatalog(new[] { changed })).Status == WorldCompatibility.Incompatible, "native binding change incompatible");
        var renamed = WorldReconciliation.Compare(prior, new WorldCatalog(new[] { Body("test/renamed") }));
        Assert(renamed.Status == WorldCompatibility.Incompatible && renamed.Differences.Any(d => d.Kind == WorldDifferenceKind.MissingBody), "stable ID rename is missing old identity");
        Assert(WorldReconciliation.Compare(prior, new WorldCatalog(new WorldBodyDescriptor[0])).Status == WorldCompatibility.Incompatible, "provider/body removal incompatible");
        var additive = WorldReconciliation.Compare(prior, new WorldCatalog(new[] { two, one }));
        Assert(additive.Status == WorldCompatibility.Additive && additive.CanRecordAdditions && additive.Differences.Count == 1, "safe addition explicit");
        Assert(WorldReconciliation.Compare(new WorldCatalog(new[] { one, two }), prior).Status == WorldCompatibility.Incompatible, "removal not an addition");
        var text = new[] { new WorldProperty("provider", "p"), new WorldProperty("generator", "g"), new WorldProperty("unicode", "床\n};=//é") };
        var descriptor = new WorldBodyDescriptor("test/text", "Text", 20, text);
        string frozen = descriptor.Fingerprint;
        text[0] = new WorldProperty("provider", "mutated");
        Assert(descriptor.Fingerprint == frozen && descriptor.Properties.Any(p => p.Key == "provider" && p.Value == "p"), "descriptor owns input copy");
        var input = new List<WorldBodyDescriptor> { one }; var catalog = new WorldCatalog(input); input.Clear();
        Assert(catalog.Bodies.Count == 1, "catalog owns input copy");
        bool rejected = false;
        void Reject(Action action, string message) { rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Assert(rejected, message); }
        Reject(() => new WorldCatalog(new[] { one, one }), "duplicate ID rejected");
        Reject(() => new WorldCatalog(new[] { one, Body("test/different", "One", 22) }), "duplicate native name rejected");
        Reject(() => new WorldCatalog(new[] { one, Body("test/different", "Different", 17) }), "duplicate native index rejected");
        Reject(() => new WorldBodyDescriptor("bad\n", "One", 17, one.Properties), "trailing newline identity rejected");
        Reject(() => new WorldBodyDescriptor("test/x", "One", 17, new[] { default(WorldProperty) }), "default property rejected");
        Reject(() => new WorldBodyDescriptor("test/x", "One", 17, one.Properties.Concat(one.Properties)), "duplicate properties rejected");
        Reject(() => new WorldBodyDescriptor("test/x", "One", 17, new[] { new WorldProperty("provider", "p") }), "missing generator rejected");
        Reject(() => new WorldProperty("oversized", new string('x', 4097)), "property text bound");
        Reject(() => new WorldBodyDescriptor("test/x", "One", 17, Enumerable.Range(0, 1025).Select(i => new WorldProperty("p" + i, "x"))), "property count bound");
        Reject(() => Body(field: "provider", value: ""), "empty provider rejected");
        var palette = new List<WorldProperty> { new WorldProperty("provider", "p"), new WorldProperty("generator", "g") };
        for (int i = 0; i < 256; i++) { palette.Add(new WorldProperty("biome." + i + ".id", new string('x', 64))); palette.Add(new WorldProperty("biome." + i + ".value", "100")); }
        Assert(new WorldBodyDescriptor("test/palette", "Palette", 23, palette).Properties.Count == 514, "full supported 256-biome palette fits descriptor");
        Reject(() => new WorldBodyDescriptor("test/x", "One", 17, Enumerable.Range(0, 32).Select(i => new WorldProperty("p" + i, new string('x', 4096)))), "total body text bound");
        var random = new Random(20261027); var culture = CultureInfo.CurrentCulture;
        try
        {
            foreach (string locale in new[] { "fr-FR", "tr-TR", "en-US" })
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(locale);
                for (int i = 0; i < 100; i++)
                {
                    var shuffled = one.Properties.OrderBy(_ => random.Next()).ToArray();
                    Assert(new WorldBodyDescriptor(one.Id, one.NativeName, one.NativeIndex, shuffled).Fingerprint == one.Fingerprint, "canonical property order/culture");
                    var shuffledCatalog = new WorldCatalog((i % 2 == 0) ? new[] { one, two } : new[] { two, one });
                    Assert(shuffledCatalog.Fingerprint == new WorldCatalog(new[] { one, two }).Fingerprint, "canonical catalog order/culture");
                    Assert(WorldReconciliation.Compare(new WorldCatalog(new[] { one, two }), shuffledCatalog).Status == WorldCompatibility.Compatible, "ordering does not change compatibility");
                }
            }
        }
        finally { CultureInfo.CurrentCulture = culture; }
    }
}
