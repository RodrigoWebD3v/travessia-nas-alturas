using UnityEngine;

/// <summary>
/// Enquanto o Equilibrista está numa das cordas listadas, solta pássaros de tempos em tempos.
/// Cada pássaro vem de frente, pelo eixo da corda, e é anunciado por um piado e um aviso na HUD.
/// </summary>
public class BirdSpawner : MonoBehaviour
{
    public BirdHazard birdPrefab;
    public RopePath[] ropes;

    [Header("Ritmo")]
    public float firstDelay = 1.5f;
    public float minInterval = 5f;
    public float maxInterval = 7f;

    [Header("Voo")]
    public float birdSpeed = 6f;
    [Tooltip("Distância à frente do jogador de onde o pássaro parte (define o tempo de aviso).")]
    public float spawnDistance = 12f;
    [Tooltip("Altura do voo acima da corda: acerta o peito em pé e passa por cima agachado.")]
    public float flightHeight = 1.5f;

    float timer;

    void Update()
    {
        var game = GameManager.Instance;
        if (game == null || !game.IsPlaying) return;

        var player = game.player;
        var rope = player.CurrentRope;
        if (rope == null || System.Array.IndexOf(ropes, rope) < 0)
        {
            timer = firstDelay;
            return;
        }

        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = Random.Range(minInterval, maxInterval);
        Spawn(game, player, rope);
    }

    /// <summary>
    /// O pássaro nasce à frente, sobre o eixo da corda, e voa reto em direção ao jogador (de frente para ele).
    /// </summary>
    void Spawn(GameManager game, PlayerController player, RopePath rope)
    {
        Vector3 target = player.transform.position + Vector3.up * flightHeight;
        Vector3 start = target + rope.Forward * spawnDistance;
        // acompanha a inclinação da corda (cordas que sobem ou descem)
        start.y += (rope.EndPoint.y - rope.StartPoint.y) / Mathf.Max(rope.Length, 0.01f) * spawnDistance;

        var bird = Instantiate(birdPrefab);
        bird.Launch(start, target - start, birdSpeed);

        var audio = AudioManager.Instance;
        audio.PlaySfx(audio.bird);
        // tempo até chegar: o pássaro e o jogador se aproximam
        float closingSpeed = birdSpeed + Mathf.Max(0f, player.RopeSpeed);
        game.hud.ShowBirdWarning(spawnDistance / closingSpeed * 0.85f);
        game.OnBirdSpawned();
    }
}
