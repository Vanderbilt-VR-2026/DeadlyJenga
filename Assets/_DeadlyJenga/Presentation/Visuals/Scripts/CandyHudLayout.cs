using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.Presentation
{
    // All gameplay overlays share the same coordinates, including in the headset.
    public static class CandyHudLayout
    {
        public static readonly Vector2 ReferenceSize=new Vector2(1280,800);
        public static readonly Vector2 StaminaPosition=new Vector2(0,-316);
        public static void Configure(Canvas canvas,Transform owner,Camera view,bool vr)
        {
            var root=canvas.GetComponent<RectTransform>();
            var scaler=canvas.GetComponent<CanvasScaler>();
            if(scaler){scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=ReferenceSize;scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.Expand;}
            if(vr&&view)
            {
                root.SetParent(view.transform,false);canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=view;
                root.sizeDelta=ReferenceSize;root.localPosition=new Vector3(0,0,1.1f);root.localRotation=Quaternion.identity;root.localScale=Vector3.one*.0007f;
            }
            else{root.SetParent(owner,false);canvas.renderMode=RenderMode.ScreenSpaceOverlay;root.localScale=Vector3.one;}
        }
        public static string PlayerName(MatchPlayerFrame player)
            =>"<color=#"+ColorUtility.ToHtmlStringRGB(player.color)+">"+(player.name??"").Replace("<","‹").Replace(">","›")+"</color>";
        public static Vector2 MarkerPosition(Vector3 viewport,Vector2 size,bool privateChoices=false)
        {
            var half=size*.5f;
            return new Vector2(Mathf.Clamp((viewport.x-.5f)*ReferenceSize.x,-600+half.x,280-half.x),
                Mathf.Clamp((viewport.y-.5f)*ReferenceSize.y,-140+half.y,(privateChoices?16:110)-half.y));
        }
    }
}
