using UnityEngine;

/// <summary>
/// Movimento lento de vaivém para nuvens e decoração.
/// </summary>
public class Floater : MonoBehaviour
{
    public Vector3 direction = new Vector3(1f, 0f, 0f);
    public float amplitude = 1.5f;
    public float speed = 0.2f;

    Vector3 origin;
    float phase;

    void Start()
    {
        origin = transform.position;
        phase = origin.x * 0.37f + origin.z * 0.11f;
    }

    void Update()
    {
        transform.position = origin + direction * Mathf.Sin(Time.time * speed + phase) * amplitude;
    }
}
