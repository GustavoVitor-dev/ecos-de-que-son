using UnityEngine;

/// Arquetipo B: o ouvinte. Nao anda, nao enxerga. So escuta — e chama.
///
/// O contraste com o patrulheiro e o que tira o stealth da temporizacao pura:
/// um tem olhos e pernas, o outro tem ouvido e voz.
/// Do patrulheiro voce se esconde. Por este, voce agacha pra passar.
/// Duas regras opostas e legiveis, que o jogador aprende sem tutorial.
public class Listener : MonoBehaviour, INoiseListener
{
    [Header("Audicao")]
    [Tooltip("Multiplica o raio do ruido recebido. Ouve bem melhor que o patrulheiro.")]
    public float hearingSensitivity = 1.6f;
    public float alarmDuration = 3f;
    [Tooltip("Tempo minimo entre dois chamados, pra nao virar sirene continua.")]
    public float alertCooldown = 4f;
    [Tooltip("Até onde a voz dele chega. No corredor antigo chamava o mapa inteiro; no mapa largo isso puxava os 10 guardas pro mesmo ponto.")]
    public float alcanceDoChamado = 22f;

    [Header("Sinalizacao")]
    public Light indicator;
    public Color calmColor = new Color(0.55f, 0.70f, 1f);
    public Color alarmColor = new Color(1f, 0.25f, 0.20f);

    public bool Alarmed => alarmTimer > 0f;

    float alarmTimer;
    float cooldown;
    AudioSource src;
    AudioClip call;

    void Awake()
    {
        src = GetComponent<AudioSource>();
        if (src != null)
        {
            src.playOnAwake = false;
            src.spatialBlend = 1f;
            src.rolloffMode = AudioRolloffMode.Linear;
            src.minDistance = 3f;
            src.maxDistance = 32f;
        }
        call = ProtoAudio.Grunt("chamado", 190f, 0.5f, 19);

        if (indicator == null) indicator = GetComponentInChildren<Light>();
    }

    void OnEnable() => Noise.Listeners.Add(this);
    void OnDisable() => Noise.Listeners.Remove(this);

    void Update()
    {
        if (alarmTimer > 0f) alarmTimer -= Time.deltaTime;
        if (cooldown > 0f) cooldown -= Time.deltaTime;

        if (indicator != null)
            indicator.color = Color.Lerp(indicator.color,
                Alarmed ? alarmColor : calmColor, 1f - Mathf.Exp(-6f * Time.deltaTime));
    }

    public void OnNoise(Vector3 position, float radius)
    {
        if (cooldown > 0f) return;
        if (Vector3.Distance(transform.position, position) > radius * hearingSensitivity) return;

        alarmTimer = alarmDuration;
        cooldown = alertCooldown;

        if (src != null && call != null) src.PlayOneShot(call, 0.9f);

        // a voz dele e a arma: redireciona os patrulheiros que OUVEM o chamado
        foreach (var g in FindObjectsByType<GuardAI>(FindObjectsSortMode.None))
            if (Vector3.Distance(g.transform.position, transform.position) <= alcanceDoChamado)
                g.AlertTo(position);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.6f);
        Gizmos.DrawWireSphere(transform.position, 6f * hearingSensitivity);   // raio de "andando"
        Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, 14f * hearingSensitivity);  // raio de "correndo"
    }
}
