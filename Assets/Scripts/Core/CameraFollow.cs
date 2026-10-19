using UnityEngine;

/// <summary>
/// Câmera elevada atrás do jogador, sempre olhando para +Z (direção das travessias),
/// para que A/D corresponda à esquerda/direita da tela.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 4.2f, -7f);
    public Vector3 lookOffset = new Vector3(0f, 0.8f, 3f);
    public float smoothTime = 0.25f;

    Vector3 velocity;

    void Start()
    {
        if (target == null) return;
        transform.position = target.position + offset;
        transform.LookAt(target.position + lookOffset);
    }

    void LateUpdate()
    {
        if (target == null) return;
        transform.position = Vector3.SmoothDamp(transform.position, target.position + offset, ref velocity, smoothTime);
        transform.rotation = Quaternion.LookRotation(target.position + lookOffset - (target.position + offset));
    }
}
