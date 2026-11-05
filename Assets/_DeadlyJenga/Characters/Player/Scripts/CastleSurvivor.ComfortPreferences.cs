using System;
using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleSurvivor
    {
        struct ComfortProfile
        {
            public SurvivorVRPlayStyle playStyle;
            public SurvivorVRMovement movement;
            public SurvivorVRTurn turn;
            public float snap,speed;
        }
        ComfortProfile hybridComfort;
        ComfortProfile CaptureComfort()=>new ComfortProfile
        {playStyle=vrPlayStyle,movement=vrMovement,turn=vrTurn,snap=vrSnapAngle,speed=vrSmoothDegreesPerSecond};
        void RestoreComfort(ComfortProfile profile)
        {
            SetVRPlayStyle(profile.playStyle,false);
            vrMovement=profile.movement;vrTurn=profile.turn;
            vrSnapAngle=profile.snap;vrSmoothDegreesPerSecond=profile.speed;
        }
        void RestoreRememberedComfort()
        {
            var saved=CaptureComfort();
            if(Enum.TryParse(PlayerPrefs.GetString("SurvivorVR.Movement",saved.movement.ToString()),out SurvivorVRMovement movement)&&
                Enum.IsDefined(typeof(SurvivorVRMovement),movement))saved.movement=movement;
            if(Enum.TryParse(PlayerPrefs.GetString("SurvivorVR.Turn",saved.turn.ToString()),out SurvivorVRTurn turn)&&
                Enum.IsDefined(typeof(SurvivorVRTurn),turn))saved.turn=turn;
            float snap=PlayerPrefs.GetFloat("SurvivorVR.SnapAngle",saved.snap);
            float speed=PlayerPrefs.GetFloat("SurvivorVR.SmoothDegrees",saved.speed);
            if(float.IsFinite(snap))saved.snap=Mathf.Clamp(snap,15,90);
            if(float.IsFinite(speed))saved.speed=Mathf.Clamp(speed,15,360);
            vrMovement=saved.movement;vrTurn=saved.turn;
            vrSnapAngle=saved.snap;vrSmoothDegreesPerSecond=saved.speed;
        }
        void RememberComfort(bool playStyle=false,bool movement=false,bool turn=false,bool snap=false,bool speed=false)
        {
            if(externallyDriven)return;
            var saved=CaptureComfort();
            if(hybridProfileActive)
            {
                saved=hybridComfort;
                if(playStyle)saved.playStyle=vrPlayStyle;
                if(movement)saved.movement=vrMovement;
                if(turn)saved.turn=vrTurn;
                if(snap)saved.snap=vrSnapAngle;
                if(speed)saved.speed=vrSmoothDegreesPerSecond;
                hybridComfort=saved;
            }
            PlayerPrefs.SetInt("SurvivorVRPlayStyle",(int)saved.playStyle);
            PlayerPrefs.SetString("SurvivorVR.Movement",saved.movement.ToString());
            PlayerPrefs.SetString("SurvivorVR.Turn",saved.turn.ToString());
            PlayerPrefs.SetFloat("SurvivorVR.SnapAngle",saved.snap);
            PlayerPrefs.SetFloat("SurvivorVR.SmoothDegrees",saved.speed);
        }
    }
}
