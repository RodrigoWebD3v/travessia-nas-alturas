using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Movimento do Equilibrista: livre nas plataformas, preso ao percurso na corda e queda.
/// </summary>
[RequireComponent(typeof(CharacterController), typeof(PlayerStats), typeof(BalanceController))]
public class PlayerController : MonoBehaviour
{
    public enum MoveState { Platform, Rope, Falling, Frozen }

    [Header("Plataforma")]
    public float gravity = -22f;
    public float jumpHeight = 1.3f;
    public float acceleration = 25f;
    public float turnSpeed = 720f;
    public float knockbackForce = 6f;

    [Header("Corda")]
    [Tooltip("Fração da velocidade usada sobre a corda.")]
    public float ropeSpeedFactor = 0.5f;
    public float ropeAcceleration = 4f;
    public float ropeEnterDistance = 0.9f;

    [Header("Queda")]
    public float fallDuration = 1.1f;

    [Header("Agachar (Ctrl)")]
    [Tooltip("Sensor de coleta/colisão; fica mais baixo quando agachado para o pássaro passar por cima.")]
    public CapsuleCollider pickupSensor;
    public float standingSensorHeight = 1.6f;
    public float crouchSensorHeight = 0.95f;

    [Header("Visual")]
    public Transform visualRoot;
    public Transform tiltPivot;
    [Tooltip("Balanço da vara e IK dos braços.")]
    public PoleRig poleRig;
    public float poleReturnSpeed = 3f;

    [Header("Animação")]
    public Animator animator;
    [Tooltip("Velocidade (unidades/s) em que o ciclo de caminhada toca em velocidade normal.")]
    public float walkAnimSpeed = 1.3f;
    [Tooltip("Velocidade na corda em que o ciclo de passos cuidadosos toca em velocidade normal.")]
    public float ropeAnimSpeed = 0.9f;
    public float crossFade = 0.15f;

    public MoveState State { get; private set; } = MoveState.Platform;
    public PlayerStats Stats { get; private set; }
    public BalanceController Balance { get; private set; }
    public bool IsCrouching { get; private set; }
    public RopePath CurrentRope => State == MoveState.Rope ? rope : null;
    public float RopeProgress => ropeT;
    public float RopeSpeed => ropeSpeed;

    CharacterController controller;
    Renderer[] renderers;
    Vector3 planarVelocity;
    float verticalVelocity;
    float facingYaw;

    RopePath rope;
    float ropeT;
    float ropeSpeed;
    float ropeBlend;

    Vector3 fallVelocity;
    float fallTimer;
    float fallTilt;

