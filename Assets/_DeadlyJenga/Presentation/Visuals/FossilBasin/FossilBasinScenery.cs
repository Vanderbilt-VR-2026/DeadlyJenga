using DeadlyJenga.CandyArt;
using UnityEngine;

namespace DeadlyJenga.Presentation
{
    public sealed class FossilBasinScenery : MonoBehaviour
    {
        public Material sky,tar;
        CandyEnvironment environment;
        bool sandOff;
        CandySeaLife seaLife;
        bool wildlifeActive;
        void LateUpdate()
        {
            if(environment)
            {
                if(!seaLife)
                {
                    seaLife=environment.GetComponentInChildren<CandySeaLife>(true);
                    if(seaLife){wildlifeActive=seaLife.gameObject.activeSelf;seaLife.gameObject.SetActive(false);}
                }
                return;
            }
            environment=CandyEnvironment.ForMap(this);
            if(!environment)return;
            sandOff=environment.sandRenderer.forceRenderingOff;
            environment.sandRenderer.forceRenderingOff=true;
        }
        void OnDisable()
        {
            if(!environment)return;
            environment.sandRenderer.forceRenderingOff=sandOff;
            if(seaLife)seaLife.gameObject.SetActive(wildlifeActive);
            seaLife=null;environment=null;
        }
    }
}
