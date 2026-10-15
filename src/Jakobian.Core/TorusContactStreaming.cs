using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Jakobian.Core
{
    public readonly struct TorusPatchId : IEquatable<TorusPatchId>, IComparable<TorusPatchId>
    {
        public readonly int U, V;
        public TorusPatchId(int u, int v) { U = u; V = v; }
        public bool Equals(TorusPatchId other) => U == other.U && V == other.V;
        public override bool Equals(object obj) => obj is TorusPatchId && Equals((TorusPatchId)obj);
        public override int GetHashCode() => U * 397 ^ V;
        public int CompareTo(TorusPatchId other) { int u = U.CompareTo(other.U); return u != 0 ? u : V.CompareTo(other.V); }
        public override string ToString() => U + ":" + V;
    }

    public readonly struct ContactSweep
    {
        public readonly Vector3d From, To;
        public readonly double Radius;
        public ContactSweep(Vector3d from, Vector3d to, double radius) { From = from; To = to; Radius = radius; }
        internal bool Valid => From.IsFinite && To.IsFinite && Vector3d.Finite(From.Length) && Vector3d.Finite(To.Length)
            && From.Length <= 1e12 && To.Length <= 1e12 && Vector3d.Finite(Radius) && Radius >= 0 && Radius <= 1e12;
    }
    public enum ContactPlanStatus { Complete, InvalidInput, BudgetExceeded, CapacityExceeded }
    public sealed class TorusContactPlan
    {
        public ContactPlanStatus Status { get; }
        public IReadOnlyList<TorusPatchId> Required { get; }
        public int Evaluations { get; }
        internal TorusContactPlan(ContactPlanStatus status, IEnumerable<TorusPatchId> required, int evaluations)
        { Status = status; Required = Array.AsReadOnly(required.ToArray()); Evaluations = evaluations; }
    }

    /// <summary>Conservative patch-sphere versus swept-ball broad phase. It does not
    /// integrate motion, certify curved paths or guarantee a native cooking time.</summary>
    public sealed class TorusContactGrid
    {
        public double MajorRadius { get; }
        public double MinorRadius { get; }
        public int TilesU { get; }
        public int TilesV { get; }
        public double PatchBound { get; }
        private readonly Vector3d[] centres;
        public TorusContactGrid(double major, double minor, int tilesU, int tilesV)
        {
            var chart = new TorusChart(major,minor);
            if (tilesU < 3 || tilesU > 256 || tilesV < 3 || tilesV > 256) throw new ArgumentOutOfRangeException(nameof(tilesU));
            MajorRadius=major; MinorRadius=minor; TilesU=tilesU; TilesV=tilesV;
            // Triangle inequality on the two circular parameter directions. This
            // bounds the complete analytic patch, not merely its sampled vertices.
            PatchBound=2*(major+minor)*Math.Sin(Math.PI/(2*tilesU))+2*minor*Math.Sin(Math.PI/(2*tilesV));
            centres=new Vector3d[tilesU*tilesV];
            for (int u=0;u<tilesU;u++) for(int v=0;v<tilesV;v++)
            {
                chart.TryResolve(new TorusAnchor((u+.5)/tilesU,(v+.5)/tilesV,0),out var frame);
                centres[u*tilesV+v]=frame.Position;
            }
        }
        public bool Contains(TorusPatchId id) => id.U >= 0 && id.U < TilesU && id.V >= 0 && id.V < TilesV;
        public Vector3d Centre(TorusPatchId id) { if(!Contains(id)) throw new ArgumentOutOfRangeException(nameof(id)); return centres[id.U*TilesV+id.V]; }
        public TorusContactPlan Plan(IReadOnlyList<ContactSweep> sweeps, int maxRequired, int evaluationBudget)
        {
            var ids = new List<TorusPatchId>(); int work=0;
            TorusContactPlan Fail(ContactPlanStatus status) => new TorusContactPlan(status,new TorusPatchId[0],work);
            if(sweeps == null || sweeps.Count > 8 || maxRequired < 1 || evaluationBudget < 1) return Fail(ContactPlanStatus.InvalidInput);
            foreach(var sweep in sweeps) if(!sweep.Valid) return Fail(ContactPlanStatus.InvalidInput);
            if(sweeps.Count == 0) return new TorusContactPlan(ContactPlanStatus.Complete,ids,0);
            for(int u=0;u<TilesU;u++) for(int v=0;v<TilesV;v++)
            {
                var id = new TorusPatchId(u,v); Vector3d centre=Centre(id); bool required=false;
                foreach(var sweep in sweeps)
                {
                    if(work == evaluationBudget) return Fail(ContactPlanStatus.BudgetExceeded);
                    work++;
                    if(DistanceToSegment(centre,sweep.From,sweep.To) <= PatchBound+sweep.Radius) { required=true; break; }
                }
                if(required) { ids.Add(id); if(ids.Count > maxRequired) return Fail(ContactPlanStatus.CapacityExceeded); }
            }
            // Stable iteration order is also stable build priority. A future provider
            // can prioritize imminence without changing required coverage membership.
            return new TorusContactPlan(ContactPlanStatus.Complete,ids,work);
        }
        private static double DistanceToSegment(Vector3d p, Vector3d a, Vector3d b)
        {
            Vector3d d=b-a; double length2=Vector3d.Dot(d,d);
            double t=length2 == 0 ? 0 : Math.Max(0,Math.Min(1,Vector3d.Dot(p-a,d)/length2));
            return (p-(a+d*t)).Length;
        }
    }

    public readonly struct TorusBuildTicket
    {
        public readonly TorusPatchId Patch;
        public readonly long Revision, Serial;
        internal readonly long Owner;
        internal TorusBuildTicket(TorusPatchId patch,long revision,long serial,long owner) { Patch=patch; Revision=revision; Serial=serial; Owner=owner; }
    }

    /// <summary>Two-phase residency: old active coverage is retained until every requested
    /// patch is resident. Incomplete/rejected plans never become active coverage.</summary>
    public sealed class TorusContactResidency
    {
        private readonly TorusContactGrid grid;
        private static long nextOwner;
        private readonly long owner = Interlocked.Increment(ref nextOwner);
        private readonly Dictionary<TorusPatchId,long> resident = new Dictionary<TorusPatchId,long>();
        private readonly Dictionary<TorusPatchId,TorusBuildTicket> pending = new Dictionary<TorusPatchId,TorusBuildTicket>();
        private HashSet<TorusPatchId> active = new HashSet<TorusPatchId>(), desired = new HashSet<TorusPatchId>();
        private TorusPatchId[] order = new TorusPatchId[0];
        private long clock, serial;
        public long Revision { get; private set; }
        public int Capacity { get; }
        public int ResidentCount => resident.Count;
        public int PendingCount => pending.Count;
        private bool ready = true;
        public bool LastRequestAccepted { get; private set; } = true;
        public bool CoverageReady => LastRequestAccepted && ready;
        public IReadOnlyList<TorusPatchId> Active => Array.AsReadOnly(active.OrderBy(p=>p).ToArray());
        public TorusContactResidency(TorusContactGrid grid,int capacity)
        { this.grid=grid ?? throw new ArgumentNullException(nameof(grid)); if(capacity < 1 || capacity > 4096) throw new ArgumentOutOfRangeException(nameof(capacity)); Capacity=capacity; }
        public bool IsResident(TorusPatchId id) => resident.ContainsKey(id);
        public bool IsActive(TorusPatchId id) => active.Contains(id);
        public bool SetTarget(TorusContactPlan plan)
        {
            LastRequestAccepted=false;
            if(plan == null || plan.Status != ContactPlanStatus.Complete) return false;
            var next=new HashSet<TorusPatchId>(plan.Required);
            if(next.Any(id=>!grid.Contains(id)) || next.Count != plan.Required.Count || active.Union(next).Count() > Capacity) return false;
            LastRequestAccepted=true;
            if(desired.SetEquals(next)) return true;
            Revision=checked(Revision+1); desired=next; order=plan.Required.ToArray(); pending.Clear();
            foreach(var id in order) if(resident.ContainsKey(id)) resident[id]=checked(++clock);
            ready=false; ActivateIfReady(); return true;
        }
        public bool TryBeginBuild(out TorusBuildTicket ticket)
        {
            foreach(var id in order) if(!resident.ContainsKey(id) && !pending.ContainsKey(id))
            {
                ticket=new TorusBuildTicket(id,Revision,checked(++serial),owner); pending.Add(id,ticket); return true;
            }
            ticket=default(TorusBuildTicket); return false;
        }
        public bool Abort(TorusBuildTicket ticket)
        { if(!Current(ticket)) return false; pending.Remove(ticket.Patch); return true; }
        public bool Commit(TorusBuildTicket ticket,out TorusPatchId? evicted)
        {
            evicted=null; if(!Current(ticket)) return false;
            if(resident.Count == Capacity)
            {
                var candidates=resident.Where(p=>!active.Contains(p.Key) && !desired.Contains(p.Key)).OrderBy(p=>p.Value).ThenBy(p=>p.Key).ToArray();
                if(candidates.Length == 0) return false;
                evicted=candidates[0].Key; resident.Remove(evicted.Value);
            }
            pending.Remove(ticket.Patch); resident.Add(ticket.Patch,checked(++clock)); ActivateIfReady(); return true;
        }
        private bool Current(TorusBuildTicket ticket) => ticket.Owner == owner && ticket.Revision == Revision && desired.Contains(ticket.Patch)
            && pending.TryGetValue(ticket.Patch,out var known) && known.Serial == ticket.Serial;
        private void ActivateIfReady()
        { if(desired.All(id=>resident.ContainsKey(id))) { active=new HashSet<TorusPatchId>(desired); ready=true; } }
    }
}
