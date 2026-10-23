using DeadlyJenga.Core;
using DeadlyJenga.Presentation;
using UnityEngine;
namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleWorldMenu
    {
        bool tabletopPage;
        void PointTabletop(Camera camera,bool pressed)
        {
            heading.text="SEA SPIRIT / TABLETOP";
            HideDisasterSliders();transform.position=camera.transform.position+openingForward*1.6f;
            var spirit=Services.Session as ISeaSpiritControls;var board=CandyTabletop.Current;
            string[] entries={"Back to match rules",spirit!=null&&spirit.SeaSpiritActive?"Return from sea spirit":"Become sea spirit","Place / reposition tabletop island","Confirm board placement","Toggle passthrough / game", "Board width: "+(board?board.Width.ToString("0.0"):"--"),"Table height: "+(board?board.Height.ToString("0.00"):"--"),"Forward: "+(board?board.Forward.ToString("0.0"):"--"),"Side: "+(board?board.Side.ToString("0.0"):"--"),"Recenter sea spirit","Exit tabletop"};
            for(int i=0;i<buttons.Length;i++){bool show=i<entries.Length;buttons[i].gameObject.SetActive(show);targets[i].enabled=show&&(i==0||MatchRuntime.Current?.Rules.mode==GameModeKind.Demo);if(show)labels[i].text=entries[i];}
            Physics.SyncTransforms();var ray=new Ray(camera.transform.position,camera.transform.forward);
            for(int i=0;i<entries.Length;i++)
            {
                bool hover=targets[i].Raycast(ray,out _,4);buttons[i].image.color=hover?CandyHudVisuals.Mint:CandyHudVisuals.Card;labels[i].color=hover?CandyHudVisuals.Ink:Color.white;
                if(!hover||!pressed||!targets[i].enabled)continue;
                if(i==0){tabletopPage=false;modePage=true;return;}
                if(i==1&&spirit!=null)spirit.SetSeaSpirit(!spirit.SeaSpiritActive);
                if(!board)continue;
                if(i==2)board.Begin();if(i==3)board.Confirm();if(i==4)board.SetPassthrough(!board.Passthrough);
                if(i==5)board.Width=board.Width>=3?.4f:board.Width+.2f;
                if(i==6)board.Height=board.Height>=1.8f?0:board.Height+.05f;
                if(i==7)board.Forward=board.Forward>=3?.2f:board.Forward+.1f;
                if(i==8)board.Side=board.Side>=2?-2:board.Side+.1f;
                if(i==9)spirit?.RecenterSeaSpirit();if(i==10)board.End();return;
            }
            help.text=board?board.Status:"Local tabletop controls unavailable";
        }
    }
}
