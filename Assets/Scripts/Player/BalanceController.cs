using System;
using UnityEngine;

/// <summary>
/// Simulação do equilíbrio sobre a corda (passo fixo, independente da taxa de quadros).
///
/// O Equilibrista segura uma vara longa com pesos nas pontas e a gira em torno dos ombros:
/// A levanta o lado esquerdo da vara, D levanta o lado direito.
///
/// Física usada (simplificada, em unidades de jogo):
///  1. Momento de inércia: a vara soma inércia ao corpo (I = I_corpo + I_vara), então
///     todos os torques produzem rotação mais lenta e o jogador ganha tempo para corrigir.
///  2. Torque de reação: girar a vara num sentido gira o corpo no sentido oposto
///     (ação e reação). Levantar o lado esquerdo (giro horário visto de trás) empurra o corpo
///     para a esquerda. Esse torque só existe enquanto a vara está acelerando.
///  3. Compensação sustentada: com a vara inclinada, o corpo desloca o peso para o lado
///     levantado, gerando uma correção contínua menor.
///
/// aceleração angular = (gravidade·inclinação + compensação·ângulo + reação·aceleração da vara
///                       + perturbação do avanço) / inércia total - amortecimento·velocidade
///
/// Convenção: inclinação positiva = tombando para a direita; ângulo da vara positivo = lado direito para cima.
/// </summary>
public class BalanceController : MonoBehaviour
{
    [Header("Corpo")]
    [Tooltip("Ângulo (graus) a partir do qual o Equilibrista cai.")]
    public float fallAngle = 35f;
    [Tooltip("Torque da gravidade por grau de inclinação (o corpo é um pêndulo invertido).")]
    public float gravityTorque = 1.9f;
    [Tooltip("Momento de inércia do corpo sozinho.")]
    public float bodyInertia = 1f;
    [Tooltip("Amortecimento da velocidade angular (1/s).")]
    public float damping = 1.5f;
    [Tooltip("Inclinação inicial ao subir na corda; alterna de lado a cada tentativa.")]
    public float initialTilt = 2.5f;

    [Header("Vara")]
    [Tooltip("Inércia extra da vara longa com pesos nas pontas. 0 = sem vara (cai bem mais rápido).")]
    public float poleInertia = 1.5f;
    [Tooltip("Ângulo máximo (graus) que cada extremidade da vara sobe ou desce.")]
    public float poleMaxAngle = 40f;
    [Tooltip("Velocidade máxima de giro da vara (fração do ângulo máximo por segundo).")]
    public float poleMaxSpeed = 1.6f;
    [Tooltip("Aceleração do giro da vara: nada de correção instantânea.")]
    public float poleAcceleration = 5f;
    [Tooltip("Torque de reação por unidade de aceleração angular da vara.")]
    public float reactionTorque = 10f;
    [Tooltip("Torque sustentado com a vara totalmente inclinada.")]
    public float holdTorque = 150f;

    [Header("Perturbação do avanço (previsível)")]
    [Tooltip("Avançar rápido amplifica a inclinação existente.")]
    public float advanceInstability = 0.5f;
    [Tooltip("Balanço senoidal por unidade/s de avanço.")]
    public float swayAmplitude = 8f;
    [Tooltip("Ciclos de balanço por metro percorrido.")]
    public float swayPerMeter = 0.22f;

    public float Tilt { get; private set; }
    public float AngularVelocity { get; private set; }
    /// <summary>Giro da vara de -1 (lado esquerdo no alto) a 1 (lado direito no alto).</summary>
    public float PolePosition { get; private set; }
    public float PoleAngle => PolePosition * poleMaxAngle;
    public float TotalInertia => bodyInertia + poleInertia;
    public bool Active { get; private set; }
    /// <summary>Agachado: o centro de massa baixo deixa o equilíbrio parado (sem tombar).</summary>
    public bool Frozen { get; set; }
    public float NormalizedTilt => Tilt / fallAngle;

    public event Action LostBalance;

    static int attempt;
    float poleVelocity;
    float poleInput;
    float advanceSpeed;
    float distance;

    public void Begin()
    {
        attempt++;
        Active = true;
        Tilt = (attempt % 2 == 0 ? 1f : -1f) * initialTilt;
        AngularVelocity = 0f;
        PolePosition = 0f;
        poleVelocity = 0f;
        distance = 0f;
    }

    public void End()
    {
        Active = false;
    }

    public void ResetState()
    {
        Active = false;
        Frozen = false;
        Tilt = 0f;
        AngularVelocity = 0f;
        PolePosition = 0f;
        poleVelocity = 0f;
        poleInput = 0f;
        advanceSpeed = 0f;
    }

    /// <param name="pole">Entrada A/D (-1 levanta a esquerda, 1 levanta a direita).</param>
    /// <param name="advance">Velocidade de avanço na corda (unidades/s, com sinal).</param>
    public void SetInput(float pole, float advance)
    {
        poleInput = Mathf.Clamp(pole, -1f, 1f);
        advanceSpeed = advance;
    }

    /// <summary>Empurrão externo (por exemplo, a bicada de um pássaro), em graus/s.</summary>
    public void Push(float angularVelocity)
    {
        if (Active) AngularVelocity += angularVelocity;
    }

    void FixedUpdate()
    {
        if (!Active || Frozen) return;
        float dt = Time.fixedDeltaTime;

        // Giro da vara com aceleração limitada; sem entrada ela desacelera e fica onde está.
        float previousVelocity = poleVelocity;
        poleVelocity = Mathf.MoveTowards(poleVelocity, poleInput * poleMaxSpeed, poleAcceleration * dt);
        PolePosition += poleVelocity * dt;
        if (Mathf.Abs(PolePosition) > 1f)
        {
            PolePosition = Mathf.Sign(PolePosition);
            poleVelocity = 0f;
        }
        float poleAngularAcceleration = (poleVelocity - previousVelocity) / dt;

        // Perturbação proporcional à velocidade de avanço.
        float speed = Mathf.Abs(advanceSpeed);
        distance += speed * dt;
        float disturbance = advanceInstability * speed * Tilt
                          + swayAmplitude * speed * Mathf.Sin(distance * swayPerMeter * 2f * Mathf.PI);

        // Levantar o lado esquerdo (posição negativa) empurra o corpo para a esquerda (torque negativo).
        float torque = gravityTorque * Tilt
                     + holdTorque * PolePosition
                     + reactionTorque * poleAngularAcceleration
                     + disturbance;

        float angularAcceleration = torque / TotalInertia - damping * AngularVelocity;
        AngularVelocity += angularAcceleration * dt;
        Tilt += AngularVelocity * dt;

        if (Mathf.Abs(Tilt) >= fallAngle)
        {
            Active = false;
            LostBalance?.Invoke();
        }
    }
}