    string currentAnim;
    float visualPoleAngle;
    float airTime;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        Stats = GetComponent<PlayerStats>();
        Balance = GetComponent<BalanceController>();
        Balance.LostBalance += () => StartFall(true);
        renderers = visualRoot.GetComponentsInChildren<Renderer>();
    }

    void Update()
    {
        UpdateCrouch();
        switch (State)
        {
            case MoveState.Platform: UpdatePlatform(); break;
            case MoveState.Rope: UpdateRope(); break;
            case MoveState.Falling: UpdateFalling(); break;
        }
        UpdateVisual();
        UpdateAnimation();
        UpdatePoleAngle();
    }

    /// <summary>Envia o ângulo da vara ao PoleRig; fora da corda ela volta suavemente à horizontal.</summary>
    void UpdatePoleAngle()
    {
        float target = State == MoveState.Rope ? Balance.PoleAngle : 0f;
        visualPoleAngle = State == MoveState.Rope
            ? target
            : Mathf.Lerp(visualPoleAngle, target, 1f - Mathf.Exp(-poleReturnSpeed * Time.deltaTime));
        // Ângulo positivo levanta o lado direito (giro anti-horário visto de trás).
        poleRig.Angle = visualPoleAngle;
    }

    void UpdateAnimation()
    {
        if (animator == null) return;
        string next = currentAnim;
        float speed = 1f;

        switch (State)
        {
            case MoveState.Platform:
                float planar = new Vector3(planarVelocity.x, 0f, planarVelocity.z).magnitude;
                // isGrounded oscila em degraus; só conta como pulo após um instante no ar.
                airTime = controller.isGrounded ? 0f : airTime + Time.deltaTime;
                if (airTime > 0.12f || verticalVelocity > 0.5f) next = "Jump";
                else if (planar > 0.2f)
                {
                    next = "Walk";
                    speed = Mathf.Clamp(planar / walkAnimSpeed, 0.6f, 2.5f);
                }
                else next = "Idle";
                if (IsCrouching) next = "Crouch";
                break;
            case MoveState.Rope:
                if (Mathf.Abs(ropeSpeed) > 0.05f)
                {
                    next = "RopeWalk";
                    // Recuar toca o ciclo na mesma velocidade; o passo continua cuidadoso.
                    speed = Mathf.Clamp(Mathf.Abs(ropeSpeed) / ropeAnimSpeed, 0.4f, 2.5f);
                }
                else next = "RopeIdle";
                if (IsCrouching)
                {
                    next = "Crouch";
                    speed = 1f;
                }
                break;
            case MoveState.Falling:
                next = "Fall";
                speed = 1.4f;
                break;
            case MoveState.Frozen:
                if (currentAnim == "Walk" || currentAnim == "RopeWalk") next = currentAnim == "Walk" ? "Idle" : "RopeIdle";
                break;
        }

        animator.speed = speed;
        if (next != currentAnim)
        {
            animator.CrossFadeInFixedTime(next, crossFade);
            currentAnim = next;
        }
    }

    /// <summary>
    /// Segurar Ctrl agacha: o sensor fica baixo (o pássaro passa por cima), o personagem não avança
    /// e, na corda, o equilíbrio fica congelado enquanto estiver agachado.
    /// </summary>
    void UpdateCrouch()
    {
        var kb = Keyboard.current;
        bool wants = kb != null && (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed);
        bool canCrouch = State == MoveState.Rope || (State == MoveState.Platform && controller.isGrounded);
        IsCrouching = wants && canCrouch;

        Balance.Frozen = IsCrouching && State == MoveState.Rope;

        if (pickupSensor != null)
        {
            float h = IsCrouching ? crouchSensorHeight : standingSensorHeight;
            pickupSensor.height = h;
            pickupSensor.center = new Vector3(0f, h * 0.5f, 0f);
        }
    }

    /// <summary>Chamado pelo pássaro. Retorna true se o golpe foi aplicado.</summary>
    public bool HitByBird(Vector3 birdDirection, float damage, float push)
    {
        if (IsCrouching || State == MoveState.Falling || State == MoveState.Frozen) return false;
        if (!Stats.TakeDamage(damage)) return false;

        // O pássaro vem de frente: o empurrão aumenta a inclinação que o jogador já tem.
        float side = Mathf.Abs(Balance.Tilt) > 0.5f ? Mathf.Sign(Balance.Tilt) : (Random.value < 0.5f ? -1f : 1f);
        if (State == MoveState.Rope) Balance.Push(side * push);
        else Knockback(transform.position - birdDirection);
        return true;
    }

    static Vector2 ReadMove()
    {
        var kb = Keyboard.current;
        if (kb == null) return Vector2.zero;
        float x = (kb.dKey.isPressed || kb.rightArrowKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed || kb.leftArrowKey.isPressed ? 1f : 0f);
        float y = (kb.wKey.isPressed || kb.upArrowKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed || kb.downArrowKey.isPressed ? 1f : 0f);
        return new Vector2(x, y);
    }

    // ------------------------------------------------------------ plataforma

    void UpdatePlatform()
    {
        float dt = Time.deltaTime;
        Vector2 input = IsCrouching ? Vector2.zero : ReadMove();
        Vector3 direction = Vector3.ClampMagnitude(new Vector3(input.x, 0f, input.y), 1f);

        planarVelocity = Vector3.MoveTowards(planarVelocity, direction * Stats.Speed, acceleration * dt);

        bool grounded = controller.isGrounded;
        if (grounded && verticalVelocity < 0f) verticalVelocity = -2f;

        var kb = Keyboard.current;
        if (grounded && !IsCrouching && kb != null && kb.spaceKey.wasPressedThisFrame)
        {
            verticalVelocity = Mathf.Sqrt(2f * jumpHeight * -gravity);
            AudioManager.Instance.PlaySfx(AudioManager.Instance.jump);
        }

        verticalVelocity += gravity * dt;
        controller.Move((planarVelocity + Vector3.up * verticalVelocity) * dt);

        if (direction.sqrMagnitude > 0.01f)
        {
            float targetYaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            facingYaw = Mathf.MoveTowardsAngle(facingYaw, targetYaw, turnSpeed * dt);
        }

        if (controller.isGrounded && input.y > 0.5f) TryEnterRope();

        if (transform.position.y < GameManager.Instance.killHeight) StartFall(false);
    }

    void TryEnterRope()
    {
        foreach (var candidate in RopePath.All)
        {
            Vector3 offset = transform.position - candidate.StartPoint;
            if (Mathf.Abs(offset.y) > 1.2f) continue;
            offset.y = 0f;
            if (offset.magnitude <= ropeEnterDistance)
            {
                EnterRope(candidate);
                return;
            }
        }
    }

    void EnterRope(RopePath target)
    {
        rope = target;
        ropeT = 0f;
        ropeSpeed = 0f;
        ropeBlend = 0.2f;
        planarVelocity = Vector3.zero;
        verticalVelocity = 0f;
        controller.enabled = false;
        facingYaw = Quaternion.LookRotation(rope.Forward).eulerAngles.y;
        Balance.Begin();
        State = MoveState.Rope;
        GameManager.Instance.OnPlayerEnteredRope();
    }

    // ------------------------------------------------------------ corda

    void UpdateRope()
    {
        float dt = Time.deltaTime;
        // Agachado não avança: serve para desviar, não para atravessar a corda em segurança.
        Vector2 input = IsCrouching ? Vector2.zero : ReadMove();

        float targetSpeed = input.y * Stats.Speed * ropeSpeedFactor;
        ropeSpeed = Mathf.MoveTowards(ropeSpeed, targetSpeed, ropeAcceleration * dt);
        ropeT += ropeSpeed * dt / rope.Length;
        Balance.SetInput(input.x, ropeSpeed);

        if (ropeT < 0f)
        {
            ExitRope(rope.StartPoint - rope.Forward * 0.5f);
            return;
        }
        if (ropeT >= 1f)
        {
            ExitRope(rope.EndPoint + rope.Forward * 0.7f);
            return;
        }

        Vector3 target = rope.Evaluate(ropeT);
        if (ropeBlend > 0f)
        {
            ropeBlend -= dt;
            transform.position = Vector3.Lerp(transform.position, target, 1f - Mathf.Exp(-25f * dt));
        }
        else
        {
            transform.position = target;
        }
    }

    void ExitRope(Vector3 position)
    {
        Balance.ResetState();
        rope = null;
        transform.position = position + Vector3.up * 0.05f;
        controller.enabled = true;
        planarVelocity = Vector3.zero;
        verticalVelocity = 0f;
        State = MoveState.Platform;
    }

    // ------------------------------------------------------------ queda

    void StartFall(bool fromRope)
    {
        if (State == MoveState.Falling || State == MoveState.Frozen) return;

        GameManager.Instance.OnPlayerStartedFalling();
        AudioManager.Instance.PlaySfx(AudioManager.Instance.fall);

        if (fromRope)
        {
            float side = Mathf.Sign(Balance.Tilt);
            Vector3 right = Vector3.Cross(Vector3.up, rope.Forward).normalized;
            fallVelocity = right * side * 2.5f + rope.Forward * ropeSpeed;
            fallTilt = Balance.Tilt;
            verticalVelocity = 1f;
            fallTimer = fallDuration;
        }
        else
        {
            fallVelocity = planarVelocity;
            fallTilt = 0f;
            fallTimer = 0.25f;
        }

        Balance.End();
        controller.enabled = false;
        State = MoveState.Falling;
    }

    void UpdateFalling()
    {
        float dt = Time.deltaTime;
        verticalVelocity += gravity * dt;
        transform.position += (fallVelocity + Vector3.up * verticalVelocity) * dt;
        fallTilt = Mathf.MoveTowards(fallTilt, Mathf.Sign(fallTilt == 0f ? 1f : fallTilt) * 90f, 160f * dt);

        fallTimer -= dt;
        if (fallTimer <= 0f)
        {
            State = MoveState.Frozen;
            GameManager.Instance.OnPlayerFell();
        }
    }

    // ------------------------------------------------------------ chamadas externas

    public void Respawn(Vector3 position)
    {
        Balance.ResetState();
        rope = null;
        controller.enabled = false;
        transform.position = position;
        controller.enabled = true;
        planarVelocity = Vector3.zero;
        verticalVelocity = 0f;
        fallTilt = 0f;
        facingYaw = 0f;
        Stats.GrantInvulnerability(1.5f);
        State = MoveState.Platform;
    }

    public void Freeze()
    {
        if (State == MoveState.Rope) Balance.End();
        if (State != MoveState.Falling) State = MoveState.Frozen;
        else fallTimer = float.MaxValue; // continua caindo, mas sem penalidade
        planarVelocity = Vector3.zero;
    }

    public void Knockback(Vector3 from)
    {
        if (State != MoveState.Platform) return;
        Vector3 away = transform.position - from;
        away.y = 0f;
        if (away.sqrMagnitude < 0.001f) away = -visualRoot.forward;
        planarVelocity = away.normalized * knockbackForce;
        verticalVelocity = 4f;
    }

    // ------------------------------------------------------------ visual

    void UpdateVisual()
    {
        visualRoot.rotation = Quaternion.Euler(0f, facingYaw, 0f);

        float tilt = State == MoveState.Falling ? fallTilt : Balance.Tilt;
        if (State == MoveState.Platform) tilt = 0f;
        // Rotação positiva em Z inclina para a esquerda; o sinal negativo mantém "positivo = direita".
        tiltPivot.localRotation = Quaternion.Euler(0f, 0f, -tilt);

        bool visible = !Stats.IsInvulnerable || State == MoveState.Frozen || Mathf.Repeat(Time.time, 0.2f) > 0.08f;
        foreach (var r in renderers) r.enabled = visible;
    }
}
