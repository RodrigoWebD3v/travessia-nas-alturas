/// <summary>
/// Dados que sobrevivem à troca de cena: pontuação total da campanha e ordem das fases.
/// </summary>
public static class GameSession
{
    public static readonly string[] Phases = { "Fase1_JardimSuspenso", "Fase2_TorresNasNuvens" };

    /// <summary>Pontos somados em todas as fases concluídas mais a fase atual.</summary>
    public static int CampaignScore;

    /// <summary>Total da campanha no início da fase atual (restaurado no Game Over).</summary>
    public static int ScoreAtPhaseStart;

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetOnPlay()
    {
        CampaignScore = 0;
        ScoreAtPhaseStart = 0;
    }
}
