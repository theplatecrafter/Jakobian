using System;
using System.Linq;
using Jakobian.Core;

internal static partial class Program
{
    private static void StreamingChecks()
    {
        var chart=new TorusChart(1000,200); var random=new Random(20261015);
        for(int i=0;i<2000;i++)
        {
            double u=random.NextDouble(),v=random.NextDouble(),offset=random.NextDouble()*100-50;
            var anchor=new TorusAnchor(u,v,offset);
            Assert(chart.TryResolve(anchor,out var frame),"torus material anchor resolves");
            double radial=Math.Sqrt(frame.Position.X*frame.Position.X+frame.Position.Z*frame.Position.Z);
            Near(Math.Sqrt((radial-1000)*(radial-1000)+frame.Position.Y*frame.Position.Y)-200,offset,1e-10,"anchor normal offset independent oracle");
            VectorNear(Vector3d.Cross(frame.VDirection,frame.UDirection),frame.Normal,1e-12,"material frame orientation matches mesh winding");
            Near(Vector3d.Dot(frame.Normal,frame.UDirection),0,1e-12,"material U tangent");
            Assert(chart.TryProject(frame.Position,out var recovered),"material anchor inverse");
            Assert(chart.TryResolve(recovered,out var restored),"material inverse resolves");
            VectorNear(restored.Position,frame.Position,1e-9,"material anchor inverse preserves position");
            Assert(chart.TryResolve(new TorusAnchor(u+1,v-2,offset),out var wrapped),"periodic material wrap");
            VectorNear(wrapped.Position,frame.Position,1e-9,"material seam wrap position");
        }
        Assert(!chart.TryProject(new Vector3d(0,100,0),out _),"axis anchor ambiguity explicit");
        Assert(!chart.TryProject(new Vector3d(1000,0,0),out _),"tube medial anchor ambiguity explicit");
        Assert(!chart.TryResolve(new TorusAnchor(0,0,-200),out _),"folded normal-offset anchor rejected");
        chart.TryResolve(new TorusAnchor(0,.5,0),out var inner);
        VectorNear(inner.Normal,new Vector3d(-1,0,0),1e-12,"inner anchor normal points into hole");
        var grid=new TorusContactGrid(1000,200,16,8);
        for(int level=0;level<3;level++)
        {
            int tilesU=8<<level,tilesV=4<<level;
            var patch=TorusMeshBaker.Build(1000,200,tilesU-1,tilesV-1,tilesU,tilesV,8);
            for(int a=0;a<=8;a++) for(int b=0;b<=8;b++)
            {
                chart.TryResolve(new TorusAnchor((tilesU-1+a/8.0)/tilesU,(tilesV-1+b/8.0)/tilesV,0),out var pose);
                VectorNear(patch.Anchor+patch.Vertices[a*9+b],pose.Position,1e-9,"material coordinates remain independent of mesh tessellation and seam wrapping");
            }
        }
        for(int u=0;u<grid.TilesU;u++) for(int v=0;v<grid.TilesV;v++)
        {
            var id=new TorusPatchId(u,v);
            for(int a=0;a<=8;a++) for(int b=0;b<=8;b++)
            {
                chart.TryResolve(new TorusAnchor((u+a/8.0)/grid.TilesU,(v+b/8.0)/grid.TilesV,0),out var sample);
                Assert((sample.Position-grid.Centre(id)).Length <= grid.PatchBound+1e-10,"analytic patch bound contains unsampled boundary and interior");
            }
        }
        for(int i=0;i<300;i++)
        {
            double u=random.NextDouble(),v=random.NextDouble(); chart.TryResolve(new TorusAnchor(u,v,0),out var surface);
            var plan=grid.Plan(new[]{new ContactSweep(surface.Position,surface.Position,1)},128,128);
            Assert(plan.Status==ContactPlanStatus.Complete,"surface footprint plan completes");
            Assert(plan.Required.Contains(new TorusPatchId((int)(u*16),(int)(v*8))),"surface's material patch cannot be omitted");
        }
        var tunnel=grid.Plan(new[]{new ContactSweep(new Vector3d(0,-2000,0),new Vector3d(0,2000,0),5)},128,128);
        Assert(tunnel.Status==ContactPlanStatus.Complete && tunnel.Required.Count==0,"axial hole sweep needs no terrain, not a sphere cap");
        var diameter=grid.Plan(new[]{new ContactSweep(new Vector3d(-1500,0,0),new Vector3d(1500,0,0),5)},128,128);
        Assert(diameter.Status==ContactPlanStatus.Complete && diameter.Required.Contains(new TorusPatchId(0,0)) && diameter.Required.Contains(new TorusPatchId(0,4))
            && diameter.Required.Contains(new TorusPatchId(8,0)) && diameter.Required.Contains(new TorusPatchId(8,4)),"sweep catches all four boundaries despite empty endpoints");
        var lowBudget=grid.Plan(new[]{new ContactSweep(inner.Position,inner.Position,1)},128,1);
        Assert(lowBudget.Status==ContactPlanStatus.BudgetExceeded && lowBudget.Required.Count==0,"partial broad-phase output never certified complete");
        var huge=grid.Plan(new[]{new ContactSweep(new Vector3d(),new Vector3d(),2000)},2,128);
        Assert(huge.Status==ContactPlanStatus.CapacityExceeded && huge.Required.Count==0,"oversized footprint fails without truncating coverage");
        Assert(grid.Plan(new[]{new ContactSweep(new Vector3d(double.NaN,0,0),new Vector3d(),1)},128,128).Status==ContactPlanStatus.InvalidInput,"invalid sweep rejected");
        // Obtain small real plans on a finer grid instead of forging successful coverage.
        var fine=new TorusContactGrid(1000,200,64,32);
        var resident=new TorusContactResidency(fine,96);
        TorusContactPlan At(double u,double v) { chart.TryResolve(new TorusAnchor(u,v,0),out var f); return fine.Plan(new[]{new ContactSweep(f.Position,f.Position,5)},48,2048); }
        var first=At(.1,.1); Assert(first.Status==ContactPlanStatus.Complete,"first target within budget");
        var tooSmall=new TorusContactResidency(fine,1);
        Assert(!tooSmall.SetTarget(first) && !tooSmall.CoverageReady && tooSmall.ResidentCount==0,"cache capacity rejection does not mutate resident state");
        Assert(resident.SetTarget(first) && !resident.CoverageReady,"coverage waits for complete native publication");
        var otherOwner=new TorusContactResidency(fine,96); Assert(otherOwner.SetTarget(first),"second world target");
        Assert(otherOwner.TryBeginBuild(out var foreign),"foreign ticket");
        Assert(resident.TryBeginBuild(out var own),"own ticket");
        Assert(!resident.Commit(foreign,out _),"another world's ticket cannot publish even with matching revision and serial");
        Assert(resident.Abort(own),"own reservation can be released");
        Drain(resident);
        Assert(resident.CoverageReady && resident.Active.Count==first.Required.Count,"complete resident set activates");
        var old=resident.Active.ToArray(); var second=At(.6,.6); Assert(resident.SetTarget(second),"new target accepted");
        Assert(old.SequenceEqual(resident.Active),"old contact retained during replacement");
        Assert(resident.TryBeginBuild(out var stale),"stale build ticket reserved");
        Assert(resident.SetTarget(At(.7,.7)),"retarget pending generation");
        Assert(!resident.Commit(stale,out _),"stale build cannot publish after retarget");
        Drain(resident); Assert(resident.CoverageReady,"retarget finishes");
        var activeBeforeFailure=resident.Active.ToArray();
        Assert(!resident.SetTarget(lowBudget) && !resident.CoverageReady,"rejected request never reports current coverage ready");
        Assert(activeBeforeFailure.SequenceEqual(resident.Active),"rejected request preserves old active patches");
        for(int i=0;i<500;i++)
        {
            Assert(resident.SetTarget(At((i*.037)%1,(i*.071)%1)),"bounded traversal request");
            while(resident.TryBeginBuild(out var ticket))
            {
                if(i%7==0) { Assert(resident.Abort(ticket),"failed native cook releases reservation"); Assert(resident.TryBeginBuild(out ticket),"failed cook can retry"); }
                Assert(resident.Commit(ticket,out var evicted),"current patch publishes");
                Assert(!resident.Commit(ticket,out _),"duplicate publication rejected");
                if(evicted.HasValue) Assert(!resident.IsActive(evicted.Value),"active terrain is never evicted");
                Assert(resident.ResidentCount<=96 && resident.PendingCount<=48,"residency and work stay bounded during travel");
            }
            Assert(resident.CoverageReady,"each traversed footprint completes");
        }
        Assert(resident.SetTarget(fine.Plan(new ContactSweep[0],48,2048)) && resident.CoverageReady && resident.Active.Count==0,"empty far-space target releases active colliders");
        void Drain(TorusContactResidency state) { int count=0; while(state.TryBeginBuild(out var ticket)) { Assert(++count<=96,"bounded queue drain"); Assert(state.Commit(ticket,out _),"build commit"); } }
    }
}
