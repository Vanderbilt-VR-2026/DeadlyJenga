using System;
using UnityEngine;
using UnityEngine.Audio;

namespace DeadlyJenga.CandyAudio
{
    public enum CandySound
    {
        BlockTap,BlockHit,BlockSmash,BlockScrape,StructureLean,BlockSplash,PlayerSplash,
        RubberSlide,RubberTouch,FootstepCandy,FootstepSand,Jump,LandSoft,LandHard,VaultGrunt,ClimbGrunt,ClimbPlant,ClimbRelease,
        FallWind,BeachWind,HandFlight,Surf,Music,Spawn,Respawn,UiClick,GripWarning,
        FriendlyWave,TotallyMeantThat,Phew,WhatWasThat,GolfClap,JustChilling,NopeNopeNope,TinyVictoryDance,
        BreathExertion,BreathRecovery,BreathExhausted,
        DisasterHorn,DisasterTension,WaveImpact,WaveSpray,PuddleDrain,AcidSizzle,AcidBurn,
        CycloneWind,EarthRumble,FireCrackle,MeteorWhistle,MeteorImpact,PowerDown,PowerUp,
        FingerFlex,HandSwirl,PushImpact,ClaimWin,IslandSink,VampireSizzle,LobbyLaunch,SpawnSlam,
        WeaponRifle,WeaponShotgun,WeaponBlaster,WeaponTransform,WeaponDraw,WeaponHolster,WeaponPickup,
        WeaponSwap,WeaponDrop,WeaponSword,WeaponHammer,WeaponBoxing,WeaponPlasmaImpact,
        JaguarIdle,JaguarChuff,JaguarGrowl,JaguarAlert,JaguarAttack,JaguarHurt,JaguarDeath,JaguarFootfall,JaguarLand,
        PlayerPain,PlayerDeath,
        SuitThruster,SuitIgnition,SuitShutdown
    }
    public enum CandyBus { World,Character,Ambience,Music,UI }
    [Serializable]
    public sealed class CandyAudioCue
    {
        public CandySound sound;
        public bool enabled=true;
        public AudioClip[] variants=Array.Empty<AudioClip>();
        public CandyBus bus;
        [Range(0,1)] public float volume=.6f;
        public Vector2 pitch=new Vector2(.94f,1.06f);
        [Min(0)] public float cooldown=.08f;
        [Range(1,12)] public int maxVoices=4;
        [Range(0,256)] public int priority=128;
        public float minDistance=2,maxDistance=65;
        public bool loop;
        public AudioMixerGroup output;
    }
    [CreateAssetMenu(menuName="Candy/Audio Bank")]
    public sealed class CandyAudioBank : ScriptableObject
    {
        public CandyAudioCue[] cues=Array.Empty<CandyAudioCue>();
        [Range(8,48)] public int oneShotVoices=28;
        [Range(4,24)] public int loopVoices=12;
        [Range(4,40)] public int eventsPerSecond=22;
        [Header("Mix (live swappable)")]
        [Range(0,1)] public float master=.8f,world=.8f,characters=.8f,ambience=.32f,music=.22f,ui=.55f;
        public float Gain(CandyBus bus)=>bus switch {CandyBus.World=>world,CandyBus.Character=>characters,CandyBus.Ambience=>ambience,CandyBus.Music=>music,_=>ui};
    }
    public enum CandySurface { Candy, Sand, Stone, Rubber }
}
