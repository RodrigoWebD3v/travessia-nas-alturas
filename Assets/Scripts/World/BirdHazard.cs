using UnityEngine;

/// <summary>
/// Pássaro que cruza a corda na altura do peito. Se acertar o Equilibrista em pé,
/// tira vida e empurra o equilíbrio; agachado (Ctrl), ele passa por cima.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BirdHazard : MonoBehaviour
{
    [Header("Voo")]
    public float speed = 6f;
    public float lifeDistance = 26f;
    public float bobAmplitude = 0.06f;

    [Header("Asas")]
    public Transform wingL;
    public Transform wingR;
    public float flapSpeed = 16f;
    public float flapAngle = 45f;

    [Header("Golpe")]
    public float damage = 25f;
    [Tooltip("Empurrão no equilíbrio (graus/s) quando acerta na corda.")]
    public float push = 30f;

    Vector3 direction;
    Vector3 origin;
    float travelled;
    bool hit;
    Quaternion wingLRest, wingRRest;

    public void Launch(Vector3 start, Vector3 flightDirection, float flightSpeed)
    {
        origin = start;
        transform.position = start;
        direction = flightDirection.normalized;
        speed = flightSpeed;
        transform.rotation = Quaternion.LookRotation(direction);
    }

    void Awake()
    {
        if (wingL != null) wingLRest = wingL.localRotation;
        if (wingR != null) wingRRest = wingR.localRotation;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        travelled += speed * dt;
        transform.position = origin + direction * travelled
                           + Vector3.up * Mathf.Sin(travelled * 2.5f) * bobAmplitude;

        // Batida de asas: as duas giram em sentidos espelhados em torno do eixo do corpo.
        float flap = Mathf.Sin(Time.time * flapSpeed) * flapAngle;
        if (wingL != null) wingL.localRotation = wingLRest * Quaternion.AngleAxis(flap, Vector3.forward);
        if (wingR != null) wingR.localRotation = wingRRest * Quaternion.AngleAxis(-flap, Vector3.forward);

        if (travelled >= lifeDistance) Destroy(gameObject);
    }

    void OnTriggerEnter(Collider other)
    {
        if (hit) return;
        var player = other.GetComponentInParent<PlayerController>();
        if (player == null || !GameManager.Instance.IsPlaying) return;

        if (player.HitByBird(direction, damage, push))
        {
            hit = true;
            var audio = AudioManager.Instance;
            audio.PlaySfx(audio.birdHit);
            GameManager.Instance.hud.ShowToast($"-{damage:0} vida", new Color(1f, 0.4f, 0.4f));
        }
    }
}
