using UnityEngine;

/// <summary>
/// Ajusta a repetição da textura de acordo com a escala do objeto (plataformas).
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(Renderer))]
public class AutoTiling : MonoBehaviour
{
    public float unitsPerTile = 2f;
    public bool useXZ = true;

    void OnEnable() => Apply();
    void OnValidate() => Apply();

    void Apply()
    {
        var r = GetComponent<Renderer>();
        if (r == null) return;
        Vector3 s = transform.lossyScale;
        Vector2 tiling = useXZ ? new Vector2(s.x, s.z) : new Vector2(s.x, s.y);
        var block = new MaterialPropertyBlock();
        r.GetPropertyBlock(block);
        block.SetVector("_BaseMap_ST", new Vector4(tiling.x / unitsPerTile, tiling.y / unitsPerTile, 0f, 0f));
        r.SetPropertyBlock(block);
    }
}
