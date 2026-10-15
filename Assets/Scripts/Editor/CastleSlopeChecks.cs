using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace DeadlyJenga.CastlePrototype.Editor
{
    // Opt-in, isolated runtime fixtures; never saves or changes the authored scene.
    [InitializeOnLoad]
    public static class CastleSlopeChecks
    {
        const string Request="ArtDirection/SlopeChecks.request",Key="CastleSlopeChecks";
        static CastleSlopeChecks()
        {
            EditorApplication.update+=()=>
            {
                string request=File.Exists("ArtDirection/SeamFinalCheck.request")?"ArtDirection/SeamFinalCheck.request":Request;
                if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorUtility.scriptCompilationFailed||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(request))return;
                string command=File.ReadAllText(request).Trim();SessionState.SetBool(Key+"Climb",command=="climb");SessionState.SetBool(Key+"Seams",command=="seams");SessionState.SetBool(Key+"Stamina",command=="stamina");File.Move(request,request+".consumed-"+DateTime.UtcNow.Ticks);
                if(command=="stamina")CastleStaminaChecks.InstallAudio();
                SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
            };
            EditorApplication.playModeStateChanged+=s=>
            {
                if(s!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Key,false))return;
                SessionState.SetBool(Key,false);EditorApplication.delayCall+=()=>Object.FindFirstObjectByType<CastlePlaySession>().StartCoroutine(SessionState.GetBool(Key+"Stamina",false)?CastleStaminaChecks.Check():SessionState.GetBool(Key+"Seams",false)?CastleSeamCameraChecks.Check():SessionState.GetBool(Key+"Climb",false)?CastleClimbChecks.Check():Check());
            };
        }
        static IEnumerator Check()
        {
            yield return null;
            var lines=new List<string>();var session=Object.FindFirstObjectByType<CastlePlaySession>();session.SetMenu(true);
            var staging=new GameObject("Slope check staging");staging.SetActive(false);
            var motor=Object.Instantiate(session.survivorPrefab,staging.transform).GetComponent<CastleSurvivor>();
            motor.externallyDriven=true;motor.enabled=false;
            foreach(var camera in motor.GetComponentsInChildren<Camera>(true))camera.enabled=false;
            foreach(var listener in motor.GetComponentsInChildren<AudioListener>(true))listener.enabled=false;
            staging.SetActive(true);motor.RefreshMode();
            var ramp=GameObject.CreatePrimitive(PrimitiveType.Cube);ramp.name="Isolated slope fixture";ramp.transform.localScale=new Vector3(30,.5f,20);
            void Assert(bool ok,string message){lines.Add((ok?"PASS ":"FAIL ")+message);File.WriteAllLines("ArtDirection/SlopeChecks.txt",lines);}
            void Settle(float angle)
            {
                ramp.transform.SetPositionAndRotation(new Vector3(120,40,0),Quaternion.Euler(0,0,angle));Physics.SyncTransforms();
                motor.Teleport(ramp.transform.position+ramp.transform.up*.25f+Vector3.up*.5f,Quaternion.LookRotation(Vector3.right));
                for(int i=0;i<100;i++)motor.Simulate(Vector2.zero,false,1f/60);
            }
            Settle(0);Assert(motor.Grounded,"Flat floor supports the survivor.");
            motor.Simulate(Vector2.zero,true,1f/60);float first=motor.VerticalSpeed;motor.Simulate(Vector2.zero,true,1f/60);
            Assert(first>0&&motor.VerticalSpeed<first,"Ground jump works; a second airborne press does not double jump.");
            Settle(30);var start=motor.transform.position;
            for(int i=0;i<30;i++)motor.Simulate(Vector2.up,false,1f/60);
            Assert(motor.Grounded&&motor.transform.position.y>start.y+.5f,"30 degree ramp permits continuous uphill walking.");
            Assert(!motor.Sliding,"30 degree ramp retains grip.");
            motor.Simulate(Vector2.zero,true,1f/60);Assert(motor.VerticalSpeed>0&&!motor.Grounded,"Jump launches from an incline.");
            Settle(55);start=motor.transform.position;
            for(int i=0;i<30;i++)motor.Simulate(Vector2.zero,false,1f/60);
            float medium=start.x-motor.transform.position.x;
            Assert(motor.Grounded&&motor.Sliding&&medium>.05f,"55 degree slope slides downhill with foot support.");
            motor.Simulate(Vector2.zero,true,1f/60);Assert(motor.VerticalSpeed>0,"Sliding survivor can jump away.");
            Settle(70);start=motor.transform.position;
            for(int i=0;i<30;i++)motor.Simulate(Vector2.zero,false,1f/60);
            Assert(start.x-motor.transform.position.x>medium,"70 degree slope slips faster than 55 degrees.");
            Settle(84);motor.Simulate(Vector2.zero,true,1f/60);
            Assert(!motor.Grounded&&motor.VerticalSpeed<=0,"Near-vertical face supplies no grounded jump.");
            Assert(Physics.GetIgnoreLayerCollision(CastleBlock.SurvivorLayer,CastleBlock.BlockLayer),"Survivors still cannot push dynamic blocks.");
            Object.Destroy(staging);Object.Destroy(ramp);Debug.Log(string.Join("\n",lines));EditorApplication.ExitPlaymode();
        }
    }
}
