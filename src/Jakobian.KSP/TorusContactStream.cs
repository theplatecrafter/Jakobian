using System;
using System.Collections.Generic;
using Jakobian.Core;
using UnityEngine;

namespace Jakobian.KSP
{
    /// <summary>Experimental main-thread contact publisher in an explicitly supplied local frame.
    /// This does not register a celestial body or grant KSP vessel/rails support.</summary>
    public sealed class TorusContactStream : IDisposable
    {
        private sealed class Entry { internal GameObject Object; internal Mesh Mesh; internal MeshCollider Collider; }
        private readonly Transform root;
        private readonly TorusContactGrid grid;
        private readonly TorusContactResidency residency;
        private readonly Dictionary<TorusPatchId,Entry> entries=new Dictionary<TorusPatchId,Entry>();
        private readonly int segments, maxRequired;
        private bool disposed;
        private int buildFrame=-1, attemptsThisFrame;
        public int ResidentCount => entries.Count;
        public int ActiveCount => disposed ? 0 : residency.Active.Count;
        public int Capacity => residency.Capacity;
        public int Builds { get; private set; }
        public int Evictions { get; private set; }
        public bool CoverageReady => !disposed && residency.CoverageReady;
        public ContactPlanStatus LastPlanStatus { get; private set; } = ContactPlanStatus.Complete;
        public TorusContactStream(Transform root,TorusContactGrid grid,int segments=8,int capacity=96,int maxRequired=48)
        {
            if(root == null) throw new ArgumentNullException(nameof(root));
            if(segments < 2 || segments > 128 || maxRequired < 1 || maxRequired > capacity) throw new ArgumentOutOfRangeException(nameof(segments));
            this.root=root; this.grid=grid ?? throw new ArgumentNullException(nameof(grid)); this.segments=segments; this.maxRequired=maxRequired;
            residency=new TorusContactResidency(grid,capacity);
            CheckAlive();
        }
        public bool Request(IReadOnlyList<ContactSweep> sweeps,int evaluationBudget)
        {
            CheckAlive(); var plan=grid.Plan(sweeps,maxRequired,evaluationBudget); LastPlanStatus=plan.Status;
            bool accepted=residency.SetTarget(plan);
            if(!accepted && plan.Status == ContactPlanStatus.Complete) LastPlanStatus=ContactPlanStatus.CapacityExceeded;
            SynchronizeActivation(); return accepted;
        }
        /// <summary>Bounds build count, not wall-clock time: MeshCollider cooking is synchronous.</summary>
        public int Pump(int maxBuilds)
        {
            CheckAlive(); if(maxBuilds < 0 || maxBuilds > 32) throw new ArgumentOutOfRangeException(nameof(maxBuilds));
            if(!residency.LastRequestAccepted) return 0;
            if(buildFrame != Time.frameCount) { buildFrame=Time.frameCount; attemptsThisFrame=0; }
            int built=0, attempts=0;
            while(attempts < maxBuilds && attemptsThisFrame < 32 && residency.TryBeginBuild(out var ticket))
            {
                attempts++; attemptsThisFrame++;
                Entry entry=null;
                try
                {
                    entry=Build(ticket.Patch);
                    if(!residency.Commit(ticket,out var evicted)) { Release(entry); residency.Abort(ticket); continue; }
                    if(evicted.HasValue) { Release(entries[evicted.Value]); entries.Remove(evicted.Value); Evictions++; }
                    entries.Add(ticket.Patch,entry); entry=null; Builds++; built++;
                }
                catch { if(entry != null) Release(entry); residency.Abort(ticket); throw; }
            }
            SynchronizeActivation(); return built;
        }
        public bool Raycast(Vector3 worldOrigin,Vector3 worldDirection,float maxDistance,out RaycastHit hit)
        {
            CheckAlive();
            hit=default(RaycastHit); float closest=float.PositiveInfinity;
            foreach(var entry in entries.Values)
                if(entry.Object.activeInHierarchy && entry.Collider.Raycast(new Ray(worldOrigin,worldDirection),out var candidate,maxDistance) && candidate.distance < closest)
                { closest=candidate.distance; hit=candidate; }
            return !float.IsPositiveInfinity(closest);
        }
        private Entry Build(TorusPatchId id)
        {
            SurfaceMeshPatch patch=TorusMeshBaker.Build(grid.MajorRadius,grid.MinorRadius,id.U,id.V,grid.TilesU,grid.TilesV,segments);
            var entry=new Entry { Object=new GameObject("Jakobian contact "+id) };
            try
            {
                entry.Object.SetActive(false); entry.Object.transform.SetParent(root,false);
                entry.Object.transform.localPosition=(Vector3)BodyQueries.Ksp(patch.Anchor);
                var positions=new Vector3[patch.Vertices.Length]; var normals=new Vector3[positions.Length];
                for(int i=0;i<positions.Length;i++) { positions[i]=(Vector3)BodyQueries.Ksp(patch.Vertices[i]); normals[i]=(Vector3)BodyQueries.Ksp(patch.Normals[i]); }
                entry.Mesh=new Mesh { name=entry.Object.name }; entry.Mesh.vertices=positions; entry.Mesh.normals=normals; entry.Mesh.triangles=patch.Triangles; entry.Mesh.RecalculateBounds();
                entry.Collider=entry.Object.AddComponent<MeshCollider>(); entry.Collider.sharedMesh=entry.Mesh;
                return entry;
            }
            catch { Release(entry); throw; }
        }
        private void SynchronizeActivation()
        {
            foreach(var pair in entries) pair.Value.Object.SetActive(residency.IsActive(pair.Key));
            Physics.SyncTransforms();
        }
        private static void Release(Entry entry)
        {
            if(entry.Object != null) { entry.Object.SetActive(false); UnityEngine.Object.Destroy(entry.Object); }
            if(entry.Mesh != null) UnityEngine.Object.Destroy(entry.Mesh);
        }
        private void CheckAlive()
        {
            if(disposed || root == null) throw new ObjectDisposedException(nameof(TorusContactStream));
            if((root.lossyScale-Vector3.one).sqrMagnitude > 1e-8f) throw new InvalidOperationException("Contact frame must have unit world scale.");
        }
        public void Dispose()
        {
            if(disposed) return;
            foreach(var entry in entries.Values) Release(entry);
            entries.Clear(); disposed=true;
        }
    }
}
