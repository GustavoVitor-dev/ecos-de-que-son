using UnityEngine;

/// A isca sonora em voo, e o barulho que ela faz ao cair.
///
/// O ponto dela e inverter o sistema de ruido. Ate agora o ruido era so custo:
/// tudo que ele fazia era te entregar. A isca e ruido que voce COLOCA onde quer,
/// pra puxar a patrulha pra longe da rota que voce escolheu.
///
/// Voar tomando tempo importa: o intervalo entre arremessar e cair e o que
/// permite planejar, e e ele que transforma "fazer barulho" em decisao.
public class SoundBait : MonoBehaviour
{
    [Header("Barulho ao cair")]
    public float noiseRadius = 13f;
    public float impactVolume = 0.85f;

    [Header("Voo")]
    public float arcHeight = 3.2f;
    [Tooltip("Segundos por metro de arremesso. Isca longe demora mais a cair.")]
    public float timePerMeter = 0.055f;
    public float minFlight = 0.35f;

    [Header("Depois")]
    public float lifeAfterLanding = 25f;

    public bool Landed { get; private set; }

    Vector3 from, to;
    float flight, t;
    AudioSource src;
    AudioClip impacto;

    void Awake()
    {
        src = GetComponent<AudioSource>();
        if (src == null) src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 1f;                       // 3D: a graca e o guarda ir PRA LA
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 3f;
        src.maxDistance = 34f;

        impacto = ProtoAudio.Footstep("isca_impacto", 0.5f, 0.34f, 91);
    }

    public void Launch(Vector3 origem, Vector3 destino)
    {
        from = origem;
        to = destino;
        flight = Mathf.Max(minFlight, Vector3.Distance(origem, destino) * timePerMeter);
        t = 0f;
        Landed = false;
        transform.position = origem;
    }

    void Update()
    {
        if (Landed) return;

        t += Time.deltaTime / Mathf.Max(0.05f, flight);
        if (t >= 1f) { Land(); return; }

        Vector3 p = Vector3.Lerp(from, to, t);
        p.y += arcHeight * 4f * t * (1f - t);        // parabola simples
        transform.position = p;
        transform.Rotate(220f * Time.deltaTime, 160f * Time.deltaTime, 0f, Space.Self);
    }

    void Land()
    {
        Landed = true;
        transform.position = to;
        transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        Noise.Emit(to, noiseRadius, NoiseSource.Isca);
        if (impacto != null) src.PlayOneShot(impacto, impactVolume);

        Destroy(gameObject, lifeAfterLanding);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, noiseRadius);
    }
}
