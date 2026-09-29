using UnityEngine;

/// Da voz ao sistema de ruido: todo Noise.Emit vira um passo audivel,
/// com volume proporcional ao raio.
///
/// Agachado o raio e zero, entao nao emite, entao nao soa.
/// O silencio e a confirmacao sensorial de que voce esta seguro —
/// e e por isso que o jogador entende a mecanica sem ler numero nenhum.
[RequireComponent(typeof(AudioSource))]
public class NoiseAudio : MonoBehaviour
{
    public float volumeAtMaxRadius = 0.55f;
    [Tooltip("Raio que corresponde a volume cheio. Bate com o ruido de correr.")]
    public float referenceRadius = 14f;

    AudioSource src;
    AudioClip[] steps;
    int next;

    void Awake()
    {
        src = GetComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;   // os proprios passos nao vem de uma direcao

        steps = new AudioClip[3];
        for (int i = 0; i < steps.Length; i++)
            steps[i] = ProtoAudio.Footstep("passo_" + i, 0.92f + 0.11f * i, 0.16f, 11 + i * 7);
    }

    void OnEnable() => Noise.Emitted += OnNoise;
    void OnDisable() => Noise.Emitted -= OnNoise;

    void OnNoise(Vector3 position, float radius, NoiseSource source)
    {
        if (source != NoiseSource.Jogador) return;   // isca cai longe; nao e passo seu
        if (radius <= 0.01f) return;

        float v = Mathf.Clamp01(radius / referenceRadius) * volumeAtMaxRadius;
        src.pitch = Random.Range(0.94f, 1.06f);
        src.PlayOneShot(steps[next], v);
        next = (next + 1) % steps.Length;
    }
}
