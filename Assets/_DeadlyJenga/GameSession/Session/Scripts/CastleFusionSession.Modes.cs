using System;
using System.Text;
using Fusion;
using UnityEngine;
using DeadlyJenga.Modes;
namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleFusionSession
    {
        const int MatchState=12, ModeCommand=13, ModeOutcome=14, MysterySecret=15, MysteryTrails=16;
        MatchModeRuntime modeRuntime;
        byte[] matchPacket;
        float nextMatch;
        [Serializable] sealed class OutcomePacket
        { public EliminationOutcome outcome; public Vector3 position; public Quaternion rotation; public string role; }
        void ConnectModes()
        {
            if(modeRuntime||!session||!session.Modes)return;
            modeRuntime=session.Modes;
            modeRuntime.CommandRequested+=SendModeCommand;
            modeRuntime.RemoteImpact+=QueueModeImpact;
            modeRuntime.RemoteEliminated+=SendModeOutcome;
            if(!IsHost)SendModeCommand("mystery:preference:"+DeadlyJenga.Core.MysteryChoices.RequestedRole);
        }
        void QueueModeImpact(string actorId,Vector3 push,float damage)
        { QueueDisasterImpact(actorId,push,damage,false); }
        void SendModeCommand(string command)
        { if(!IsHost&&welcomed&&command!=null&&command.Length<=8192)Send(default,ModeCommand,Encoding.UTF8.GetBytes(command)); }
        void SendModeOutcome(string id,EliminationOutcome outcome)
        {
            if(!IsHost||!modeRuntime)return;
            var spawn=modeRuntime.OutcomeSpawn(id);
            foreach(var pair in peers)if(pair.Value.ready&&pair.Key.ToString()==id)
            {Send(pair.Key,ModeOutcome,Encoding.UTF8.GetBytes(JsonUtility.ToJson(new OutcomePacket{outcome=outcome,position=spawn.position,rotation=spawn.rotation})));break;}
        }
        void CaptureMatch()
        {
            ConnectModes();
            if(!IsHost||!modeRuntime||Time.unscaledTime<nextMatch)return;
            nextMatch=Time.unscaledTime+.2f;
            matchPacket=CastleFusionWire.Compress(Encoding.UTF8.GetBytes(modeRuntime.CaptureFrameJson()));
            if(modeRuntime.Active)foreach(var pair in peers)if(pair.Value.ready)
            {
                Send(pair.Key,MysterySecret,Encoding.UTF8.GetBytes(modeRuntime.CaptureSecret(pair.Key.ToString())));
                var peer=pair.Value;if(peer.mysteryRound!=modeRuntime.TrailRound){peer.mysteryRound=modeRuntime.TrailRound;peer.trailCount=0;}
                for(int i=0;i<2&&(peer.trailCount<modeRuntime.Trails.Count||i==0&&peer.trailCount==0);i++)
                {int count=Mathf.Min(64,modeRuntime.Trails.Count-peer.trailCount);Send(pair.Key,MysteryTrails,Encoding.UTF8.GetBytes(modeRuntime.CaptureTrails(peer.trailCount)));peer.trailCount+=count;}
            }
        }
        void ReceiveMatch(ReadOnlySpan<byte> bytes)
        {ConnectModes();if(modeRuntime)modeRuntime.ReceiveFrameJson(Encoding.UTF8.GetString(CastleFusionWire.Decompress(bytes.ToArray())));}
        void ReceiveOutcome(ReadOnlySpan<byte> bytes)
        {
            ConnectModes();if(!modeRuntime)return;
            var p=JsonUtility.FromJson<OutcomePacket>(Encoding.UTF8.GetString(bytes.ToArray()));
            if(p==null||!Enum.IsDefined(typeof(EliminationOutcome),p.outcome)||!float.IsFinite(p.position.sqrMagnitude))return;
            modeRuntime.ApplyRemoteOutcome(p.outcome,p.position,p.rotation,p.role);
        }
        void DisconnectModes()
        {
            if(!modeRuntime)return;
            modeRuntime.CommandRequested-=SendModeCommand;
            modeRuntime.RemoteImpact-=QueueModeImpact;
            modeRuntime.RemoteEliminated-=SendModeOutcome;
            modeRuntime=null;
        }
    }
}
