using System;
using System.Collections;
using System.IO;
using System.Reflection;
using DeadlyJenga.Core;
using DeadlyJenga.Presentation;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DeadlyJenga.DevTools.Editor
{
    // Explicit Presentation checks only; real transition capture is a separate QA journey.
    public static class TravelBeaconLifecycleChecks
    {
        public static IEnumerator RunPresentationChecks()
        {
            string report=ProjectPath.Report("TravelBeaconLifecycleChecks.txt");
            File.WriteAllText(report,"Presentation cue/column checks; synthetic cue, not scene-transition or headset proof.\n");
            void Require(bool value,string message)
            {
                File.AppendAllText(report,(value?"PASS ":"FAIL ")+message+"\n");
                if(!value)throw new InvalidOperationException(message);
            }
            Require(Application.isPlaying,"Explicit existing Play Mode required");
            var session=Services.Session;var player=session?.Player;
            Require(NetworkActors.Alive(player)&&player.gameObject.activeInHierarchy,"Actual active local player required");
            Require(!Object.FindFirstObjectByType<CandySessionTravel>(),"Do not interrupt a current travel effect");
            var shader=Resources.Load<Shader>("CandyScenery/CandySummoningColumn");
            Require(shader,"Existing beacon shader available");
            GameObject target=null,columnObject=null,foreign=null;Material material=null;Mesh mesh=null;CandySessionTravel owned=null;
            try
            {
                target=new GameObject("Owned beacon visibility target");columnObject=new GameObject("Owned beacon visibility check");
                mesh=CandySummoningColumn.CreateMesh(2);material=new Material(shader);
                var column=columnObject.AddComponent<CandySummoningColumn>();column.Initialize(target.transform,mesh,material,0,2,true);
                column.Render(.3f,1,1);Require(column.Visible,"Column visible for active target");
                target.SetActive(false);column.Render(.4f,1,1);Require(!column.Visible,"Inactive target temporarily hides column");
                target.SetActive(true);column.Render(.5f,1,1);Require(column.Visible,"Target reactivation restores column visibility");
                column.SetVisible(false);column.Render(.6f,1,1);Require(!column.Visible,"Explicit transition suppression remains hidden");
                column.SetVisible(true);column.Render(.7f,1,1);Require(column.Visible,"Explicit visibility may be restored");
                foreign=new GameObject("Owned unrelated arrival cue");GameEvents.RaiseEmoted(foreign,"PlayerArrival");
                Require(!Object.FindFirstObjectByType<CandySessionTravel>(),"Unrelated actor cue does not trigger local travel");
                var position=player.transform.position;bool input=player.InputSuspended;
                const BindingFlags flags=BindingFlags.Instance|BindingFlags.Static|BindingFlags.NonPublic;
                typeof(CandySessionTravel).GetMethod("Begin",flags).Invoke(null,new object[]{10f});
                owned=Object.FindFirstObjectByType<CandySessionTravel>();Require(owned&&!owned.Arriving,"Synthetic Presentation departure begins without gameplay mutation");
                int departureMesh=owned.GetComponentInChildren<CandySummoningColumn>().GetComponent<MeshFilter>().sharedMesh.GetInstanceID();
                GameEvents.RaiseEmoted(player.gameObject,"PlayerArrival");
                Require(!owned.Arriving&&owned.GetComponentInChildren<CandySummoningColumn>().GetComponent<MeshFilter>().sharedMesh.GetInstanceID()==departureMesh,"Visual-only cue cannot replace an active departure");
                GameEvents.RaiseEmoted(player.gameObject,"SpawnArrival");
                Require(owned.Arriving,"Physical spawn cue supersedes departure as before");
                int physicalMesh=owned.GetComponentInChildren<CandySummoningColumn>().GetComponent<MeshFilter>().sharedMesh.GetInstanceID();
                GameEvents.RaiseEmoted(player.gameObject,"PlayerArrival");
                Require(owned.GetComponentInChildren<CandySummoningColumn>().GetComponent<MeshFilter>().sharedMesh.GetInstanceID()==physicalMesh,"Visual-only cue cannot replace a physical arrival");
                GameEvents.RaiseEmoted(player.gameObject,"SpawnArrivalSettled");
                GameEvents.RaiseEmoted(player.gameObject,"PlayerArrival");
                Require(owned.GetComponentInChildren<CandySummoningColumn>().GetComponent<MeshFilter>().sharedMesh.GetInstanceID()==physicalMesh,"Lobby visual cue waits for the existing physical curtain cleanup");
                float cleanupDeadline=Time.realtimeSinceStartup+5;
                var previous=owned;
                while(previous&&Time.realtimeSinceStartup<cleanupDeadline)yield return null;
                Require(!previous,"Physical arrival cleanup completed");
                owned=Object.FindFirstObjectByType<CandySessionTravel>();
                Require(owned&&owned.Arriving&&owned.FadeAmount==0,"Queued lobby visual starts after full-travel cleanup");
                typeof(CandySessionTravel).GetField("started",flags).SetValue(owned,Time.unscaledTime-2);
                typeof(CandySessionTravel).GetMethod("LateUpdate",flags).Invoke(owned,null);
                Require((bool)typeof(CandySessionTravel).GetField("finishing",flags).GetValue(owned),"Visual-only expiry uses unscaled wall time even without intermediate ticks");
                int finishingMesh=owned.GetComponentInChildren<CandySummoningColumn>().GetComponent<MeshFilter>().sharedMesh.GetInstanceID();
                GameEvents.RaiseEmoted(player.gameObject,"PlayerArrival");
                Require((bool)typeof(CandySessionTravel).GetField("finishing",flags).GetValue(owned)&&owned.GetComponentInChildren<CandySummoningColumn>().GetComponent<MeshFilter>().sharedMesh.GetInstanceID()==finishingMesh,"Duplicate during finishing cannot restart the visual");
                cleanupDeadline=Time.realtimeSinceStartup+5;
                while(owned&&Time.realtimeSinceStartup<cleanupDeadline)yield return null;
                Require(!owned,"Synthetic overlap checks clean up before timed visual check");
                position=player.transform.position;input=player.InputSuspended;
                GameEvents.RaiseEmoted(player.gameObject,"PlayerArrival");
                owned=Object.FindFirstObjectByType<CandySessionTravel>();Require(owned&&owned.Arriving,"Visual-only local arrival cue starts streams");
                var localColumns=owned.GetComponentsInChildren<CandySummoningColumn>();
                Require(localColumns.Length==1&&localColumns[0].Target==player.transform,"Visual-only arrival targets only local player");
                Require(owned.FadeAmount==0,"Visual-only lobby arrival does not whiten the view");
                GameEvents.RaiseEmoted(player.gameObject,"PlayerArrival");
                Require(Object.FindObjectsByType<CandySessionTravel>(FindObjectsSortMode.None).Length==1&&owned.GetComponentsInChildren<CandySummoningColumn>().Length==1,"Duplicate visual cue does not duplicate streams");
                Require(player.InputSuspended==input&&(player.transform.position-position).sqrMagnitude<.000001f,"Cue handler does not move or pause the player");
                float until=Time.realtimeSinceStartup+10;
                while(owned&&Time.realtimeSinceStartup<until)yield return null;
                Require(!owned,"Visual-only arrival settles without a physical-drop settled event");
                Require(!Object.FindFirstObjectByType<CandySessionTravel>(),"Visual-only manager cleaned up");
                typeof(CandySessionTravel).GetMethod("Begin",flags).Invoke(null,new object[]{10f});
                owned=Object.FindFirstObjectByType<CandySessionTravel>();
                typeof(CandySessionTravel).GetMethod("Finish",flags).Invoke(owned,new object[]{false});
                GameEvents.RaiseEmoted(player.gameObject,"PlayerArrival");
                Object.Destroy(owned.gameObject);yield return null;
                Require(!Object.FindFirstObjectByType<CandySessionTravel>(),"Forced teardown cannot replay a queued arrival");
                Require(ReferenceEquals(Services.Session,session),"Presentation does not replace session service");
                File.AppendAllText(report,"DONE. Actual local/exterior arrival/departure and motion still require normal GameSession QA.\n");
            }
            finally
            {
                if(owned)
                {
                    typeof(CandySessionTravel).GetField("queuedVisualArrival",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(owned,null);
                    Object.Destroy(owned.gameObject);
                }
                if(columnObject)Object.Destroy(columnObject);if(target)Object.Destroy(target);if(foreign)Object.Destroy(foreign);
                if(mesh)Object.Destroy(mesh);if(material)Object.Destroy(material);
            }
        }
    }
}
