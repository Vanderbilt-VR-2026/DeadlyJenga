using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeadlyJenga.Modes
{
    public sealed class RemoteHealthLedger
    {
        readonly Dictionary<string,float> health=new Dictionary<string,float>();
        readonly HashSet<string> eliminated=new HashSet<string>();
        public float Read(string id)=>health.TryGetValue(id,out float value)?value:1;
        public void Reset(string id){health[id]=1;eliminated.Remove(id);}
        public void Seed(string id,float normalizedHealth,bool alreadyEliminated=false)
        {
            if(string.IsNullOrEmpty(id)||float.IsNaN(normalizedHealth)||float.IsInfinity(normalizedHealth))return;
            health[id]=Mathf.Clamp01(normalizedHealth);
            if(alreadyEliminated)eliminated.Add(id);else eliminated.Remove(id);
        }
        public bool Damage(string id,float amount,bool canDie)
        {
            if(string.IsNullOrEmpty(id)||float.IsNaN(amount)||float.IsInfinity(amount)||amount<=0)return false;
            float before=Read(id),after=Mathf.Max(0,before-Mathf.Min(10000,amount)/100);
            health[id]=after;return canDie&&after<=0&&eliminated.Add(id);
        }
    }
}
