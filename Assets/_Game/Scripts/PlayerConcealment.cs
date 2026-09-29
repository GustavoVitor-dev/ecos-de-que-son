using System.Collections.Generic;
using UnityEngine;

/// Quanto o jogador esta oculto neste instante.
///
/// A regra que faz a vegetacao ser interessante em vez de so um buff:
/// ela esconde quem fica quieto e denuncia quem corre. Parado e agachado no mato
/// voce quase some; atravessando correndo, o mato balanca, faz barulho, e o
/// beneficio de ocultacao praticamente desaparece.
///
/// Assim o jogador negocia velocidade contra invisibilidade dentro do mesmo lugar,
/// em vez de simplesmente entrar num esconderijo e ficar seguro.
public class PlayerConcealment : MonoBehaviour
{
    [Header("Eficacia por estado")]
    [Tooltip("Multiplicador aplicado sobre a forca do volume quando parado.")]
    [Range(0f, 1f)] public float stillFactor = 0f;
    [Tooltip("Andando: a ocultacao ja perde boa parte do efeito.")]
    [Range(0f, 1f)] public float walkFactor = 0.55f;
    [Tooltip("Correndo: praticamente nao esconde mais nada.")]
    [Range(0f, 1f)] public float runFactor = 0.9f;

    [Header("Ruido de vegetacao")]
    public float rustleInterval = 0.45f;

    /// 1 = totalmente visivel. Valores menores encurtam o alcance de visao do guarda.
    public float VisibilityMultiplier { get; private set; } = 1f;
    public bool InConcealment => volumes.Count > 0;
    /// A moita em que ele está agora (a mais densa), ou null.
    public Moita MoitaAtual { get; private set; }

    readonly List<ConcealmentVolume> volumes = new List<ConcealmentVolume>();
    SoldierController soldier;
    CharacterController cc;
    float rustleTimer;
    float recheque;

    void Awake()
    {
        soldier = GetComponent<SoldierController>();
        cc = GetComponent<CharacterController>();
    }

    void OnTriggerEnter(Collider other)
    {
        var v = other.GetComponent<ConcealmentVolume>();
        if (v != null && !volumes.Contains(v)) volumes.Add(v);
    }

    void OnTriggerExit(Collider other)
    {
        var v = other.GetComponent<ConcealmentVolume>();
        if (v != null) volumes.Remove(v);
    }

    void Update()
    {
        // Teleporte (renascer depois de morrer) não dispara OnTriggerExit: o
        // volume ficava na lista pra sempre e o jogador renascia com o mato
        // grudado na tela. Confere a posição de verdade todo quadro.
        volumes.RemoveAll(v => v == null || !v.isActiveAndEnabled || !Dentro(v));

        // Teleporte (renascer, ou a historia te colocando em algum lugar)
        // tambem nao dispara OnTriggerEnter: sem isto, renascer DENTRO de uma
        // moita deixava o jogador visivel no meio do mato. Confere por posicao
        // quatro vezes por segundo.
        recheque -= Time.deltaTime;
        if (recheque <= 0f)
        {
            recheque = 0.25f;
            float raio = cc != null ? cc.radius : 0.4f;
            foreach (var c in Physics.OverlapSphere(transform.position, raio, ~0, QueryTriggerInteraction.Collide))
            {
                var v = c.GetComponent<ConcealmentVolume>();
                if (v != null && v.isActiveAndEnabled && !volumes.Contains(v)) volumes.Add(v);
            }
        }
        MoitaAtual = null;
        float maisDenso = 2f;
        foreach (var v in volumes)
            if (v.strength < maisDenso) { maisDenso = v.strength; MoitaAtual = v.GetComponentInParent<Moita>(); }

        if (volumes.Count == 0)
        {
            VisibilityMultiplier = 1f;
            rustleTimer = 0f;
            return;
        }

        // vale o volume mais denso em que o jogador estiver
        float best = 1f;
        ConcealmentVolume bestVol = null;
        foreach (var v in volumes)
            if (v.strength < best) { best = v.strength; bestVol = v; }

        float speed = cc != null ? new Vector2(cc.velocity.x, cc.velocity.z).magnitude : 0f;
        bool moving = speed > 0.15f;
        bool running = speed > 4f;

        // parado esconde de verdade; correndo o mato deixa de servir
        float lost = !moving ? stillFactor : (running ? runFactor : walkFactor);
        VisibilityMultiplier = Mathf.Lerp(best, 1f, lost);

        // agachado não farfalha: é a regra do Ouvinte ("por este, você agacha
        // pra passar"). Sem isto, chegar no sargento — do lado de uma moita e
        // de um Ouvinte — era ser pego em ciclo a cada 9 s.
        bool agachado = soldier != null && soldier.IsCrouching;
        if (moving && !agachado && bestVol != null) Rustle(bestVol, running);
        else rustleTimer = 0f;
    }

    bool Dentro(ConcealmentVolume v)
    {
        var c = v.GetComponent<Collider>();
        if (c == null || !c.enabled) return false;
        var b = c.bounds;
        b.Expand(cc != null ? cc.radius * 2f + 0.2f : 0.8f);
        return b.Contains(transform.position);
    }

    /// Esquece todo mato: usado ao renascer.
    public void Limpar()
    {
        volumes.Clear();
        VisibilityMultiplier = 1f;
        rustleTimer = 0f;
    }

    void Rustle(ConcealmentVolume v, bool running)
    {
        rustleTimer -= Time.deltaTime;
        if (rustleTimer > 0f) return;

        rustleTimer = rustleInterval * (running ? 0.65f : 1f);
        Noise.Emit(transform.position, v.rustleRadius * (running ? 1.6f : 1f), NoiseSource.Jogador);
    }
}
