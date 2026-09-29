using System.Collections;
using UnityEngine;

/// Base de ruido ambiente.
/// Existe tanto pra estar quanto pra poder ser TIRADA: num jogo cujo sistema
/// central e som, silencio subito e a ferramenta de susto mais barata que existe.
/// Hush() ja deixa esse gancho pronto pra quando a entidade aparecer.
[RequireComponent(typeof(AudioSource))]
public class Ambience : MonoBehaviour
{
    public float volume = 0.35f;

    AudioSource src;

    void Awake()
    {
        src = GetComponent<AudioSource>();
        src.clip = ProtoAudio.Drone("ambiente");
        src.loop = true;
        src.spatialBlend = 0f;
        src.volume = volume;
        src.playOnAwake = false;
    }

    void Start() => src.Play();

    /// Corta o ambiente por N segundos e devolve devagar.
    public void Hush(float seconds)
    {
        StopAllCoroutines();
        StartCoroutine(HushRoutine(seconds));
    }

    IEnumerator HushRoutine(float seconds)
    {
        float t = 0f;
        while (t < 0.25f) { t += Time.deltaTime; src.volume = Mathf.Lerp(volume, 0f, t / 0.25f); yield return null; }
        src.volume = 0f;

        yield return new WaitForSeconds(seconds);

        t = 0f;
        while (t < 1.5f) { t += Time.deltaTime; src.volume = Mathf.Lerp(0f, volume, t / 1.5f); yield return null; }
        src.volume = volume;
    }
}
