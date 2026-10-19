using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public sealed class ModeNames
    {
        public static readonly string[] Nouns = {
            "Challenger", "Adventurer", "Explorer", "Rebel", "Klutz", "Resistor", "Stifler", "Loner", "Seeker", "Discoverer",
            "Thinker", "Jumper", "Gambler", "Trainer", "Flipper", "Climber", "Runner", "Dancer", "Dodger", "Drifter",
            "Dreamer", "Builder", "Breaker", "Surfer", "Skater", "Sprinter", "Wanderer", "Defender", "Raider", "Strider",
            "Leaper", "Tumbler", "Rambler", "Juggler", "Scout", "Voyager", "Daredevil", "Navigator", "Escapist", "Champion",
            "Balancer", "Scrambler", "Bouncer", "Glider", "Trickster", "Racer", "Rover", "Sailor", "Pioneer", "Survivor"
        };
        public static readonly string[] ColorNames = { "Red", "Blue", "Yellow", "Purple", "Orange", "Cyan", "Pink", "Green", "Mint", "Coral", "Gold", "Violet" };
        public static readonly Color[] Colors = {
            new Color(1,.12f,.2f), new Color(.1f,.4f,1), new Color(1,.85f,.05f), new Color(.65f,.2f,1),
            new Color(1,.4f,.05f), new Color(.05f,.85f,1), new Color(1,.15f,.65f), new Color(.15f,.9f,.25f),
            new Color(.25f,1,.7f), new Color(1,.4f,.45f), new Color(1,.65f,.05f), new Color(.4f,.2f,1)
        };
        readonly HashSet<string> used = new HashSet<string>(StringComparer.Ordinal);
        readonly System.Random random;
        public ModeNames(int seed) { random = new System.Random(seed); }
        public void Reserve(string name){if(!string.IsNullOrEmpty(name))used.Add(name);}
        public static string NameFor(Color color,string noun)
        {
            int closest=0;float distance=float.MaxValue;
            for(int i=0;i<Colors.Length;i++){float d=((Vector4)color-(Vector4)Colors[i]).sqrMagnitude;if(d<distance){closest=i;distance=d;}}
            return ColorNames[closest]+noun;
        }
        public static string NounOf(string name)
        {
            foreach(var prefix in ColorNames.OrderByDescending(v=>v.Length))if(name.StartsWith(prefix,StringComparison.Ordinal))return name.Substring(prefix.Length);
            return Nouns[0];
        }
        public void Release(string name){if(name!=null)used.Remove(name);}
        public string Preserve(Color color,string noun)
        {
            string stem=NameFor(color,noun);if(used.Add(stem))return stem;
            for(int suffix=2;;suffix++){string name=stem+suffix;if(used.Add(name))return name;}
        }
        public string Next(Color color)
        {
            int closest = 0; float distance = float.MaxValue;
            for (int i = 0; i < Colors.Length; i++)
            {
                float d = (new Vector3(color.r,color.g,color.b)-new Vector3(Colors[i].r,Colors[i].g,Colors[i].b)).sqrMagnitude;
                if (d < distance) { distance = d; closest = i; }
            }
            int start = random.Next(Nouns.Length);
            for (int i = 0; i < Nouns.Length; i++)
            {
                string name = ColorNames[closest] + Nouns[(start+i)%Nouns.Length];
                if (used.Add(name)) return name;
            }
            for (int suffix = 2; ; suffix++)
            {
                string name = ColorNames[closest]+Nouns[start]+suffix;
                if (used.Add(name)) return name;
            }
        }
    }
}
