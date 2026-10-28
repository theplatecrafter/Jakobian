using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Jakobian.Core
{
    public readonly struct WorldProperty
    {
        public readonly string Key, Value;
        public WorldProperty(string key, string value)
        {
            if (key == null || !Regex.IsMatch(key, @"\A[A-Za-z0-9][A-Za-z0-9_./-]{0,95}\z")
                || value == null || value.Length > 4096) throw new ArgumentException("Invalid world property.");
            Key = key; Value = value;
        }
    }

    /// <summary>Immutable versioned inputs, not an executable generator or a vessel migration.</summary>
    public sealed class WorldBodyDescriptor
    {
        public string Id { get; }
        public string NativeName { get; }
        public int NativeIndex { get; }
        public IReadOnlyList<WorldProperty> Properties { get; }
        public string Fingerprint { get; }
        public WorldBodyDescriptor(string id, string nativeName, int nativeIndex, IEnumerable<WorldProperty> properties)
        {
            if (id == null || !Regex.IsMatch(id, @"\A[A-Za-z0-9][A-Za-z0-9_./-]{0,127}\z")
                || nativeName == null || !Regex.IsMatch(nativeName, @"\A[A-Za-z][A-Za-z0-9_]{0,63}\z")
                || nativeIndex < 0 || properties == null) throw new ArgumentException("Invalid world identity.");
            var copy = new List<WorldProperty>(); var keys = new HashSet<string>(StringComparer.Ordinal); int textBytes = 0;
            foreach (var p in properties)
            {
                // Revalidate default structs and stop unbounded enumerations at the record limit.
                var valid = new WorldProperty(p.Key, p.Value);
                textBytes += Encoding.UTF8.GetByteCount(valid.Key) + Encoding.UTF8.GetByteCount(valid.Value);
                if (textBytes > 65536) throw new ArgumentException("World body text exceeds 64 KiB.");
                if (copy.Count == 1024 || !keys.Add(valid.Key)) throw new ArgumentException("Duplicate or excessive world properties.");
                copy.Add(valid);
            }
            if (!keys.Contains("provider") || !keys.Contains("generator")) throw new ArgumentException("Provider and generator are required.");
            foreach (var p in copy)
                if ((p.Key == "provider" || p.Key == "generator") && !Regex.IsMatch(p.Value, @"\A[A-Za-z0-9][A-Za-z0-9_./-]{0,127}\z"))
                    throw new ArgumentException("Provider/generator identifiers must be nonempty canonical tokens.");
            copy.Sort((a, b) => StringComparer.Ordinal.Compare(a.Key, b.Key));
            Id = id; NativeName = nativeName; NativeIndex = nativeIndex; Properties = copy.AsReadOnly();
            using (var bytes = new MemoryStream())
            {
                // BinaryWriter's length-prefixed UTF-8 strings and little-endian Int32 values are the v1 encoding.
                using (var writer = new BinaryWriter(bytes, new UTF8Encoding(false, true), true))
                {
                    writer.Write("Jakobian.WorldBody/v1"); writer.Write(id); writer.Write(nativeName); writer.Write(nativeIndex); writer.Write(copy.Count);
                    foreach (var p in copy) { writer.Write(p.Key); writer.Write(p.Value); }
                }
                using (var sha = SHA256.Create()) Fingerprint = Hex(sha.ComputeHash(bytes.ToArray()));
            }
        }
        public static string HashBytes(byte[] bytes)
        {
            if (bytes == null) throw new ArgumentNullException(nameof(bytes));
            using (var sha = SHA256.Create()) return Hex(sha.ComputeHash(bytes));
        }
        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    }

    public sealed class WorldCatalog
    {
        public IReadOnlyList<WorldBodyDescriptor> Bodies { get; }
        public string Fingerprint { get; }
        public WorldCatalog(IEnumerable<WorldBodyDescriptor> bodies)
        {
            if (bodies == null) throw new ArgumentNullException(nameof(bodies));
            var copy = new List<WorldBodyDescriptor>();
            var ids = new HashSet<string>(StringComparer.Ordinal); var names = new HashSet<string>(StringComparer.Ordinal); var indices = new HashSet<int>();
            foreach (var b in bodies)
            {
                if (b == null || copy.Count == 1024 || !ids.Add(b.Id) || !names.Add(b.NativeName) || !indices.Add(b.NativeIndex))
                    throw new ArgumentException("Null, duplicate or excessive world bodies.");
                copy.Add(b);
            }
            copy.Sort((a, b) => StringComparer.Ordinal.Compare(a.Id, b.Id)); Bodies = copy.AsReadOnly();
            using (var bytes = new MemoryStream())
            {
                using (var writer = new BinaryWriter(bytes, new UTF8Encoding(false, true), true))
                { writer.Write("Jakobian.WorldCatalog/v1"); writer.Write(copy.Count); foreach (var b in copy) writer.Write(b.Fingerprint); }
                Fingerprint = WorldBodyDescriptor.HashBytes(bytes.ToArray());
            }
        }
    }

    public enum WorldCompatibility { Unavailable, Compatible, Additive, LegacyUnverified, Incompatible, InvalidRecord, UnsupportedSchema, UnsupportedRecord }
    public enum WorldDifferenceKind { AddedBody, MissingBody, NativeName, NativeIndex, Property }
    public readonly struct WorldDifference
    {
        public readonly WorldDifferenceKind Kind;
        public readonly string BodyId, Field;
        internal WorldDifference(WorldDifferenceKind kind, string id, string field) { Kind = kind; BodyId = id; Field = field; }
    }
    public sealed class WorldReconciliation
    {
        public WorldCompatibility Status { get; }
        public IReadOnlyList<WorldDifference> Differences { get; }
        public bool CanRecordAdditions => Status == WorldCompatibility.Compatible || Status == WorldCompatibility.Additive;
        private WorldReconciliation(WorldCompatibility status, List<WorldDifference> differences)
        { Status = status; Differences = differences.AsReadOnly(); }
        public static WorldReconciliation State(WorldCompatibility status) => new WorldReconciliation(status, new List<WorldDifference>());
        public static WorldReconciliation Compare(WorldCatalog saved, WorldCatalog current)
        {
            if (current == null) return State(WorldCompatibility.Unavailable);
            if (saved == null) return State(WorldCompatibility.LegacyUnverified);
            var now = current.Bodies.ToDictionary(b => b.Id, StringComparer.Ordinal);
            var prior = new HashSet<string>(saved.Bodies.Select(b => b.Id), StringComparer.Ordinal);
            var changes = new List<WorldDifference>(); bool incompatible = false, added = false;
            foreach (var body in saved.Bodies)
            {
                if (!now.TryGetValue(body.Id, out var live)) { changes.Add(new WorldDifference(WorldDifferenceKind.MissingBody, body.Id, "body")); incompatible = true; continue; }
                if (body.NativeName != live.NativeName) { changes.Add(new WorldDifference(WorldDifferenceKind.NativeName, body.Id, "nativeName")); incompatible = true; }
                if (body.NativeIndex != live.NativeIndex) { changes.Add(new WorldDifference(WorldDifferenceKind.NativeIndex, body.Id, "nativeIndex")); incompatible = true; }
                var oldProperties = body.Properties.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
                var newProperties = live.Properties.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
                foreach (string key in oldProperties.Keys.Union(newProperties.Keys, StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal))
                    if (!oldProperties.TryGetValue(key, out var oldValue) || !newProperties.TryGetValue(key, out var newValue) || oldValue != newValue)
                    { changes.Add(new WorldDifference(WorldDifferenceKind.Property, body.Id, key)); incompatible = true; }
            }
            foreach (var body in current.Bodies)
                if (!prior.Contains(body.Id)) { added = true; changes.Add(new WorldDifference(WorldDifferenceKind.AddedBody, body.Id, "body")); }
            return new WorldReconciliation(incompatible ? WorldCompatibility.Incompatible : added ? WorldCompatibility.Additive : WorldCompatibility.Compatible, changes);
        }
    }
}
