using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// A entidade.
///
/// Regra unica, e ela vale por doze minutos antes de ser quebrada uma vez:
/// a entidade SO muda de estado durante uma lacuna de percepcao.
/// O jogador nunca a ve surgir nem sumir. Ela simplesmente passa a estar,
/// ou passa a nao estar, e ele nao consegue apontar o momento.
///
/// As aparicoes escalam em AGENCIA, nao em visibilidade:
/// percebida -> confirmada -> contextualizada. Mostrar mais nao assusta mais;
/// o que assusta e ela fazer mais.
public class EntityPresence : MonoBehaviour
{
    public enum Manifestation
    {
        /// 1 — na borda do quadro, some antes de voce ter certeza. Corte duplo.
        Periferica,
        /// 2 — silhueta legivel a distancia fixa. A primeira vez que da pra afirmar que viu.
        Confirmada,
        /// 3 — parada ENTRE os inimigos, no posto de uma sentinela. Nao age.
        EntreEles
    }

    [Header("Partes")]
    public GameObject body;
    public Light aura;

    [Header("Tempo visivel por aparicao")]
    public float visiblePeriferica = 0.25f;
    public float visibleConfirmada = 1.2f;
    public float visibleEntreEles = 2.5f;

    [Header("Posicionamento")]
    public float distanciaConfirmada = 9f;
    public float offsetEntreEles = 3f;
    [Tooltip("Distancia minima do jogador. Perto demais nao e aparicao, e susto barato.")]
    public float distanciaMinima = 3.5f;
    [Tooltip("Quanto o pivo dela fica acima do pe. Medido do proprio corpo no Start.")]
    public float alturaDoPiso = 1.3f;
    public float raioDoCorpo = 0.45f;
    public float alturaDoCorpo = 1.9f;
    [Tooltip("Quantas direcoes testar antes de desistir de aparecer.")]
    public int tentativas = 48;

    [Header("Aura por aparicao")]
    public float auraPeriferica = 0.5f;
    public float auraConfirmada = 1.3f;
    public float auraEntreEles = 1.0f;

    public bool Visible { get; private set; }
    public Manifestation Last { get; private set; }

    BlinkSystem blink;
    CameraRig rig;
    Transform player;
    AudioSource src;
    AudioClip somPeriferica, somConfirmada, somEntreEles;
    System.Action pending;

    void Start()
    {
        blink = FindFirstObjectByType<BlinkSystem>();
        rig = FindFirstObjectByType<CameraRig>();

        var soldier = FindFirstObjectByType<SoldierController>();
        if (soldier != null) player = soldier.transform;

        src = GetComponent<AudioSource>();
        if (src == null) src = gameObject.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 1f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 3f;
        src.maxDistance = 30f;

        // placeholders: ver ent_presence_01, ent_step_wrong e ent_breath em Docs/AUDIO.md
        somPeriferica = ProtoAudio.Drone("ent_presenca", 2f, 61);
        somConfirmada = ProtoAudio.Footstep("ent_passo_errado", 0.34f, 0.42f, 67);
        somEntreEles = ProtoAudio.Grunt("ent_respiracao", 52f, 1.1f, 71);

        MedirAltura();

        if (blink != null) blink.Blinked += OnGap;
        SetVisible(false);
    }

    // quanto o pivo esta acima do pe dela, na pose em que foi autorada.
    // sem isso ela pousa enterrada no chao ou flutuando meio metro.
    //
    // Media pelo primeiro Renderer que encontrasse, e o primeiro era a capsula
    // de prototipo que ficou na hierarquia com o renderer desligado: 1,3 m de
    // pivo central. Quando o corpo virou malha de gente, com pivo no PE, ela
    // passou a pousar 1,3 m no ar. Agora mede pelo osso do pe quando existe, e
    // so cai pra bounds da malha visivel se nao houver esqueleto.
    void MedirAltura()
    {
        if (body == null) return;

        var anim = body.GetComponentInChildren<Animator>();
        if (anim != null && anim.avatar != null && anim.avatar.isHuman)
        {
            var pe = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
            if (pe != null)
            {
                alturaDoPiso = Mathf.Clamp(transform.position.y - pe.position.y, 0f, 5f);
                return;
            }
        }

        float baixo = float.MaxValue;
        foreach (var r in body.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;                 // capsula de prototipo desligada nao conta
            if (r.bounds.min.y < baixo) baixo = r.bounds.min.y;
        }
        if (baixo == float.MaxValue) return;

        alturaDoPiso = Mathf.Clamp(transform.position.y - baixo, 0f, 5f);
    }

