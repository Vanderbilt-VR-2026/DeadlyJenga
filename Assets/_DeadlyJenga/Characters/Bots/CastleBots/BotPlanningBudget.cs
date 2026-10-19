using UnityEngine;

namespace DeadlyJenga.CastleBots
{
    public static class BotPlanningBudget
    {
        public static int MaximumEdgesPerFrame = 20;
        public static int MaximumSensingPerFrame = 8;
        public static int EdgesUsed { get; private set; }
        public static int SensingUsed { get; private set; }
        static int frame=-1;
        static void BeginFrame()
        {
            if(frame==Time.frameCount)return;frame=Time.frameCount;EdgesUsed=SensingUsed=0;
        }
        public static bool TryEdge(int owner=0,int population=1)
        {
            BeginFrame();
            int group=Mathf.Max(1,MaximumEdgesPerFrame/2),count=Mathf.Max(1,population);
            int slot=(Time.frameCount*group)%count;
            if((owner-slot+count*1000)%count>=group||EdgesUsed>=MaximumEdgesPerFrame)return false;
            EdgesUsed++;return true;
        }
        public static bool TrySense()=>TrySense(0,1);
        public static bool TrySense(int owner,int population)
        {
            BeginFrame();int count=Mathf.Max(1,population),group=Mathf.Max(1,MaximumSensingPerFrame);
            int slot=(Time.frameCount*group)%count;
            if((owner-slot+count*1000)%count>=group||SensingUsed>=MaximumSensingPerFrame)return false;
            SensingUsed++;return true;
        }
    }
}
