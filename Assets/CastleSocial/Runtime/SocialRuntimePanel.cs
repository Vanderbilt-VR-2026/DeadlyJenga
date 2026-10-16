using System.Linq;
using UnityEngine;

namespace DeadlyJenga.CastleSocial
{
    public sealed class SocialRuntimePanel : MonoBehaviour
    {
        SocialWorld world;Vector2 scroll;string selected="";bool muted;
        public void Initialize(SocialWorld value){world=value;}
        void OnGUI()
        {
            if(!world||!world.Settings||!world.Settings.showRuntimePanel)return;
            if(!world.Session.MenuOpen)
            {
                if(world.Settings.configuration.enableVoice&&world.Caption!="Social simulation ready")GUI.Label(new Rect(20,Screen.height-62,Mathf.Max(200,Screen.width-40),50),world.Caption);
                return;
            }
            // Desktop controls are embedded in CandyDemoHud's Settings page;
            // headset controls use the existing world menu. No competing overlay.
        }
        public void DrawControls()
        {
            if(!world||!world.Settings)return;
            GUILayout.Label("AI players · social & voice");GUILayout.Label(world.IsAuthority?"Host simulation":"Joined host simulation");
            world.Config.enableVoice=GUILayout.Toggle(world.Config.enableVoice,"Enable bot voices");
            GUILayout.Label("Bot volume");world.Settings.botVolume=GUILayout.HorizontalSlider(world.Settings.botVolume,0,1);
            GUILayout.Label("Human voice volume");world.Settings.humanVolume=GUILayout.HorizontalSlider(world.Settings.humanVolume,0,1);
            if(GUILayout.Button(world.MicrophoneInput.Capturing?"Mute microphone":"Enable microphone (permission required)"))world.MicrophoneInput.EnableCapture(!world.MicrophoneInput.Capturing);
            if(!world.MicrophoneInput.Capturing&&Microphone.devices.Length>1)
            {var devices=Microphone.devices;int current=System.Array.IndexOf(devices,world.MicrophoneInput.Device);int chosen=GUILayout.SelectionGrid(Mathf.Max(0,current),devices,1);world.MicrophoneInput.Device=devices[chosen];}
            GUILayout.Label(world.MicrophoneInput.Status);GUILayout.Label(world.Speech.Status);
            scroll=GUILayout.BeginScrollView(scroll,GUILayout.MaxHeight(210));
            foreach(var n in world.Participants.Values.OrderBy(n=>n.name))
            {
                if(GUILayout.Button(n.name+" [AI]")){selected=n.id;muted=world.Playback.IsMuted(selected);}
            }
            GUILayout.EndScrollView();
            if(!string.IsNullOrEmpty(selected))
            {bool value=GUILayout.Toggle(muted,"Mute selected AI player");if(value!=muted){muted=value;world.Mute(selected,muted);}GUILayout.Label("Selected AI volume");world.Playback.SetVolume(selected,GUILayout.HorizontalSlider(world.Playback.Volume(selected),0,1));}
            GUILayout.Label(world.Caption);
        }
    }
}
