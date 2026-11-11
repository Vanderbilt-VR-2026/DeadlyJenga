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
        RectTransform marinaMirror;
        Texture2D marinaMaskTexture;
        Sprite marinaMaskSprite;
        string marinaDetail="";
        int terminalPageChangedFrame=-1;
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
            wardrobePanel.SetPositionAndRotation(WardrobePosition+new Vector3(2.4f,1.8f,.65f),Quaternion.Euler(0,75,0));
            var screenPosition=MayhemMarina.PlayScreen+Vector3.back*.12f;
            setupStation.SetPositionAndRotation(screenPosition,Quaternion.identity);
            setupStation.localScale=Vector3.one*.0040f;
            roomPage.SetPositionAndRotation(screenPosition,Quaternion.identity);
            roomPage.localScale=Vector3.one*.0037f;
            marinaScreen=gameObject.AddComponent<MarinaScreen>();
            marinaScreen.Build(lobby,OpenMarinaDetail);
            if(wardrobeCamera)wardrobeCamera.backgroundColor=new Color(.10f,.28f,.34f);
            DeadlyJenga.Presentation.MenuAppear.Attach(setupStation.gameObject,fade:false);DeadlyJenga.Presentation.MenuAppear.Attach(roomPage.gameObject,fade:false);
            BuildMarinaMirror();CloseMarinaDetail();SyncMarinaBacks();
        }
        void OpenMarinaDetail(string page)
        {
            if(page=="host")page="room";
            if((page=="maps"||page=="modes")&&!lobby.CanConfigure)return;
            if(page=="join"&&(!lobby.JoinEnabled||RoomHasGuests))return;
            marinaDetail=page;
            terminalPageChangedFrame=Time.frameCount;
            hostStation.gameObject.SetActive(page=="room");
            joinStation.gameObject.SetActive(page=="join");
            roomPage.gameObject.SetActive(page=="room"||page=="join");
            setupStation.gameObject.SetActive(page=="maps"||page=="modes"||page=="settings");
            ClosePrivateKeyboard();
            if(page=="maps"||page=="modes")OpenPicker(page);
            if(page=="settings")OpenMatchSettings();
            if(marinaScreen)marinaScreen.Visible=false;
            SyncMarinaBacks();
        }
        void CloseMarinaDetail()
        {
            ClosePrivateKeyboard();
            marinaDetail="";
            terminalPageChangedFrame=Time.frameCount;
            setupPage="main";
            roomPage.gameObject.SetActive(false);
            setupStation.gameObject.SetActive(!marina);
            if(wardrobeOpen)SetWardrobeOpen(false);
            if(marinaScreen)marinaScreen.Visible=true;
            SyncMarinaBacks();
        }
        partial void MarinaInteractionRange(ref float distance)
        {
            if(marina)distance=DeadlyJenga.Presentation.MenuPointer.LobbyRange;
        }
        partial void UpdateMayhemMarina()
        {
            if(marinaDetail=="join"&&RoomHasGuests)CloseMarinaDetail();
            if((marinaDetail=="maps"||marinaDetail=="modes")&&!lobby.IsHost)CloseMarinaDetail();
            if(marinaDetail!=""&&!lobby.CodeFocused&&Keyboard.current?.escapeKey.wasPressedThisFrame==true)
            {
                if(setupPage=="dev-settings")OpenMatchSettings();
                else CloseMarinaDetail();
            }
            SyncMarinaBacks();
            if(!marina)return;
            if(marinaScreen)marinaScreen.Visible=marinaDetail==""&&!wardrobeOpen;
            if(!wardrobeOpen&&Time.unscaledTime>=nextMarinaMirror&&Vector3.Distance(Player.transform.position,WardrobePosition)<6)
            {
                nextMarinaMirror=Time.unscaledTime+.2f;var previous=wardrobePlayer;wardrobePlayer=Player;
                try{ResetWardrobeView();PresentWardrobe();}finally{wardrobePlayer=previous;}
            }
        }
        void SyncMarinaBacks()
        {
            void Sync(string name,RectTransform front){var back=transform.Find(name+" / readable back");if(back)back.gameObject.SetActive(front&&front.gameObject.activeInHierarchy);}
            Sync("Room",roomPage);Sync("MODE + MAP",setupStation);
        }
        void BuildMarinaMirror()
        {
            var go=new GameObject("Marina live wardrobe mirror",typeof(RectTransform),typeof(Canvas),typeof(Image),typeof(Mask));go.transform.SetParent(transform,false);marinaMirror=go.GetComponent<RectTransform>();marinaMirror.position=new Vector3(-13,3.4f,10.08f);marinaMirror.localScale=Vector3.one*.01f;marinaMirror.sizeDelta=new Vector2(247,348);go.GetComponent<Canvas>().renderMode=RenderMode.WorldSpace;go.GetComponent<Canvas>().overrideSorting=true;go.GetComponent<Canvas>().sortingOrder=60;
            marinaMaskTexture=new Texture2D(64,64,TextureFormat.RGBA32,false);var pixels=new Color[4096];
            for(int y=0;y<64;y++)for(int x=0;x<64;x++){float d=Vector2.Distance(new Vector2(x+.5f,y+.5f),Vector2.one*32);pixels[y*64+x]=new Color(1,1,1,Mathf.Clamp01(32-d));}
            marinaMaskTexture.SetPixels(pixels);marinaMaskTexture.Apply();marinaMaskSprite=Sprite.Create(marinaMaskTexture,new Rect(0,0,64,64),Vector2.one*.5f);go.GetComponent<Image>().sprite=marinaMaskSprite;go.GetComponent<Image>().color=new Color(.42f,.68f,.75f);go.GetComponent<Mask>().showMaskGraphic=true;
            var image=new GameObject("Mirror image",typeof(RectTransform),typeof(RawImage));image.transform.SetParent(go.transform,false);var raw=image.GetComponent<RawImage>();raw.texture=wardrobeTexture;raw.color=Color.white;raw.raycastTarget=false;raw.rectTransform.sizeDelta=new Vector2(348,348);
            BuildMirrorRotateIcon(marinaMirror,-127,54);
        }
        partial void DisposeMayhemMarina()
        {if(marinaScreen)Destroy(marinaScreen);if(marina)Destroy(marina);if(marinaMirror)Destroy(marinaMirror.gameObject);if(marinaMaskSprite)Destroy(marinaMaskSprite);if(marinaMaskTexture)Destroy(marinaMaskTexture);}
    }
}