    void OnDestroy()
    {
        if (blink != null) blink.Blinked -= OnGap;
    }

    void OnGap()
    {
        var p = pending;
        pending = null;
        p?.Invoke();
    }

    /// Agenda uma aparicao. Ela so vai acontecer dentro de uma lacuna.
    public void Manifest(Manifestation kind) => Manifest(kind, null);

    /// Com ancora, o beat manda ONDE. Serve pras aparicoes de roteiro, em que
    /// deixar o sorteio decidir seria abrir mao do enquadramento no unico
    /// momento em que ele importa. A ancora ainda passa pela validacao: se o
    /// lugar autorado estiver bloqueado, o sorteio assume.
    public void Manifest(Manifestation kind, Transform ancora)
    {
        if (Visible) return;
        this.ancora = ancora;
        StopAllCoroutines();
        StartCoroutine(Sequence(kind));
    }

    Transform ancora;

    IEnumerator Sequence(Manifestation kind)
    {
        Last = kind;

        // o lugar e escolhido NO instante do corte, nao ao agendar: entre uma coisa
        // e outra o jogador andou, e meio metro decide entre o corredor e a pedra.
        pending = () =>
        {
            if (EscolherPosicao(kind, out Vector3 pos)) Appear(pos, kind);
            else Debug.Log($"[ENTIDADE] {kind} cancelada — nenhum lugar valido em volta do jogador. " +
                           "Melhor nao aparecer do que aparecer dentro da parede.");
        };
        if (blink != null) blink.ForceBlink();

        yield return new WaitForSeconds(VisibleTimeFor(kind) + 0.2f);

        pending = () => SetVisible(false);
        if (blink != null) blink.ForceBlink();
    }

