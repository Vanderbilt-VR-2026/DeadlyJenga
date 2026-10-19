using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public sealed class RemoteHealthLedger
    {
        readonly Dictionary<string,float> health=new Dictionary<string,float>();
        readonly HashSet<string> eliminated=new HashSet<string>();
        readonly Dictionary<string,float> quietSeconds=new Dictionary<string,float>();
        public float Read(string id)=>health.TryGetValue(id,out float value)?value:1;
        public void Reset(string id){health[id]=1;eliminated.Remove(id);quietSeconds.Remove(id);}
        public void Seed(string id,float normalizedHealth,bool alreadyEliminated=false)
        {
            if(string.IsNullOrEmpty(id)||float.IsNaN(normalizedHealth)||float.IsInfinity(normalizedHealth))return;
            health[id]=Mathf.Clamp01(normalizedHealth);
            quietSeconds[id]=0;
            if(alreadyEliminated)eliminated.Add(id);else eliminated.Remove(id);
        }
        public bool Damage(string id,float amount,bool canDie)
        {
            if(string.IsNullOrEmpty(id)||float.IsNaN(amount)||float.IsInfinity(amount)||amount<=0)return false;
            float before=Read(id),after=Mathf.Max(0,before-Mathf.Min(10000,amount)/100);
            health[id]=after;quietSeconds[id]=0;return canDie&&after<=0&&eliminated.Add(id);
        }
        public void Recover(string id,float seconds,float delay=10,float normalizedRate=.05f)
        {
            if(seconds<=0||eliminated.Contains(id)||Read(id)>=1)return;
            float before=quietSeconds.TryGetValue(id,out var elapsed)?elapsed:0;
            quietSeconds[id]=before+seconds;
            float recovering=Mathf.Max(0,before+seconds-delay)-Mathf.Max(0,before-delay);
            health[id]=Mathf.Min(1,Read(id)+recovering*Mathf.Max(0,normalizedRate));
        }
    }
}
