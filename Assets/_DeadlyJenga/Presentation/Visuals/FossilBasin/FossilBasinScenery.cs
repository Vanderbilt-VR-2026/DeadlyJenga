using DeadlyJenga.CandyArt;
using UnityEngine;

namespace DeadlyJenga.Presentation
{
    public sealed class FossilBasinScenery : MonoBehaviour
    {
        public Material sky,tar;
        public Mesh swimmerModel;
        public Material swimmerSkin;

        GameObject animal;
        void OnEnable() { if (animal) animal.SetActive(true); }

        void Awake()
        {
            if (!swimmerModel || !swimmerSkin) return;
            animal = new GameObject("Circling Spinosaurus");
            animal.transform.SetParent(transform, false);
            animal.AddComponent<FossilBasinSwimmer>().Initialize(swimmerModel, swimmerSkin);
        }

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
            if (animal) animal.SetActive(false);
            if(!environment)return;
            environment.sandRenderer.forceRenderingOff=sandOff;
            if(seaLife)seaLife.gameObject.SetActive(wildlifeActive);
            seaLife=null;environment=null;
        }
    }
}
