using System.Collections.Generic;

namespace Jakobian.Core
{
    public enum AdmissionReason { DeniedUnknown, AllowedRecorded, AllowedLegacyUnverified, IncompatibleWorld, UnsupportedRecord, UnsupportedCapability, UnavailableCatalog, InvalidNativeBindings, InvalidReference, ReferenceBudgetExceeded }
    public readonly struct AdmissionDecision
    {
        public readonly bool Allowed;
        public readonly AdmissionReason Reason;
        public readonly WorldCompatibility WorldStatus;
        public readonly string Detail;
        internal AdmissionDecision(bool allowed, AdmissionReason reason, WorldCompatibility world, string detail)
        { Allowed = allowed; Reason = reason; WorldStatus = world; Detail = detail; }
        public static AdmissionDecision Denied(AdmissionReason reason, WorldCompatibility world, string detail) => new AdmissionDecision(false, reason, world, detail);
    }
    public static class AdmissionPolicy
    {
        public const int MaximumVessels = 100000;
        public static AdmissionDecision Evaluate(WorldCompatibility world, bool supportedCapabilities,
            IReadOnlyList<int> vesselReferences, IReadOnlyList<int> nativeIndices)
        {
            if (!supportedCapabilities) return AdmissionDecision.Denied(AdmissionReason.UnsupportedCapability, world, "Unsupported body capability/provider; no flight admission granted.");
            switch (world)
            {
                case WorldCompatibility.Compatible: case WorldCompatibility.Additive: case WorldCompatibility.LegacyUnverified: break;
                case WorldCompatibility.Incompatible: return AdmissionDecision.Denied(AdmissionReason.IncompatibleWorld, world, "Saved world inputs differ or a recorded body is missing.");
                case WorldCompatibility.Unavailable: return AdmissionDecision.Denied(AdmissionReason.UnavailableCatalog, world, "Required published world catalog is unavailable.");
                default: return AdmissionDecision.Denied(AdmissionReason.UnsupportedRecord, world, "Saved-world data is invalid, unknown or from an unsupported schema.");
            }
            if (nativeIndices == null || nativeIndices.Count > 4096) return AdmissionDecision.Denied(AdmissionReason.InvalidNativeBindings, world, "Invalid native binding registry.");
            var known = new HashSet<int>();
            foreach (int index in nativeIndices) if (index < 0 || !known.Add(index)) return AdmissionDecision.Denied(AdmissionReason.InvalidNativeBindings, world, "Ambiguous native body indices.");
            if (vesselReferences == null) return AdmissionDecision.Denied(AdmissionReason.InvalidReference, world, "Missing native reference data.");
            if (vesselReferences.Count > MaximumVessels) return AdmissionDecision.Denied(AdmissionReason.ReferenceBudgetExceeded, world, "Native reference inspection budget exceeded.");
            foreach (int index in vesselReferences) if (!known.Contains(index)) return AdmissionDecision.Denied(AdmissionReason.InvalidReference, world, "Vessel references an unavailable native body index.");
            bool legacy = world == WorldCompatibility.LegacyUnverified;
            return new AdmissionDecision(true, legacy ? AdmissionReason.AllowedLegacyUnverified : AdmissionReason.AllowedRecorded, world,
                legacy ? "Legacy radial/native state admitted without historical compatibility proof." : "Recorded world and native references admitted.");
        }
    }
}