    void Appear(Vector3 pos, Manifestation kind)
    {
        transform.position = pos;

        if (player != null)
        {
            Vector3 look = player.position - pos;
            look.y = 0f;
            if (look.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(look);
        }

        if (aura != null) aura.intensity = AuraFor(kind);
        SetVisible(true);

        AudioClip clip = kind == Manifestation.Periferica ? somPeriferica
                       : kind == Manifestation.Confirmada ? somConfirmada
                       : somEntreEles;
        if (clip != null) src.PlayOneShot(clip, kind == Manifestation.Periferica ? 0.45f : 0.8f);
    }

    void SetVisible(bool v)
    {
        Visible = v;
        if (body != null) body.SetActive(v);
        if (aura != null) aura.enabled = v;
    }

    float VisibleTimeFor(Manifestation k) =>
        k == Manifestation.Periferica ? visiblePeriferica
        : k == Manifestation.Confirmada ? visibleConfirmada
        : visibleEntreEles;

    float AuraFor(Manifestation k) =>
        k == Manifestation.Periferica ? auraPeriferica
        : k == Manifestation.Confirmada ? auraConfirmada
        : auraEntreEles;

    // A versao antiga escolhia um ponto num circulo matematico em volta do jogador
    // e teleportava a entidade pra la, sem perguntar o que havia naquele lugar.
    // Num corredor de 3 m de largura, um circulo de 9 m cai dentro da rocha quase
    // sempre — e foi exatamente isso que aconteceu no playtest. Agora ela so aparece
    // onde CABE em pe, no chao, e onde o jogador consegue de fato ver.
    bool EscolherPosicao(Manifestation kind, out Vector3 pos)
    {
        pos = transform.position;
        if (player == null) return false;

        var candidatos = Candidatos(kind);
        if (ancora != null) candidatos.Insert(0, ancora.position);

        // primeiro os que estao na linha de visao: entidade atras de parede nao e aparicao
        foreach (var bruto in candidatos)
            if (Assentar(bruto, out Vector3 p) && Longe(p) && NaLinhaDeVisao(p)) { pos = p; return true; }

        // se nada estiver visivel, ao menos garante que nao e dentro de geometria
        foreach (var bruto in candidatos)
            if (Assentar(bruto, out Vector3 p) && Longe(p)) { pos = p; return true; }

        return false;
    }

    List<Vector3> Candidatos(Manifestation kind)
    {
        var lista = new List<Vector3>();

        if (kind == Manifestation.EntreEles)
            foreach (var g in FindObjectsByType<GuardAI>(FindObjectsSortMode.None))
            {
                Vector3 dir = (player.position - g.transform.position).normalized;
                Vector3 lado = Vector3.Cross(Vector3.up, dir);
                lista.Add(g.transform.position + lado * offsetEntreEles);
                lista.Add(g.transform.position - lado * offsetEntreEles);
            }

        if (kind == Manifestation.Periferica)
            for (int i = 0; i < tentativas; i++) lista.Add(EdgeOfFrame());

        // anel em volta do jogador: fonte principal da Confirmada e reserva das
        // outras duas quando o lugar ideal nao existe no mapa
        float alvo = kind == Manifestation.Confirmada ? distanciaConfirmada : distanciaConfirmada * 0.8f;
        float giro = Random.Range(0f, Mathf.PI * 2f);
        for (int i = 0; i < tentativas; i++)
        {
            float a = giro + i * (Mathf.PI * 2f / tentativas);
            Vector3 u = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            for (float d = alvo; d >= distanciaMinima; d -= 1.5f)
                lista.Add(player.position + u * d);
        }

        return lista;
    }

    // pousa o candidato no chao e confirma que um corpo cabe em pe ali.
    //
    // O raio comeca meio metro acima do candidato, nao la em cima: os candidatos
    // nascem na altura do jogador, e um raio disparado de 8 m atravessaria o teto
    // do tunel de fora pra dentro e chamaria a LAJE de chao — ela apareceria em pe
    // sobre o tunel, do lado de fora do mapa. Mesmo bug, outro disfarce.
    bool Assentar(Vector3 bruto, out Vector3 p)
    {
        p = bruto;

        if (!Physics.Raycast(bruto + Vector3.up * 0.5f, Vector3.down,
                             out RaycastHit chao, 6f, ~0, QueryTriggerInteraction.Ignore))
            return false;

        Vector3 pe = chao.point;
        if (Physics.CheckCapsule(pe + Vector3.up * (raioDoCorpo + 0.1f),
                                 pe + Vector3.up * (alturaDoCorpo - raioDoCorpo),
                                 raioDoCorpo, ~0, QueryTriggerInteraction.Ignore))
            return false;

        p = pe + Vector3.up * alturaDoPiso;
        return true;
    }

    bool Longe(Vector3 p)
    {
        Vector3 v = p - player.position;
        v.y = 0f;
        return v.magnitude >= distanciaMinima;
    }

    bool NaLinhaDeVisao(Vector3 p)
    {
        Vector3 olho = player.position + Vector3.up * 0.6f;
        Vector3 peito = new Vector3(p.x, p.y + 0.2f, p.z);
        return !Physics.Linecast(olho, peito, ~0, QueryTriggerInteraction.Ignore);
    }

    /// Borda do enquadramento: em top-down nao existe "atras de voce",
    /// entao o limite da percepcao e a moldura da tela.
    Vector3 EdgeOfFrame()
    {
        Camera cam = rig != null && rig.topDownCamera != null ? rig.topDownCamera : Camera.main;
        if (cam == null) return player.position + Vector3.forward * 12f;

        float vx, vy;
        if (Random.value < 0.5f) { vx = Random.value < 0.5f ? 0.06f : 0.94f; vy = Random.Range(0.15f, 0.85f); }
        else { vx = Random.Range(0.15f, 0.85f); vy = Random.value < 0.5f ? 0.10f : 0.90f; }

        Ray r = cam.ViewportPointToRay(new Vector3(vx, vy, 0f));
        Plane ground = new Plane(Vector3.up, new Vector3(0f, player.position.y - 1f, 0f));
        if (ground.Raycast(r, out float d)) return r.GetPoint(d);

        return player.position + Vector3.forward * 12f;
    }
}
