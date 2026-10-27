using System.Collections.Generic;
using DeadlyJenga.Core;
using DeadlyJenga.CandyAudio;
using UnityEngine;
namespace DeadlyJenga.Presentation
{
    public sealed class CandyExpressionBubbles:MonoBehaviour
    {
        sealed class Bubble{public TextMesh text;public string actor;public Vector3 point;public float until,nextVisibility;}
        readonly List<Bubble> bubbles=new List<Bubble>();int sequence;string epoch;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Listen()=>GameEvents.SessionStarted+=go=>go.AddComponent<CandyExpressionBubbles>();
        void Update()
        {
            var runtime=MatchRuntime.Current;var camera=Services.Session?.ActiveCamera;if(runtime==null||!camera)return;var frame=runtime.Frame;
            if(epoch!=frame.enemyActionEpoch){epoch=frame.enemyActionEpoch;sequence=frame.expressionSequence;}
            foreach(var expression in frame.expressions)
            {
                if(expression.id<=sequence)continue;sequence=expression.id;
                if((expression.position-camera.transform.position).sqrMagnitude>400)continue;
                if(bubbles.Count>=16){Destroy(bubbles[0].text.gameObject);bubbles.RemoveAt(0);}
                var text=CandyNameplate.Create(transform,"Expression");text.characterSize=.017f;text.color=new Color(1,.93f,.65f);text.text=System.Text.RegularExpressions.Regex.Replace(expression.name,"([a-z])([A-Z])","$1 $2");CandyNameplate.RefreshShadow(text);bubbles.Add(new Bubble{text=text,actor=expression.actor,point=expression.position,until=Time.time+expression.remaining});
                if(!runtime.Authority)CandySignatureSounds.PlayEmote(runtime.FindActor(expression.actor)?.gameObject,expression.name);
            }
            for(int i=bubbles.Count-1;i>=0;i--)
            {
                var b=bubbles[i];if(Time.time>b.until){Destroy(b.text.gameObject);bubbles.RemoveAt(i);continue;}var actor=runtime.FindActor(b.actor);var point=(actor!=null?actor.ViewPose.position:b.point)+Vector3.up*.52f;b.text.transform.position=point;b.text.transform.rotation=Quaternion.LookRotation(point-camera.transform.position);if(Time.time>=b.nextVisibility){b.nextVisibility=Time.time+.2f;b.text.gameObject.SetActive((point-camera.transform.position).sqrMagnitude<400&&!Physics.Linecast(camera.transform.position,point,(1<<Layers.Block)|(1<<Layers.Surface)|1,QueryTriggerInteraction.Ignore));}
            }
        }
    }
}
