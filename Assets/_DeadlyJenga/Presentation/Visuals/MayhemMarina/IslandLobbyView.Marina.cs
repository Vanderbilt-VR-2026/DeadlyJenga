using System;
using DeadlyJenga.CandyArt;
using DeadlyJenga.CandyArt.Marina;
using DeadlyJenga.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class IslandLobbyView
    {
        MayhemMarina marina;
        MarinaScreen marinaScreen;
        RectTransform marinaBack,marinaMirror;
        Texture2D marinaMaskTexture;
        Sprite marinaMaskSprite;
        string marinaDetail="";
        float nextMarinaMirror;
        partial void BuildMayhemMarina()
        {
            if(!(lobby is IWorldQuery world)||world.Radius<30)return;
            if(marina)return;
            foreach(var resort in GetComponents<CandyLobbyResort>())
            {
                resort.enabled=false;
                var old=resort.transform.Find("Visual · Candy island resort");if(old)old.gameObject.SetActive(false);
            }
            foreach(string name in new[]{"Grounded red arcade cabinet","Host controller computer","Mode controller computer"})
            {
                var old=transform.Find(name);if(old)foreach(var r in old.GetComponentsInChildren<Renderer>())r.enabled=false;
            }
            marina=gameObject.AddComponent<MayhemMarina>();marina.Build(lobby,Environment);
            foreach(var television in GetComponentsInChildren<LobbySpectatorTVWall>(true))
            {
                television.transform.localPosition=new Vector3(-10,4.16f,20.2f);television.transform.localRotation=Quaternion.identity;
                lobby.AddBlocker(television.transform,new Vector3(0,1.48f,.03f),new Vector3(3.76f,2.95f,.55f));
            }
            wardrobeCenter=MayhemMarina.Wardrobe;
            var pod=transform.Find("Visual - wardrobe pod");if(pod)pod.gameObject.SetActive(false);
            wardrobePanel.SetPositionAndRotation(wardrobeCenter+new Vector3(2.8f,1.8f,1.3f),Quaternion.Euler(0,-35,0));
            joinStation.SetPositionAndRotation(new Vector3(-4.25f,3.5f,9.25f),Quaternion.Euler(0,-12,0));
            hostStation.SetPositionAndRotation(new Vector3(4.15f,3.5f,9.3f),Quaternion.Euler(0,12,0));
            setupStation.SetPositionAndRotation(MayhemMarina.PlayScreen+Vector3.back*.10f,Quaternion.identity);setupStation.localScale=Vector3.one*.0036f;
            piano.SetPositionAndRotation(new Vector3(-4.2f,2.65f,7.6f),Quaternion.Euler(35,-12,0));
            marinaScreen=gameObject.AddComponent<MarinaScreen>();marinaScreen.Build(lobby,OpenMarinaDetail);
            var go=new GameObject("Marina detail back control",typeof(RectTransform),typeof(Canvas));go.transform.SetParent(transform,false);marinaBack=go.GetComponent<RectTransform>();marinaBack.position=new Vector3(0,2.9f,4.15f);marinaBack.localScale=Vector3.one*.0028f;marinaBack.sizeDelta=new Vector2(1000,125);go.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;go.GetComponent<Canvas>().overrideSorting=true;go.GetComponent<Canvas>().sortingOrder=100;
            Add(marinaBack,"marina-close",()=>"BACK TO MARINA",0,CloseMarinaDetail,()=>true,0,1000,125);
            if(wardrobeCamera)wardrobeCamera.backgroundColor=new Color(.10f,.28f,.34f);
            BuildMarinaMirror();CloseMarinaDetail();SyncMarinaBacks();
        }
        void OpenMarinaDetail(string page)
        {
            if(page=="style"){if(Vector3.Distance(Player.transform.position,WardrobePosition)>2)Player.Place(WardrobePosition+Vector3.up*.04f,transform.rotation);SetWardrobeOpen(true);return;}
            marinaDetail=page;joinStation.gameObject.SetActive(page=="join");hostStation.gameObject.SetActive(page=="host");setupStation.gameObject.SetActive(page=="maps"||page=="modes"||page=="settings");piano.gameObject.SetActive(page=="join");
            if(page=="maps"||page=="modes")OpenPicker(page);
            if(page=="settings"){setupPage="settings";settingsPage=0;}
            marinaScreen.Visible=false;marinaBack.gameObject.SetActive(true);SyncMarinaBacks();
        }
        void CloseMarinaDetail()
        {
            marinaDetail="";joinStation.gameObject.SetActive(false);hostStation.gameObject.SetActive(false);setupStation.gameObject.SetActive(false);piano.gameObject.SetActive(false);wardrobePanel.gameObject.SetActive(false);
            if(wardrobeOpen)SetWardrobeOpen(false);if(marinaBack)marinaBack.gameObject.SetActive(false);if(marinaScreen)marinaScreen.Visible=true;SyncMarinaBacks();
        }
        partial void MarinaInteractionRange(ref float distance){if(marina)distance=11;}
        partial void UpdateMayhemMarina()
        {
            if(!marina)return;
            if(marinaDetail!=""&&Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame)CloseMarinaDetail();
            if(marinaScreen)marinaScreen.Visible=marinaDetail==""&&!wardrobeOpen;
            if(wardrobePanel)wardrobePanel.gameObject.SetActive(wardrobeOpen&&Player.IsVR);
            SyncMarinaBacks();
            if(!wardrobeOpen&&Time.unscaledTime>=nextMarinaMirror&&Vector3.Distance(Player.transform.position,WardrobePosition)<6)
            {
                nextMarinaMirror=Time.unscaledTime+.2f;var previous=wardrobePlayer;wardrobePlayer=Player;
                try{ResetWardrobeView();PresentWardrobe();}finally{wardrobePlayer=previous;}
            }
        }
        void SyncMarinaBacks()
        {
            void Sync(string name,RectTransform front){var back=transform.Find(name+" / readable back");if(back)back.gameObject.SetActive(front&&front.gameObject.activeInHierarchy);}
            Sync("JOIN",joinStation);Sync("HOST",hostStation);Sync("MODE + MAP",setupStation);Sync("WARDROBE",wardrobePanel);
        }
        void BuildMarinaMirror()
        {
            var go=new GameObject("Marina live wardrobe mirror",typeof(RectTransform),typeof(Canvas),typeof(Image),typeof(Mask));go.transform.SetParent(transform,false);marinaMirror=go.GetComponent<RectTransform>();marinaMirror.position=new Vector3(-13,3.4f,10.08f);marinaMirror.localScale=Vector3.one*.01f;marinaMirror.sizeDelta=new Vector2(247,348);go.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;go.GetComponent<Canvas>().overrideSorting=true;go.GetComponent<Canvas>().sortingOrder=60;
            marinaMaskTexture=new Texture2D(64,64,TextureFormat.RGBA32,false);var pixels=new Color[4096];
            for(int y=0;y<64;y++)for(int x=0;x<64;x++){float d=Vector2.Distance(new Vector2(x+.5f,y+.5f),Vector2.one*32);pixels[y*64+x]=new Color(1,1,1,Mathf.Clamp01(32-d));}
            marinaMaskTexture.SetPixels(pixels);marinaMaskTexture.Apply();marinaMaskSprite=Sprite.Create(marinaMaskTexture,new Rect(0,0,64,64),Vector2.one*.5f);go.GetComponent<Image>().sprite=marinaMaskSprite;go.GetComponent<Image>().color=new Color(.42f,.68f,.75f);go.GetComponent<Mask>().showMaskGraphic=true;
            var image=new GameObject("Mirror image",typeof(RectTransform),typeof(RawImage));image.transform.SetParent(go.transform,false);var raw=image.GetComponent<RawImage>();raw.texture=wardrobeTexture;raw.color=Color.white;raw.raycastTarget=false;raw.rectTransform.sizeDelta=new Vector2(348,348);
        }
        partial void DisposeMayhemMarina()
        {if(marinaScreen)Destroy(marinaScreen);if(marina)Destroy(marina);if(marinaBack)Destroy(marinaBack.gameObject);if(marinaMirror)Destroy(marinaMirror.gameObject);if(marinaMaskSprite)Destroy(marinaMaskSprite);if(marinaMaskTexture)Destroy(marinaMaskTexture);}
    }
}
