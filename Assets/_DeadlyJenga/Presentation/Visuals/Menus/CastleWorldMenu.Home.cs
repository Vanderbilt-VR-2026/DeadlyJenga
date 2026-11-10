using UnityEngine;
using UnityEngine.UI;

namespace DeadlyJenga.CastlePrototype
{
    // The pause menu's first page: a compact card sized to its rows, with a big Resume and Leave at the bottom.
    // Every deeper page keeps the original tall layout, restored by LayoutDefault.
    public sealed partial class CastleWorldMenu
    {
        const float HomeScale=.0017f,DefaultScale=.002f,RowStep=66,RowHeight=58;
        int homeRows=-1;
        bool homeLayout;
        void LayoutHome()
        {
            homeLayout=true;
            transform.localScale=Vector3.one*HomeScale;
            bool exit=exitButton.gameObject.activeSelf;
            float height=300+homeRows*RowStep+(exit?100:30);
            var panel=(RectTransform)transform;panel.sizeDelta=new Vector2(600,height);
            float top=height*.5f;
            heading.text="PAUSED";heading.fontSize=heading.resizeTextMaxSize=42;heading.fontStyle=FontStyle.Bold;
            heading.rectTransform.anchoredPosition=new Vector2(0,top-52);heading.rectTransform.sizeDelta=new Vector2(560,56);
            joinCodeLabel.rectTransform.anchoredPosition=new Vector2(0,top-96);joinCodeLabel.fontSize=joinCodeLabel.resizeTextMaxSize=22;
            joinCodeLabel.text=joinCodeLabel.text.Replace("JOIN CODE  ","Party code  ");
            var resume=(RectTransform)backButton.transform;resume.anchoredPosition=new Vector2(0,top-160);resume.sizeDelta=new Vector2(540,74);
            backTarget.size=new Vector3(540,74,4);
            var resumeLabel=backButton.GetComponentInChildren<Text>();resumeLabel.text="Resume";resumeLabel.rectTransform.sizeDelta=new Vector2(520,70);
            resumeLabel.fontSize=resumeLabel.resizeTextMaxSize=30;resumeLabel.fontStyle=FontStyle.Bold;
            bool resumeHover=Hit(backTarget,pointerRay);
            backButton.image.color=resumeHover?new Color(.62f,1,.86f):new Color(.30f,.95f,.74f);resumeLabel.color=CandyHudVisuals.Ink;
            float first=top-250;
            for(int i=0;i<homeRows&&i<buttons.Length;i++)
            {
                var rect=(RectTransform)buttons[i].transform;rect.anchoredPosition=new Vector2(0,first-i*RowStep);rect.sizeDelta=new Vector2(540,RowHeight);
                targets[i].size=new Vector3(540,RowHeight,4);
                labels[i].fontSize=labels[i].resizeTextMaxSize=24;labels[i].rectTransform.sizeDelta=new Vector2(450,RowHeight-6);
                labels[i].alignment=TextAnchor.MiddleLeft;labels[i].rectTransform.anchoredPosition=new Vector2(30,0);
                sectionGlyphs[i].rectTransform.sizeDelta=new Vector2(32,32);sectionGlyphs[i].rectTransform.anchoredPosition=new Vector2(-236,0);
            }
            var leave=(RectTransform)exitButton.transform;leave.anchoredPosition=new Vector2(0,first-homeRows*RowStep-24);leave.sizeDelta=new Vector2(540,RowHeight);
            exitTarget.size=new Vector3(540,RowHeight,4);exitLabel.fontSize=exitLabel.resizeTextMaxSize=24;
            help.text=session.IsVR?"Point and pull the trigger  ·  Menu button closes":"Click a button  ·  Esc closes";
            help.fontSize=help.resizeTextMaxSize=18;help.rectTransform.anchoredPosition=new Vector2(0,-top+26);
        }
        // Called every frame before a page draws, so pages other than home look exactly as before.
        void LayoutDefault()
        {
            if(!homeLayout)return;
            homeLayout=false;
            transform.localScale=Vector3.one*DefaultScale;
            heading.fontSize=heading.resizeTextMaxSize=24;heading.fontStyle=FontStyle.Normal;heading.rectTransform.sizeDelta=new Vector2(570,95);
            joinCodeLabel.rectTransform.anchoredPosition=new Vector2(0,410);joinCodeLabel.fontSize=joinCodeLabel.resizeTextMaxSize=26;
            var back=(RectTransform)backButton.transform;back.sizeDelta=new Vector2(112,48);backTarget.size=new Vector3(112,48,4);
            var backLabel=backButton.GetComponentInChildren<Text>();backLabel.rectTransform.sizeDelta=new Vector2(108,48);backLabel.fontSize=backLabel.resizeTextMaxSize=17;backLabel.fontStyle=FontStyle.Normal;
            var leave=(RectTransform)exitButton.transform;leave.anchoredPosition=new Vector2(0,-407);leave.sizeDelta=new Vector2(510,54);exitTarget.size=new Vector3(510,54,4);
            exitLabel.fontSize=exitLabel.resizeTextMaxSize=22;help.fontSize=help.resizeTextMaxSize=19;
            for(int i=0;i<buttons.Length;i++)
            {
                targets[i].size=new Vector3(540,46,4);labels[i].fontSize=labels[i].resizeTextMaxSize=21;labels[i].alignment=TextAnchor.MiddleCenter;
                labels[i].rectTransform.sizeDelta=new Vector2(472,46);labels[i].rectTransform.anchoredPosition=new Vector2(20,0);
                sectionGlyphs[i].rectTransform.sizeDelta=new Vector2(27,27);sectionGlyphs[i].rectTransform.anchoredPosition=new Vector2(-243,0);
            }
        }
    }
}
