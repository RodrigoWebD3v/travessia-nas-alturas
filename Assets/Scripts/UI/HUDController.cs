using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Interface: pontos, tempo, vida (Slider), velocidade, indicador de equilíbrio,
/// mensagens e telas de Game Over e vitória.
/// </summary>
public class HUDController : MonoBehaviour
{
    [Header("Painel")]
    public Text pointsText;
    public Text totalText;
    public Text timeText;
    public Text speedText;
    public Slider healthSlider;
    public Text healthText;
    public Image healthFill;

    [Header("Equilíbrio")]
    public GameObject balanceRoot;
    public RectTransform balanceBar;
    public RectTransform tiltMarker;
    public Image tiltMarkerImage;
    public RectTransform poleMarker;

    [Header("Mensagens")]
    public Text messageText;
    public Text toastText;
    public Text hintText;
    public Text birdWarning;

    [Header("Telas finais")]
    public GameObject gameOverPanel;
    public GameObject victoryPanel;
    public Text victoryText;

    float messageTimer;
    float toastTimer;
    float hintTimer;
    float birdWarningTimer;
    RectTransform toastRect;
    Vector2 toastBase;

    void Awake()
    {
        gameOverPanel.SetActive(false);
        victoryPanel.SetActive(false);
        messageText.text = "";
        toastText.text = "";
        hintText.text = "";
        birdWarning.enabled = false;
        toastRect = toastText.rectTransform;
        toastBase = toastRect.anchoredPosition;
    }

    void Update()
    {
        var game = GameManager.Instance;
        if (game == null) return;
        var stats = game.stats;
        var player = game.player;

        pointsText.text = $"Pontos: {game.PhasePoints} / {game.pointsGoal}";
        totalText.text = $"Total: {GameSession.CampaignScore}";
        timeText.text = Mathf.CeilToInt(game.TimeLeft).ToString();
        timeText.color = game.TimeLeft <= 10f && Mathf.Repeat(Time.time, 0.6f) < 0.3f ? new Color(1f, 0.35f, 0.35f) : Color.white;
        speedText.text = $"Velocidade: {stats.Speed:0.0}";

        healthSlider.maxValue = stats.maxHealth;
        healthSlider.value = stats.Health;
        healthText.text = $"{stats.Health:0}";
        float h = stats.Health / stats.maxHealth;
        healthFill.color = Color.Lerp(new Color(0.9f, 0.25f, 0.25f), new Color(0.35f, 0.85f, 0.35f), h);

        bool onRope = player.State == PlayerController.MoveState.Rope;
        balanceRoot.SetActive(onRope);
        if (onRope)
        {
            float half = balanceBar.rect.width * 0.5f;
            float n = Mathf.Clamp(player.Balance.NormalizedTilt, -1f, 1f);
            tiltMarker.anchoredPosition = new Vector2(n * half, 0f);
            tiltMarkerImage.color = Color.Lerp(Color.white, new Color(1f, 0.3f, 0.3f), Mathf.Abs(n));
            poleMarker.localRotation = Quaternion.Euler(0f, 0f, player.Balance.PoleAngle);
        }

        if (birdWarningTimer > 0f)
        {
            birdWarningTimer -= Time.deltaTime;
            // pisca enquanto o pássaro se aproxima
            var c = birdWarning.color;
            c.a = Mathf.Repeat(Time.time, 0.3f) < 0.18f ? 1f : 0.35f;
            birdWarning.color = c;
            if (birdWarningTimer <= 0f) birdWarning.enabled = false;
        }

        Tick(ref messageTimer, messageText);
        Tick(ref hintTimer, hintText);
        if (toastTimer > 0f)
        {
            toastTimer -= Time.deltaTime;
            toastRect.anchoredPosition = toastBase + Vector2.up * (1.2f - toastTimer) * 40f;
            var c = toastText.color;
            c.a = Mathf.Clamp01(toastTimer / 0.4f);
            toastText.color = c;
            if (toastTimer <= 0f) toastText.text = "";
        }
    }

    static void Tick(ref float timer, Text text)
    {
        if (timer <= 0f) return;
        timer -= Time.deltaTime;
        if (timer <= 0f) text.text = "";
    }

    public void ShowMessage(string message, float seconds)
    {
        messageText.text = message;
        messageTimer = seconds;
    }

    public void ShowHint(string message, float seconds)
    {
        hintText.text = message;
        hintTimer = seconds;
    }

    /// <summary>Aviso piscante de pássaro vindo de frente.</summary>
    public void ShowBirdWarning(float seconds)
    {
        birdWarning.enabled = true;
        birdWarningTimer = seconds;
    }

    public void ShowToast(string message, Color color)
    {
        toastText.text = message;
        color.a = 1f;
        toastText.color = color;
        toastTimer = 1.2f;
    }

    public void ShowGameOver()
    {
        messageText.text = "";
        hintText.text = "";
        balanceRoot.SetActive(false);
        birdWarning.enabled = false;
        gameOverPanel.SetActive(true);
    }

    public void ShowVictory(int totalScore, float timeLeft)
    {
        messageText.text = "";
        hintText.text = "";
        victoryText.text = $"Pontuação total: {totalScore}\nTempo restante: {timeLeft:0} s";
        victoryPanel.SetActive(true);
    }
}
