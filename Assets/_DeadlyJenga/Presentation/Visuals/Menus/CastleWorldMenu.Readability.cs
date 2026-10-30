using UnityEngine;

namespace DeadlyJenga.CastlePrototype
{
    public sealed partial class CastleWorldMenu
    {
        void LateUpdate()
        {
            for(int i=0;i<buttons.Length;i++)
            {
                if(!buttons[i]||!buttons[i].gameObject.activeInHierarchy||!labels[i])continue;
                labels[i].color=buttons[i].image.color.grayscale>.5f?CandyHudVisuals.Ink:
                    targets[i].enabled?Color.white:new Color(.85f,.81f,.92f);
            }
        }
    }
}
