using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleWorldMenu
    {
        void ApplyLabelContrast()
        {
            heading.fontSize=Mathf.Max(heading.fontSize,28);heading.lineSpacing=.85f;
            help.fontSize=giveEquipmentPage?21:Mathf.Max(help.fontSize,22);help.lineSpacing=.8f;
            if(giveEquipmentPage)help.rectTransform.sizeDelta=new Vector2(help.rectTransform.sizeDelta.x,68);
            for(int i=0;i<buttons.Length;i++)
            {
                if(!buttons[i]||!buttons[i].gameObject.activeInHierarchy||!labels[i])continue;
                var label=labels[i];int minimum=label.rectTransform.rect.height<44?22:26;
                label.fontSize=Mathf.Max(label.fontSize,minimum);label.lineSpacing=.8f;
                if(label.resizeTextForBestFit)
                {
                    label.resizeTextMaxSize=Mathf.Max(label.resizeTextMaxSize,minimum);
                    label.resizeTextMinSize=Mathf.Max(label.resizeTextMinSize,minimum);
                }
                label.color=buttons[i].image.color.grayscale>.5f?CandyHudVisuals.Ink:
                    targets[i].enabled?Color.white:new Color(.85f,.81f,.92f);
            }
        }
    }
}
