using System.Collections.Generic;
using Fusion;
using UnityEngine;

// tuning for how hands pull blocks. put this on the Tower object
public class PullRules : MonoBehaviour
{
    [Header("Cooldown")]
    [SerializeField, Min(0f)] float baseCooldown = 6f;
    [SerializeField, Min(1)] int handCount = 1; // number of dead players, set by game code later

    [Header("Drag")]
    [SerializeField, Range(0.01f, 1f)] float heaviestSpeed = 0.15f; // speed of a block with load 1
    [SerializeField, Min(0.1f)] float maxSpeed = 10f;
    [SerializeField, Min(1f)] float followStrength = 15f;
    [SerializeField] bool lockLastInLayer = true;

    readonly Dictionary<PlayerRef, float> readyAt = new();

    public int HandCount { get => handCount; set => handCount = Mathf.Max(1, value); }
    public bool LockLastInLayer => lockLastInLayer;
    public float MaxSpeed => maxSpeed;
    public float FollowStrength => followStrength;

    // more hands = longer cooldown each, so danger goes up but doesn't double
    public float Cooldown => baseCooldown * Mathf.Sqrt(handCount);

    public bool IsReady(PlayerRef player) => !readyAt.TryGetValue(player, out var t) || Time.time >= t;
    public void StartCooldown(PlayerRef player) => readyAt[player] = Time.time + Cooldown;

    // 1 = block follows the hand exactly
    public float SpeedForLoad(float load) => Mathf.Lerp(1f, heaviestSpeed, load);
}