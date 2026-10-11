using System.Xml.Serialization;
using Fusion;
using UnityEngine;

// a single tower block. only the host can change it
[RequireComponent(typeof(Rigidbody))]
public class JengaBlock: NetworkBehaviour
{
    [Networked] public int Layer { get; set; }
    [Networked] public int Index { get; set; }

    public Rigidbody Body { get; private set; }
    public bool IsHost => Object != null && Object.HasStateAuthority;
    public TowerGenerator Tower { get; set; }

    void Awake() => Body = GetComponent<Rigidbody>();

    public override void Spawned()
    {
        name = $"Block_L{Layer}_{Index}";
    }

    // call this to destroy a block. host only, does nothing on clients
    [ContextMenu("Remove")]
    public void Remove()
    {
        if (!IsHost) return;
        Runner.Despawn(Object);
    }

    // how much of the tower this block holds up (1, 2, 3)
    public float GetLoad(out bool lastInLayer)
    {
        lastInLayer = false;
        if (Tower == null || !Tower.OnTower(transform.position)) return 0f;

        float myY = transform.position.y;
        float halfH = Tower.BlockHeight * 0.5f;
        int above = 0, sameLayer = 0;

        foreach (var rb in Tower.Blocks)
        {
            if (rb == null || !Tower.OnTower(rb.position)) continue;
            float dy = rb.position.y - myY;
            if (dy > halfH) above++;
            else if (Mathf.Abs(dy) <= halfH) sameLayer++;
        }

        lastInLayer = sameLayer == 1 && above > 0;

        // bottom block in a full layer = 1 which is full load
        float maxLoad = Mathf.Max(1f, (Tower.Blocks.Count - Tower.BlocksPerLayer) / (float)Tower.BlocksPerLayer);
        return Mathf.Clamp01(above / (float)sameLayer / maxLoad);
    }

    [ContextMenu("Log Load")]
    void LogLoad()
    {
        float load = GetLoad(out bool last);
        Debug.Log($"{name}: load {load:0.00}, last in layer: {last}", this);
    }

    PullRules rules;
    PlayerRef puller;
    float pullSpeed = 1f;
    Vector3 pullTarget, lastHandPos;
    public bool IsBeingPulled { get; private set; }

    // check if player is on cooldown or if the block is locked
    public bool TryStartPull(PlayerRef player, Vector3 handPos)
    {
        if (!IsHost || IsBeingPulled || Tower == null) return false;
        if (rules == null) rules = Tower.GetComponent<PullRules>();
        if (rules == null || !rules.IsReady(player)) return false;

        float load = GetLoad(out bool lastInLayer);
        if (lastInLayer && rules.LockLastInLayer) return false;

        puller = player;
        pullSpeed = rules.SpeedForLoad(load);
        pullTarget = Body.position;
        lastHandPos = handPos;
        IsBeingPulled = true;
        return true;
    }

    // call every tick while holding. heavier blocks move less than the hand
    public void PullToward(Vector3 handPos)
    {
        if (!IsHost || !IsBeingPulled) return;

        pullTarget += (handPos - lastHandPos) * pullSpeed;
        lastHandPos = handPos;

        pullTarget = Body.position + Vector3.ClampMagnitude(pullTarget - Body.position, Tower.BlockHeight * 0.5f);

        Vector3 velocity = (pullTarget - Body.position) * rules.FollowStrength;
        Body.linearVelocity = Vector3.ClampMagnitude(velocity, rules.MaxSpeed);
    }

    // call when the hand lets go. cooldown only starts if the block actually left the tower
    public void EndPull()
    {
        if (!IsHost || !IsBeingPulled) return;
        IsBeingPulled = false;
        if (!Tower.OnTower(Body.position))
            rules.StartCooldown(puller);
    }

    // dev test. fakes a hand pulling block straight out
    [ContextMenu("Test Pull")]
    void TestPull() => StartCoroutine(TestPullRoutine());

    System.Collections.IEnumerator TestPullRoutine()
    {
        Vector3 start = Body.position, hand = start;
        if (!TryStartPull(Runner.LocalPlayer, hand))
        {
            Debug.Log($"{name}: cant pull (cooldown or locked)", this);
            yield break;
        }
        for (float t = 0; t < 4f; t += Time.fixedDeltaTime)
        {
            hand += transform.forward * (3f * Time.fixedDeltaTime);
            PullToward(hand);
            yield return new WaitForFixedUpdate();
        }
        EndPull();
        Debug.Log($"{name}: hand moved {(hand - start).magnitude:0.0}, block moved {(Body.position - start).magnitude:0.0}", this);
    }

}