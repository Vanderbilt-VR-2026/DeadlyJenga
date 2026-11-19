using System;
using UnityEngine;

namespace DeadlyJenga.CastleSocial
{
    [Serializable]
    public sealed class OnDeviceConversationSettings
    {
        [Range(1,2)] public int cpuThreads=1;
        [Range(512,2048)] public int contextTokens=1024;
        [Range(12,64)] public int replyTokens=40;
        [Range(1000,15000)] public int generationTimeoutMs=5000;
        [Tooltip("Defer new language-model replies above this rolling frame time. Zero disables the guard.")]
        [Range(0,80)] public float frameBudgetMs=22;
        [Range(.2f,3)] public float quietBeforeReply=.45f;
        [Range(0,6)] public float minimumReplyGap=1;
        [Range(4,32)] public int cachedVoiceClips=24;
        [Tooltip("Use the language model for observed game events as well as direct player conversation.")]
        public bool improviseGameReactions;
        [Tooltip("Developer pack location; builds use their embedded StreamingAssets pack.")]
        internal const string DefaultEditorPack="Tools/OnDeviceConversation/.models/pocket";
        public string editorPackFolder=DefaultEditorPack;
    }
}
