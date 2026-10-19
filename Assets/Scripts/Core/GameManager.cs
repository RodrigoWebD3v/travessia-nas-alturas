using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public enum GameState { Playing, Recovering, PhaseComplete, GameOver, Victory }

/// <summary>
/// Controla a fase: cronômetro, pontos, checkpoints, conclusão, Game Over e reinício.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Fase")]
    public string phaseTitle = "Jardim Suspenso";
    public int pointsGoal = 100;
    public float startTime = 60f;
    public float fallDamage = 25f;
    [Tooltip("Abaixo desta altura o jogador é considerado em queda.")]
    public float killHeight = -8f;
    [Tooltip("Cena carregada ao concluir. Vazio = última fase (vitória).")]
    public string nextScene;
    public float phaseCompleteDelay = 3f;

    [Header("Referências")]
    public PlayerController player;
    public PlayerStats stats;
    public HUDController hud;
    public AudioManager audioManager;

    public GameState State { get; private set; } = GameState.Playing;
    public int PhasePoints { get; private set; }
    public float TimeLeft { get; private set; }
    public Vector3 RespawnPoint { get; private set; }
    public bool IsPlaying => State == GameState.Playing || State == GameState.Recovering;

    Checkpoint currentCheckpoint;
    float phaseCompleteTimer;
    float flagMessageCooldown;
    bool ropeHintShown;
    bool birdHintShown;

    void Awake()
    {
        Instance = this;
        TimeLeft = startTime;
    }

    void Start()
    {
        GameSession.ScoreAtPhaseStart = GameSession.CampaignScore;
        RespawnPoint = player.transform.position;
        audioManager.PlayPhaseMusic();
        hud.ShowMessage($"{phaseTitle}\nColete {pointsGoal} pontos e toque na bandeira!", 4f);
        hud.ShowHint("WASD: mover   •   Espaço: pular   •   Ctrl: abaixar   •   Ande até a corda e segure W", 7f);
    }

    void Update()
    {
        if (flagMessageCooldown > 0f) flagMessageCooldown -= Time.deltaTime;

        switch (State)
        {
            case GameState.Playing:
            case GameState.Recovering:
                TimeLeft -= Time.deltaTime;
                if (TimeLeft <= 0f)
                {
                    TimeLeft = 0f;
                    TriggerGameOver();
                }
                break;

            case GameState.PhaseComplete:
                phaseCompleteTimer -= Time.deltaTime;
                if (phaseCompleteTimer <= 0f) SceneManager.LoadScene(nextScene);
                break;

            case GameState.GameOver:
            case GameState.Victory:
                var kb = Keyboard.current;
                if (kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
                    Restart();
                break;
        }
    }

    // ------------------------------------------------------------ pontos e tempo

    public void AddPoints(int amount)
    {
        if (!IsPlaying) return;
        PhasePoints += amount;
        GameSession.CampaignScore += amount;
    }

    public void AddTime(float seconds)
    {
        if (IsPlaying) TimeLeft += seconds;
    }

    // ------------------------------------------------------------ checkpoints e quedas

    public void SetCheckpoint(Checkpoint checkpoint)
    {
        if (checkpoint == currentCheckpoint || !IsPlaying) return;
        if (currentCheckpoint != null) currentCheckpoint.SetActivated(false);
        currentCheckpoint = checkpoint;
        checkpoint.SetActivated(true);
        RespawnPoint = checkpoint.SpawnPosition;
        audioManager.PlaySfx(audioManager.checkpoint);
        hud.ShowToast("Checkpoint!", new Color(0.4f, 1f, 0.5f));
    }

    public void OnPlayerStartedFalling()
    {
        if (State == GameState.Playing) State = GameState.Recovering;
    }

    /// <summary>Chamado quando a animação de queda termina: aplica dano e volta ao checkpoint.</summary>
    public void OnPlayerFell()
    {
        if (!IsPlaying) return;
        stats.TakeDamage(fallDamage, ignoreInvulnerability: true);
        if (stats.Health <= 0f) return; // TakeDamage já acionou o Game Over
        player.Respawn(RespawnPoint);
        State = GameState.Playing;
        hud.ShowToast($"-{fallDamage:0} vida", new Color(1f, 0.45f, 0.45f));
    }

    public void OnPlayerEnteredRope()
    {
        if (ropeHintShown) return;
        ropeHintShown = true;
        hud.ShowHint("Na corda:  W/S avançar/recuar   •   A/D levantar o lado esquerdo/direito da vara   •   Ctrl abaixar", 6f);
    }

    public void OnBirdSpawned()
    {
        if (birdHintShown) return;
        birdHintShown = true;
        hud.ShowHint("Pássaro!  Segure Ctrl para se abaixar e deixar ele passar por cima", 5f);
    }

    // ------------------------------------------------------------ fim de fase

    public void ReachFlag()
    {
        if (!IsPlaying) return;

        if (PhasePoints < pointsGoal)
        {
            if (flagMessageCooldown > 0f) return;
            flagMessageCooldown = 2f;
            int missingCoins = Mathf.CeilToInt((pointsGoal - PhasePoints) / 10f);
            hud.ShowMessage($"Faltam {missingCoins} moeda{(missingCoins > 1 ? "s" : "")}!", 2f);
            audioManager.PlaySfx(audioManager.denied);
            return;
        }

        player.Freeze();
        audioManager.PlaySfx(audioManager.flag);

        if (string.IsNullOrEmpty(nextScene))
        {
            State = GameState.Victory;
            hud.ShowVictory(GameSession.CampaignScore, TimeLeft);
        }
        else
        {
            State = GameState.PhaseComplete;
            phaseCompleteTimer = phaseCompleteDelay;
            hud.ShowMessage($"Fase concluída!\nTempo restante: {TimeLeft:0} s", phaseCompleteDelay);
        }
    }

    public void TriggerGameOver()
    {
        if (State == GameState.GameOver || State == GameState.Victory) return;
        State = GameState.GameOver;
        player.Freeze();
        hud.ShowGameOver();
        audioManager.PlayGameOverMusic();
    }

    void Restart()
    {
        if (State == GameState.Victory)
        {
            GameSession.CampaignScore = 0;
            SceneManager.LoadScene(GameSession.Phases[0]);
        }
        else
        {
            // Game Over: reinicia a fase atual e descarta os pontos feitos nela.
            GameSession.CampaignScore = GameSession.ScoreAtPhaseStart;
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }
}
