using UnityEngine;

public enum CollectibleType { Coin, Heart, Boot, Slime, Spikes, Clock }

/// <summary>
/// Itens da fase. Cada um aplica o efeito uma vez e desaparece; os espinhos ficam
/// no lugar e causam dano respeitando o intervalo de proteção do jogador.
/// Nenhum item retira pontos.
/// </summary>
[RequireComponent(typeof(Collider))]
public class Collectible : MonoBehaviour
{
    public CollectibleType type;

    [Header("Efeitos")]
    public int points = 10;
    public float healthAmount = 25f;
    public float speedAmount = 0.5f;
    public float timeAmount = 10f;
    public float damage = 25f;

    [Header("Animação")]
    public Transform visual;
    public float spinSpeed = 120f;
    public float bobAmplitude = 0.12f;
    public float bobSpeed = 2.5f;

    bool consumed;
    Vector3 visualBase;
    float phase;

    bool Persistent => type == CollectibleType.Spikes;

    void Start()
    {
        if (visual != null) visualBase = visual.localPosition;
        phase = transform.position.x * 0.7f + transform.position.z * 0.3f;
    }

    void Update()
    {
        if (visual == null || Persistent) return;
        visual.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        visual.localPosition = visualBase + Vector3.up * Mathf.Sin(Time.time * bobSpeed + phase) * bobAmplitude;
    }

    void OnTriggerEnter(Collider other) => TryApply(other);

    void OnTriggerStay(Collider other)
    {
        if (Persistent) TryApply(other);
    }

    void TryApply(Collider other)
    {
        if (consumed) return;
        var player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;
        var game = GameManager.Instance;
        if (!game.IsPlaying) return;

        var stats = player.Stats;
        var audio = AudioManager.Instance;
        var hud = game.hud;

        switch (type)
        {
            case CollectibleType.Coin:
                game.AddPoints(points);
                audio.PlaySfx(audio.coin);
                hud.ShowToast($"+{points}", new Color(1f, 0.85f, 0.3f));
                break;
            case CollectibleType.Heart:
                stats.Heal(healthAmount);
                audio.PlaySfx(audio.heart);
                hud.ShowToast($"+{healthAmount:0} vida", new Color(1f, 0.5f, 0.6f));
                break;
            case CollectibleType.Boot:
                stats.ChangeSpeed(speedAmount);
                audio.AddMusicPitch();
                audio.PlaySfx(audio.boot);
                hud.ShowToast("Velocidade +", new Color(0.4f, 0.75f, 1f));
                break;
            case CollectibleType.Slime:
                stats.ChangeSpeed(-speedAmount);
                audio.PlaySfx(audio.slime);
                hud.ShowToast("Velocidade -", new Color(0.75f, 0.5f, 1f));
                break;
            case CollectibleType.Spikes:
                if (!stats.TakeDamage(damage)) return;
                player.Knockback(transform.position);
                audio.PlaySfx(audio.spikes);
                hud.ShowToast($"-{damage:0} vida", new Color(1f, 0.4f, 0.4f));
                break;
            case CollectibleType.Clock:
                game.AddTime(timeAmount);
                audio.PlaySfx(audio.clock);
                hud.ShowToast($"+{timeAmount:0} s", new Color(0.5f, 0.85f, 1f));
                break;
        }

        if (!Persistent)
        {
            consumed = true;
            gameObject.SetActive(false);
        }
    }
}
