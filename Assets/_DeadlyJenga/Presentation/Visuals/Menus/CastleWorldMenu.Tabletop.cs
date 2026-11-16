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
            HideDisasterSliders();var spirit=Services.Session as ISeaSpiritControls;var board=CandyTabletop.Current;
            string[] entries={developerChild?"Back to "+developerRoute:"Back to match rules",spirit!=null&&spirit.SeaSpiritActive?"Return from sea spirit":"Become sea spirit","Place / reposition tabletop island","Confirm board placement","Toggle passthrough / game", "Board width: "+(board?board.Width.ToString("0.0"):"--"),"Table height: "+(board?board.Height.ToString("0.00"):"--"),"Forward: "+(board?board.Forward.ToString("0.0"):"--"),"Side: "+(board?board.Side.ToString("0.0"):"--"),"Recenter sea spirit","Exit tabletop"};
            var names=new System.Collections.Generic.List<string>(entries);
            var actions=new System.Collections.Generic.List<System.Action>();
            var enabled=new System.Collections.Generic.List<bool>();
            var icons=new System.Collections.Generic.List<CandyHudIcon>();
            var captions=new System.Collections.Generic.List<string>();
            var pictures=new System.Collections.Generic.List<Texture>();
            for(int slot=0;slot<entries.Length;slot++)
            {
                int i=slot;enabled.Add(i==0||i==10||CanCommand);icons.Add(CandyHudIcon.Settings);captions.Add("");pictures.Add(null);
                actions.Add(()=>{
                    if(i==0){tabletopPage=false;if(developerChild)developerChild=false;else modePage=true;return;}
                    if(i==1&&spirit!=null)spirit.SetSeaSpirit(!spirit.SeaSpiritActive);
                    if(!board)return;
                    if(i==2)board.Begin();if(i==3)board.Confirm();if(i==4)board.SetPassthrough(!board.Passthrough);
                    if(i==5)board.Width=board.Width>=3?.4f:board.Width+.2f;
                    if(i==6)board.Height=board.Height>=1.8f?0:board.Height+.05f;
                    if(i==7)board.Forward=board.Forward>=3?.2f:board.Forward+.1f;
                    if(i==8)board.Side=board.Side>=2?-2:board.Side+.1f;
                    if(i==9)spirit?.RecenterSeaSpirit();if(i==10)board.End();
                });
            }
            RenderOrganizedMenuGrid(names,actions,enabled,icons,captions,pictures,pressed);
            help.text=board?board.Status:"Local tabletop controls unavailable";
        }
    }
}
