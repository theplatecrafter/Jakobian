using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Jakobian.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using CoreVector = Jakobian.Core.Vector3d;

namespace Jakobian.KSP
{
    public sealed class TorusContactWitness : MonoBehaviour
    {
        public int Contacts;
        private void OnCollisionEnter(Collision collision) { if(collision.collider is MeshCollider) Contacts++; }
        private void OnCollisionStay(Collision collision) { if(collision.collider is MeshCollider) Contacts++; }
    }

    [KSPAddon(KSPAddon.Startup.MainMenu,true)]
    public sealed class TorusStreamingProbe : MonoBehaviour
    {
        private TorusContactStream stream;
        private GameObject root;
        private Scene scene;
        private PhysicsScene physics;
        private bool failed;
        private readonly TorusChart chart=new TorusChart(1000,200);
        private readonly AnalyticSolid solid=new AnalyticSolid(1000,200);
        private readonly ConfigNode anchors=new ConfigNode("JakobianProbeAnchors");
        private readonly List<CoreVector> savedPositions=new List<CoreVector>();
        private IEnumerator Start()
        {
            if(!Environment.GetCommandLineArgs().Contains("-jakobianTorusStreamingProbe")) yield break;
            DontDestroyOnLoad(gameObject);
            bool automatic=Physics.autoSimulation; float fixedStep=Time.fixedDeltaTime;
            try
            {
                NativeChecks.Run();
                scene=SceneManager.CreateScene("Jakobian isolated torus contact",new CreateSceneParameters(LocalPhysicsMode.Physics3D));
                physics=scene.GetPhysicsScene();
                if(!physics.IsValid() || physics.Equals(Physics.defaultPhysicsScene)) throw new InvalidOperationException("Isolated physics scene unavailable.");
                root=new GameObject("Jakobian streaming probe"); SceneManager.MoveGameObjectToScene(root,scene);
                root.transform.position=new Vector3(20000,20000,20000); root.transform.rotation=Quaternion.Euler(20,35,10);
                stream=new TorusContactStream(root.transform,new TorusContactGrid(1000,200,64,32));
            }
            catch(Exception e) { Fail(e); }
            if(!failed)
            {
                for(int i=0;i<128;i++)
                {
                    // Two independent participants, including periodic U/V crossings.
                    chart.TryResolve(new TorusAnchor((i*.037)%1,(i*.071)%1,0),out var first);
                    chart.TryResolve(new TorusAnchor((i*.037+.5)%1,(i*.071+.5)%1,0),out var second);
                    yield return Prepare(new[]{new ContactSweep(first.Position,first.Position,20),new ContactSweep(second.Position,second.Position,20)});
                    if(failed) break;
                    try { CheckRay(first); CheckRay(second); if(stream.ResidentCount > 96) throw new InvalidOperationException("Native residency exceeded its budget."); }
                    catch(Exception e) { Fail(e); break; }
                    yield return null; // consume Unity's deferred destruction queue every footprint
                }
            }
            if(!failed)
            {
                Debug.Log("[Jakobian] TORUS_STREAM_TRAVERSAL_PASS requests=128 participants=2 residents="+stream.ResidentCount+" builds="+stream.Builds+" evictions="+stream.Evictions);
                yield return SweepSeam();
            }
            if(!failed)
            {
                foreach(var coordinate in new[]{new Vector2(0,0),new Vector2(0,.5f),new Vector2(.25f,.25f)})
                {
                    yield return Drop(coordinate.x,coordinate.y);
                    if(failed) break;
                }
            }
            if(!failed) yield return HoleTransit();
            if(!failed)
            {
                try
                {
                    chart.TryResolve(new TorusAnchor(0,0,0),out var outer);
                    int active=stream.ActiveCount;
                    bool accepted=stream.Request(new[]{new ContactSweep(new CoreVector(),new CoreVector(),10000)},2048);
                    if(accepted || stream.CoverageReady || stream.LastPlanStatus != ContactPlanStatus.CapacityExceeded || stream.Pump(2) != 0 || active != stream.ActiveCount)
                        throw new InvalidOperationException("Rejected oversized coverage request mutated active terrain or reported ready.");
                    CheckRay(outer);
                    Debug.Log("[Jakobian] TORUS_STREAM_REJECTION_PASS oldCoverage=retained newCoverage=not-ready");
                    string path=Path.Combine(KSPUtil.ApplicationRootPath,".dev","TorusAnchors.cfg"); anchors.Save(path);
                    var restored=ConfigNode.Load(path);
                    int index=0;
                    foreach(var record in restored.GetNodes("JakobianTorusAnchor"))
                    {
                        if(!TorusAnchorCodec.TryLoad(record,"jakobian/probes/torus",chart,out var anchor,out var error) || !chart.TryResolve(anchor,out var frame)
                            || (frame.Position-savedPositions[index++]).Length > 1e-8) throw new InvalidOperationException(error ?? "Anchor roundtrip changed material position.");
                        if(TorusAnchorCodec.TryLoad(record,"other-body",chart,out _,out _) || TorusAnchorCodec.TryLoad(record,"jakobian/probes/torus",new TorusChart(1001,200),out _,out _))
                            throw new InvalidOperationException("Changed identity/geometry accepted an old anchor.");
                        var future=TorusAnchorCodec.Save("jakobian/probes/torus",chart,anchor); future.SetValue("schemaVersion","999",true);
                        if(TorusAnchorCodec.TryLoad(future,"jakobian/probes/torus",chart,out _,out _)) throw new InvalidOperationException("Unknown anchor schema accepted.");
                        var invalid=TorusAnchorCodec.Save("jakobian/probes/torus",chart,anchor); invalid.SetValue("normalOffset","NaN",true);
                        if(TorusAnchorCodec.TryLoad(invalid,"jakobian/probes/torus",chart,out _,out _)) throw new InvalidOperationException("Nonfinite anchor accepted.");
                    }
                    if(restored.GetNodes("JakobianTorusAnchor").Length != 3) throw new InvalidOperationException("Anchor record count mismatch.");
                    if(Physics.autoSimulation != automatic || Time.fixedDeltaTime != fixedStep) throw new InvalidOperationException("Default physics settings changed.");
                    Debug.Log("[Jakobian] TORUS_ANCHOR_ROUNDTRIP_PASS records=3 position=preserved wrongBody=rejected changedShape=rejected futureSchema=rejected nonfinite=rejected");
                }
                catch(Exception e) { Fail(e); }
            }
            if(stream != null) stream.Dispose();
            if(root != null) { root.SetActive(false); Destroy(root); }
            if(scene.IsValid()) { var unload=SceneManager.UnloadSceneAsync(scene); while(unload != null && !unload.isDone) yield return null; }
            yield return null;
            if(!failed && (physics.IsValid() || stream.ResidentCount != 0)) Fail(new InvalidOperationException("Local physics scene/resources survived cleanup."));
            if(!failed) Debug.Log("[Jakobian] TORUS_STREAM_SUITE_PASS scene=isolated defaultPhysics=unchanged");
            Application.Quit();
        }
        private IEnumerator Prepare(ContactSweep[] requests)
        {
            try { if(!stream.Request(requests,4096)) throw new InvalidOperationException("Contact request failed: "+stream.LastPlanStatus); }
            catch(Exception e) { Fail(e); yield break; }
            int frames=0;
            while(!stream.CoverageReady)
            {
                try { if(++frames > 64) throw new InvalidOperationException("Contact publication did not finish."); if(stream.Pump(2) > 2) throw new InvalidOperationException("Build quota exceeded."); }
                catch(Exception e) { Fail(e); yield break; }
                yield return null;
            }
        }
        private void CheckRay(TorusMaterialFrame frame)
        {
            Vector3 origin=root.transform.TransformPoint((Vector3)BodyQueries.Ksp(frame.Position+frame.Normal*20));
            Vector3 direction=root.transform.TransformDirection((Vector3)BodyQueries.Ksp(-frame.Normal));
            if(!stream.Raycast(origin,direction,40,out var hit) || Math.Abs(hit.distance-20) > .3
                || Vector3.Angle(hit.normal,root.transform.TransformDirection((Vector3)BodyQueries.Ksp(frame.Normal))) > 3)
                throw new InvalidOperationException("Streamed patch contact gap/normal mismatch.");
        }
        private IEnumerator Drop(double u,double v)
        {
            chart.TryResolve(new TorusAnchor(u,v,0),out var frame);
            yield return Prepare(new[]{new ContactSweep(frame.Position,frame.Position,40)}); if(failed) yield break;
            GameObject actor=null;
            try
            {
                actor=Actor(frame.Position+frame.Normal*5,out var body,out var witness);
                Vector3 down=root.transform.TransformDirection((Vector3)BodyQueries.Ksp(-frame.Normal)); body.velocity=down*2;
                for(int i=0;i<250;i++) { body.AddForce(down*2,ForceMode.Acceleration); physics.Simulate(.02f); }
                CoreVector local=BodyQueries.Core(root.transform.InverseTransformPoint(actor.transform.position));
                double distance=solid.SignedDistance(local);
                if(witness.Contacts == 0 || Math.Abs(distance-1) > .3 || body.velocity.magnitude > .5f) throw new InvalidOperationException("Native rigidbody did not settle on torus: contacts="+witness.Contacts+" distance="+distance+" speed="+body.velocity.magnitude);
                if(!chart.TryProject(local,out var anchor)) throw new InvalidOperationException("Settled material anchor unavailable.");
                anchors.AddNode(TorusAnchorCodec.Save("jakobian/probes/torus",chart,anchor));
                savedPositions.Add(local);
                Debug.Log("[Jakobian] TORUS_STREAM_RIGIDBODY_PASS u="+u+" v="+v+" contacts="+witness.Contacts+" clearance="+distance+" speed="+body.velocity.magnitude);
            }
            catch(Exception e) { Fail(e); }
            finally { if(actor != null) { actor.SetActive(false); Destroy(actor); } }
            yield return null;
        }
        private IEnumerator HoleTransit()
        {
            chart.TryResolve(new TorusAnchor(0,0,0),out var outer);
            yield return Prepare(new[]{new ContactSweep(new CoreVector(0,-250,0),new CoreVector(0,250,0),2),new ContactSweep(outer.Position,outer.Position,20)}); if(failed) yield break;
            GameObject actor=null;
            try
            {
                actor=Actor(new CoreVector(0,-250,0),out var body,out var witness);
                body.velocity=root.transform.TransformDirection(Vector3.up*100);
                for(int i=0;i<250;i++) physics.Simulate(.02f);
                Vector3 local=root.transform.InverseTransformPoint(actor.transform.position);
                if(witness.Contacts != 0 || (local-new Vector3(0,250,0)).magnitude > 2 || stream.ActiveCount == 0) throw new InvalidOperationException("Hole transit was blocked or not simulated.");
                Debug.Log("[Jakobian] TORUS_STREAM_HOLE_PASS contacts=0 travelM=500 activeRemoteTerrain="+stream.ActiveCount);
            }
            catch(Exception e) { Fail(e); }
            finally { if(actor != null) { actor.SetActive(false); Destroy(actor); } }
            yield return null;
        }
        private IEnumerator SweepSeam()
        {
            chart.TryResolve(new TorusAnchor(.99,.25,0),out var from); chart.TryResolve(new TorusAnchor(.01,.25,0),out var to);
            yield return Prepare(new[]{new ContactSweep(from.Position,to.Position,25)}); if(failed) yield break;
            try
            {
                for(int i=0;i<=32;i++)
                {
                    CoreVector point=from.Position+(to.Position-from.Position)*(i/32.0);
                    if(!chart.TryProject(point,out var anchor) || !chart.TryResolve(new TorusAnchor(anchor.U,anchor.V,0),out var frame)) throw new InvalidOperationException("Seam sweep chart unavailable.");
                    CheckRay(frame);
                }
                Debug.Log("[Jakobian] TORUS_STREAM_SWEEP_PASS samples=33 seam=U corridorM="+(to.Position-from.Position).Length);
            }
            catch(Exception e) { Fail(e); }
        }
        private GameObject Actor(CoreVector position,out Rigidbody body,out TorusContactWitness witness)
        {
            var actor=new GameObject("Jakobian isolated rigidbody"); actor.transform.SetParent(root.transform,false);
            actor.transform.localPosition=(Vector3)BodyQueries.Ksp(position); actor.AddComponent<SphereCollider>().radius=1;
            body=actor.AddComponent<Rigidbody>(); body.useGravity=false; body.mass=2; body.constraints=RigidbodyConstraints.FreezeRotation;
            body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic; witness=actor.AddComponent<TorusContactWitness>(); return actor;
        }
        private void Fail(Exception e) { failed=true; Debug.LogError("[Jakobian] TORUS_STREAM_SUITE_FAIL "+e); }
    }
}
