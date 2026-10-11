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

    void Awake() => Body = GetComponent<Rigidbody>();

    public override void Spawned()
    {
        name = $"Block_L{Layer}_{Index}";
    }

    // call this to destroy a block. host only, does nothing on clients
    public void Remove()
    {
        if (!IsHost) return;
        Runner.Despawn(Object);
    }
}