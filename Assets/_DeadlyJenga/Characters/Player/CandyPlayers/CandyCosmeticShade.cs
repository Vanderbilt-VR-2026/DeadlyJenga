using DeadlyJenga.Core;
using UnityEngine;

namespace DeadlyJenga.CandyPlayers
{
    internal static class CandyCosmeticShade
    {
        public static Color PlayerShade(Color player, AvatarHairShade shade)
        {
            if (shade is AvatarHairShade.Original or AvatarHairShade.Match) return player;
            var basis = ToLab(player);
            if (shade == AvatarHairShade.Dark)
            {
                var firstPass = ToLab(player * .23f);
                var secondPass = new Vector3(Mathf.Max(0, basis.x - .12f), basis.y * .8f, basis.z * .8f);
                secondPass = InGamut(secondPass);
                return ToRgb(InGamut((firstPass + secondPass) * .5f));
            }
            var oldLight = ToLab(Color.Lerp(player, Color.white, .42f));
            var newLight = InGamut(new Vector3(.89f, basis.y * .57f, basis.z * .57f));
            var light = new Vector3(Mathf.Max(.8535533f, Mathf.Lerp(basis.x, 1f, .35f)),
                (oldLight.y + newLight.y) * .5f, (oldLight.z + newLight.z) * .5f);
            return ToRgb(InGamut(light));
        }

        static Vector3 ToLab(Color input)
        {
            var c = input.linear;
            var l = Mathf.Pow(.41222147f*c.r + .53633254f*c.g + .05144599f*c.b, 1f/3);
            var m = Mathf.Pow(.21190350f*c.r + .68069955f*c.g + .10739696f*c.b, 1f/3);
            var s = Mathf.Pow(.08830246f*c.r + .28171884f*c.g + .62997870f*c.b, 1f/3);
            return new Vector3(.21045426f*l + .79361779f*m - .00407205f*s,
                1.97799850f*l - 2.42859221f*m + .45059371f*s,
                .02590404f*l + .78277177f*m - .80867577f*s);
        }

        static Color LinearRgb(Vector3 lab)
        {
            float l = lab.x + .39633778f*lab.y + .21580376f*lab.z;
            float m = lab.x - .10556135f*lab.y - .06385417f*lab.z;
            float s = lab.x - .08948418f*lab.y - 1.29148555f*lab.z;
            l *= l*l; m *= m*m; s *= s*s;
            return new Color(4.07674166f*l - 3.30771159f*m + .23096993f*s,
                -1.26843800f*l + 2.60975740f*m - .34131940f*s,
                -.00419609f*l - .70341861f*m + 1.70761470f*s);
        }

        static Vector3 InGamut(Vector3 lab)
        {
            float low = 0f, high = 1f;
            for (int step = 0; step < 16; step++)
            {
                float mid = (low + high)*.5f;
                var rgb = LinearRgb(new Vector3(lab.x, lab.y*mid, lab.z*mid));
                if (rgb.r >= 0f && rgb.r <= 1f && rgb.g >= 0f && rgb.g <= 1f && rgb.b >= 0f && rgb.b <= 1f)
                    low = mid;
                else
                    high = mid;
            }
            return new Vector3(lab.x, lab.y*low, lab.z*low);
        }

        static Color ToRgb(Vector3 lab)
        {
            var rgb = LinearRgb(lab);
            return new Color(Mathf.Clamp01(rgb.r), Mathf.Clamp01(rgb.g), Mathf.Clamp01(rgb.b)).gamma;
        }
    }
}
