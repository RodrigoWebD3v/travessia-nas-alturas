using UnityEngine;

/// <summary>
/// Vida, velocidade e proteção temporária contra dano do Equilibrista.
/// </summary>
public class PlayerStats : MonoBehaviour
{
    [Header("Vida")]
    public float maxHealth = 100f;
    public float invulnerabilityTime = 1.5f;

    [Header("Velocidade (unidades/segundo)")]
    public float baseSpeed = 3f;
    public float minSpeed = 2f;
    public float maxSpeed = 5f;

    public float Health { get; private set; }
    public float Speed { get; private set; }
    public bool IsInvulnerable => invulnerableTimer > 0f;

    float invulnerableTimer;

    void Awake()
    {
        Health = maxHealth;
        Speed = baseSpeed;
    }

    void Update()
    {
        if (invulnerableTimer > 0f) invulnerableTimer -= Time.deltaTime;
    }

    public void Heal(float amount)
    {
        Health = Mathf.Min(maxHealth, Health + amount);
    }

    /// <summary>Retorna true se o dano foi aplicado.</summary>
    public bool TakeDamage(float amount, bool ignoreInvulnerability = false)
    {
        if (Health <= 0f) return false;
        if (!ignoreInvulnerability && IsInvulnerable) return false;

        Health = Mathf.Max(0f, Health - amount);
        invulnerableTimer = invulnerabilityTime;
        if (Health <= 0f) GameManager.Instance.TriggerGameOver();
        return true;
    }

    public void ChangeSpeed(float delta)
    {
        Speed = Mathf.Clamp(Speed + delta, minSpeed, maxSpeed);
    }

    public void GrantInvulnerability(float seconds)
    {
        invulnerableTimer = Mathf.Max(invulnerableTimer, seconds);
    }
}
