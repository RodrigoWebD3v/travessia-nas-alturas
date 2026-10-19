using UnityEngine;

/// <summary>
/// Bandeira de chegada: conclui a fase quando a meta de pontos foi atingida.
/// </summary>
[RequireComponent(typeof(Collider))]
public class LevelExit : MonoBehaviour
{
    void OnTriggerEnter(Collider other) => Check(other);
    void OnTriggerStay(Collider other) => Check(other);

    void Check(Collider other)
    {
        if (other.GetComponentInParent<PlayerController>() != null)
            GameManager.Instance.ReachFlag();
    }
}
