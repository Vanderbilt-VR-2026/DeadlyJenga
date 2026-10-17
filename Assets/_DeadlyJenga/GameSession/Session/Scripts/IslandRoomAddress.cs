using System;
using System.Security.Cryptography;

namespace DeadlyJenga.CastlePrototype
{
    public static class IslandRoomAddress
    {
        public static readonly string[] Regions={"us","usw","eu","asia","au","jp","kr"};
        const string Alphabet="ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        public static string Create(string region)
        {
            var bytes=new byte[6];using(var random=RandomNumberGenerator.Create())random.GetBytes(bytes);
            var letters=new char[6];for(int i=0;i<6;i++)letters[i]=Alphabet[bytes[i]%Alphabet.Length];
            return region.ToUpperInvariant()+"-"+new string(letters);
        }
        public static bool TryParse(string input,string fallbackRegion,out string region,out string code)
        {
            region=fallbackRegion;code="";input=(input??"").Trim().ToUpperInvariant();
            var pieces=input.Split('-');if(pieces.Length==2){region=pieces[0].ToLowerInvariant();input=pieces[1];}else if(pieces.Length!=1)return false;
            if(Array.IndexOf(Regions,region)<0||input.Length!=6)return false;
            foreach(char c in input)if(Alphabet.IndexOf(c)<0)return false;
            code=region.ToUpperInvariant()+"-"+input;return true;
        }
    }
}
