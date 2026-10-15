
using UnityEngine;

public class OceanFlow : MonoBehaviour
{
    public Vector2 flowSpeed = new Vector2(0.01f, 0.005f);

    private Material mat;
    private Vector2 offset;

    void Start()
    {
        Renderer rend = GetComponentInChildren<Renderer>();
        if (rend == null) { enabled = false; return; }

        mat = rend.material;
        offset = mat.GetTextureOffset("_BaseMap");
    }

    void Update()
    {
        offset += flowSpeed * Time.deltaTime;
        mat.SetTextureOffset("_BaseMap", offset);
    }
}
