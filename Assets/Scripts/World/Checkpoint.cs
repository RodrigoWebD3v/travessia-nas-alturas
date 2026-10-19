using UnityEngine;

/// <summary>
/// Plataforma segura: ao ser tocada vira o ponto de retorno após quedas.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Checkpoint : MonoBehaviour
{
    public Transform spawnPoint;
    public Renderer flagRenderer;
    public Color inactiveColor = new Color(0.55f, 0.55f, 0.6f);
    public Color activeColor = Color.white;

    public Vector3 SpawnPosition => (spawnPoint != null ? spawnPoint.position : transform.position) + Vector3.up * 0.1f;

    MaterialPropertyBlock block;

    void Start()
    {
        SetActivated(false);
    }

    public void SetActivated(bool active)
    {
        if (flagRenderer == null) return;
        block ??= new MaterialPropertyBlock();
        flagRenderer.GetPropertyBlock(block);
        block.SetColor("_BaseColor", active ? activeColor : inactiveColor);
        flagRenderer.SetPropertyBlock(block);
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.GetComponentInParent<PlayerController>() != null)
            GameManager.Instance.SetCheckpoint(this);
    }
}
