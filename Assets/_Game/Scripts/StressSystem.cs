using UnityEngine;

/// O estresse nao e uma barra de vida invertida. Ele reescreve a percepcao.
///
/// E, de proposito, nao e so punicao: na faixa mais alta o jogador ganha um
/// sentido de proximidade que so existe ali. Se estresse alto fosse apenas ruim,
/// o jogador otimizaria pra mante-lo em zero — e o sistema mais caro do jogo
/// nunca dispararia. Assim ele vira recurso administrado, nao barra a evitar.
public class StressSystem : MonoBehaviour
{
    public enum Tier { Calmo, Alerta, Ruptura, Hipervigilancia }

    public static StressSystem Instance { get; private set; }

    [Header("Limiares")]
    public float alertaAt = 30f;
    public float rupturaAt = 60f;
    public float hiperAt = 85f;

    [Header("Ganho por segundo")]
    [Tooltip("Quando um guarda esta te detectando, multiplicado pela deteccao dele.")]
    public float gainDetected = 34f;
    [Tooltip("Por estar perto de uma ameaca, mesmo sem ser visto.")]
    public float gainProximity = 7f;
    public float proximityRadius = 10f;
    [Tooltip("No escuro, com a lanterna apagada. Nao enxergar custa.")]
    public float gainDarkness = 2.2f;
    public float spikeOnCaught = 35f;

    // Teto do ganho AMBIENTE (proximidade e escuro).
    //
    // Sem isto o bloco dos tuneis cravava 100 em 22 s e ficava la o resto do
    // caminho: num corredor de 3 m sempre ha alguem dentro do raio de 10 m, e o
    // ganho por proximidade nunca parava. As quatro faixas deixavam de significar
    // qualquer coisa e a lacuna passava a disparar so por temporizador.
    //
    // A regra que faz sentido: estar perto de uma ameaca te deixa tenso, ser
    // VISTO e o que te quebra. Ambiente empurra ate aqui; dai pra cima so
    // deteccao e picos de roteiro.
    [Header("Teto do ambiente")]
    [Tooltip("Proximidade e escuro nao empurram o estresse acima disto.")]
    public float tetoDeAmbiente = 70f;

    [Header("Alivio")]
    [Tooltip("Por segundo, longe de ameaca. Lento de proposito: a run acumula cicatriz.")]
    public float relief = 4.5f;
    [Tooltip("Segundos de calma antes do alivio comecar.")]
    public float reliefDelay = 2f;

    [Header("Hipervigilancia")]
    [Tooltip("O sentido que so o estresse alto da: pulso que acelera perto da ameaca, atraves de parede.")]
    public float pulseMaxVolume = 0.5f;
    public float pulseRange = 22f;

    /// Disparado quando o estresse da um salto brusco. A lacuna de percepcao assina isto:
    /// gatilho por EVENTO em vez de temporizador e o que torna a causa perceptivel.
    public event System.Action<float> Salto;

    [Tooltip("Aumento minimo, de uma vez, para contar como salto.")]
    public float limiarDeSalto = 12f;

    public float Value { get; private set; }
    public Tier Current { get; private set; }
    public float Normalized => Value * 0.01f;
    /// Distancia ate a ameaca mais proxima neste frame. -1 se nao ha nenhuma.
    public float NearestThreat { get; private set; } = -1f;

    GuardAI[] guards;
    Listener[] listeners;
    Flashlight flashlight;
    Transform player;
    AudioSource pulse;
    float calmTimer;

    void Awake()
    {
        Instance = this;

        // continua de onde o bloco anterior parou
        RunState.AoEntrarNoBloco(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        Value = RunState.Estresse;
    }

    void OnDisable()
    {
        RunState.Estresse = Value;      // guarda ao trocar de cena
    }

    void Start()
    {
        guards = FindObjectsByType<GuardAI>(FindObjectsSortMode.None);
        listeners = FindObjectsByType<Listener>(FindObjectsSortMode.None);
        flashlight = FindFirstObjectByType<Flashlight>();

        var soldier = FindFirstObjectByType<SoldierController>();
        if (soldier != null) player = soldier.transform;

        var go = new GameObject("PulsoHipervigilancia");
        go.transform.SetParent(transform, false);
        pulse = go.AddComponent<AudioSource>();
        pulse.clip = ProtoAudio.Drone("pulso", 4f, 41);
        pulse.loop = true;
        pulse.spatialBlend = 0f;
        pulse.volume = 0f;
        pulse.Play();
    }

    void Update()
    {
        if (player == null) return;

        float detected = 0f;
        NearestThreat = -1f;

        foreach (var g in guards)
        {
            if (g == null) continue;
            detected = Mathf.Max(detected, g.Detection);
            float d = Vector3.Distance(player.position, g.transform.position);
            if (NearestThreat < 0f || d < NearestThreat) NearestThreat = d;
        }
        foreach (var l in listeners)
        {
            if (l == null) continue;
            float d = Vector3.Distance(player.position, l.transform.position);
            if (NearestThreat < 0f || d < NearestThreat) NearestThreat = d;
        }

        float gain = 0f;
        if (detected > 0.01f) gain += gainDetected * detected;

        // ambiente so empurra ate o teto; deteccao continua subindo sem limite
        float ambiente = 0f;
        if (NearestThreat >= 0f && NearestThreat < proximityRadius)
            ambiente += gainProximity * (1f - NearestThreat / proximityRadius);
        if (flashlight != null && !flashlight.IsOn) ambiente += gainDarkness;
        if (Value < tetoDeAmbiente) gain += ambiente;

        if (gain > 0.01f)
        {
            Value += gain * Time.deltaTime;
            calmTimer = 0f;
        }
        else
        {
            calmTimer += Time.deltaTime;
            if (calmTimer >= reliefDelay) Value -= relief * Time.deltaTime;
        }

        Value = Mathf.Clamp(Value, 0f, 100f);
        Current = TierOf(Value);
        RunState.Estresse = Value;

        UpdatePulse();
    }

    void UpdatePulse()
    {
        if (pulse == null) return;

        bool on = Current == Tier.Hipervigilancia && NearestThreat >= 0f && NearestThreat < pulseRange;
        float target = 0f;
        if (on)
        {
            float closeness = 1f - Mathf.Clamp01(NearestThreat / pulseRange);
            target = pulseMaxVolume * closeness;
            pulse.pitch = Mathf.Lerp(0.6f, 1.5f, closeness);
        }

        pulse.volume = Mathf.Lerp(pulse.volume, target, 1f - Mathf.Exp(-3f * Time.deltaTime));
    }

    public Tier TierOf(float v)
    {
        if (v >= hiperAt) return Tier.Hipervigilancia;
        if (v >= rupturaAt) return Tier.Ruptura;
        if (v >= alertaAt) return Tier.Alerta;
        return Tier.Calmo;
    }

    public void Spike(float amount)
    {
        float antes = Value;
        Value = Mathf.Clamp(Value + amount, 0f, 100f);
        calmTimer = 0f;
        RunState.Estresse = Value;

        // salto suficiente vira evento: e o que a lacuna usa como gatilho
        if (Value - antes >= limiarDeSalto) Salto?.Invoke(Value - antes);
    }

    public void ResetStress()
    {
        Value = 0f;
        calmTimer = 0f;
    }
}
