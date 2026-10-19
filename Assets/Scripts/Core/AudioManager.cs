using UnityEngine;

/// <summary>
/// Dois AudioSources: um para a trilha (com pitch variável) e outro para os efeitos.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Fontes")]
    public AudioSource musicSource;
    public AudioSource sfxSource;

    [Header("Trilhas")]
    public AudioClip phaseMusic;
    public AudioClip gameOverMusic;
    public float pitchStep = 0.1f;

    [Header("Efeitos")]
    public AudioClip coin;
    public AudioClip heart;
    public AudioClip boot;
    public AudioClip slime;
    public AudioClip spikes;
    public AudioClip clock;
    public AudioClip fall;
    public AudioClip checkpoint;
    public AudioClip flag;
    public AudioClip jump;
    public AudioClip denied;
    public AudioClip bird;
    public AudioClip birdHit;

    void Awake()
    {
        Instance = this;
    }

    public void PlayPhaseMusic()
    {
        PlayMusic(phaseMusic);
    }

    public void PlayGameOverMusic()
    {
        PlayMusic(gameOverMusic);
    }

    void PlayMusic(AudioClip clip)
    {
        musicSource.Stop();
        musicSource.clip = clip;
        musicSource.loop = true;
        musicSource.pitch = 1f;
        musicSource.Play();
    }

    /// <summary>Cada bota coletada acelera a trilha em 0,1.</summary>
    public void AddMusicPitch()
    {
        musicSource.pitch += pitchStep;
    }

    public void PlaySfx(AudioClip clip)
    {
        if (clip != null) sfxSource.PlayOneShot(clip);
    }
}
