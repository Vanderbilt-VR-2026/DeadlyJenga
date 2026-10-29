using UnityEditor;

namespace DeadlyJenga.DevTools.Editor
{
    public static class ValidationRunGate
    {
        const string Owner="DeadlyJenga.ValidationOwner";
        public static bool Owns(string key)=>SessionState.GetString(Owner,"")==key;
        public static bool TryAcquire(string key)
        {
            string current=SessionState.GetString(Owner,"");
            if(current.Length>0&&current!=key)
            {
                bool pending=SessionState.GetBool(current,false)||SessionState.GetBool(current+"Running",false);
                if(pending||EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.timeSinceStartup-SessionState.GetFloat(Owner+"Lease",0)<30)return false;
            }
            SessionState.SetString(Owner,key);SessionState.SetFloat(Owner+"Lease",(float)EditorApplication.timeSinceStartup);return true;
        }
        public static void Release(string key){if(SessionState.GetString(Owner,"")==key)SessionState.SetString(Owner,"");}
    }
}
