using UnityEngine;
using UnityEngine.AI;

/// Arquetipo A: patrulheiro. Anda uma rota, enxerga num cone, escuta mal de proposito
/// (quem ouve bem e o Listener — um e olhos, o outro e ouvido).
///
/// A deteccao escala com a distancia numa curva quadratica: na borda ele so desconfia,
/// no meio te da tempo de sumir, encostado te pega quase na hora.
///
/// Perder a linha de visao NAO zera a suspeita: ela segura por um tempo e so entao
/// decai. Zerar na hora deixaria o jogador entrar e sair de cobertura impunemente.
///
/// Movimento por NavMeshAgent. Antes ele ia em linha reta na direcao do alvo, o que
/// funcionava no patio vazio e entraria na parede assim que o nivel tivesse canto.
[RequireComponent(typeof(NavMeshAgent))]
public class GuardAI : MonoBehaviour, INoiseListener
{
    public enum State { Patrulha, Investigando, Alerta, Cacando }

    [Header("Patrulha")]
    public Transform[] waypoints;
    public float moveSpeed = 1.8f;
    public float turnSpeed = 200f;
    public float waypointPause = 1.5f;

    [Header("Visao")]
    public float viewAngle = 70f;
    public float viewRange = 8.5f;
    [Tooltip("Segundos ate ser pego, na distancia de referencia.")]
    public float detectTime = 1.5f;
    [Tooltip("Velocidade de deteccao na BORDA do alcance. 0.25 = quatro vezes mais lento la.")]
    [Range(0.05f, 1f)] public float farDetectionRate = 0.25f;
    [Tooltip("Velocidade de deteccao ENCOSTADO nele.")]
    [Range(1f, 10f)] public float nearDetectionRate = 4f;
    [Tooltip("Segundos que a suspeita SEGURA depois de te perder de vista, antes de comecar a cair.")]
    public float detectionHold = 1.2f;
    public LayerMask sightBlockers = ~0;

    [Header("Alturas da linha de visao")]
    [Tooltip("Olho do guarda, deslocado do centro dele.")]
    public float eyeOffset = 0.6f;
    [Tooltip("Alvo no jogador em pe. Acima do topo das caixas: cobertura baixa nao esconde quem esta de pe.")]
    public float markOffsetStanding = 0.5f;
    [Tooltip("Alvo no jogador agachado. Abaixo do topo das caixas (1.4m): agachar passa a esconder de verdade.")]
    public float markOffsetCrouched = -0.6f;

    [Header("Luz")]
    [Tooltip("Multiplica o alcance de visao quando a lanterna do jogador esta acesa.")]
    public float flashlightRangeMultiplier = 1.35f;

    [Header("Justica")]
    // Guarda fora da tela nao te pega. A camera mostra ~6 m pra cima e ~11 m
    // pro lado; com alcance 12 x lanterna 1,7 o patrulheiro te via a 20 m e
    // voce perdia sem nunca ter visto quem foi. Quem te pega tem que estar
    // visivel pra voce reagir.
    [Tooltip("So acumula deteccao quando o guarda esta dentro do enquadramento da camera.")]
    public bool soNaTela = true;
    [Tooltip("Margem da borda da tela (fracao) que ainda nao conta como 'na tela'.")]
    [Range(0f, 0.2f)] public float margemDaTela = 0.04f;

    [Header("Tiro")]
    [Tooltip("Guarda armado atira quando te ve de verdade. Desarmado so persegue.")]
    public bool armado = true;
    [Tooltip("Ate onde ele acerta alguma coisa.")]
    public float alcanceDoTiro = 15f;
    [Tooltip("Segundos entre reconhecer o alvo e o primeiro tiro. E a sua janela pra sumir.")]
    public float tempoDeMira = 0.8f;
    [Tooltip("Segundos entre um tiro e outro.")]
    public float intervaloDoTiro = 1.1f;
    [Tooltip("Vida que cada acerto tira.")]
    public int danoDoTiro = 1;
    [Tooltip("Chance de acertar encostado.")]
    [Range(0f, 1f)] public float pontariaPerto = 0.65f;
    [Tooltip("Chance de acertar no limite do alcance.")]
    [Range(0f, 1f)] public float pontariaLonge = 0.2f;
    [Tooltip("Distancia que ele tenta manter do alvo enquanto atira.")]
    public float distanciaDeCombate = 6f;

    [Header("Alerta")]
    [Tooltip("Segundos cacando depois de te perder de vista.")]
    public float tempoDeCaca = 18f;
    [Tooltip("Quanto ele corre mais rapido enquanto esta alerta.")]
    public float pressaNoAlerta = 1.75f;
    [Tooltip("Quanto o cone de visao abre enquanto ele caca.")]
    public float visaoNoAlerta = 1.35f;
    [Tooltip("Raio em que ele vasculha em volta do ultimo lugar onde te viu.")]
    public float raioDaCaca = 7f;

    [Header("Audicao")]
    [Tooltip("Multiplica o raio do ruido recebido. Baixo de proposito: este arquetipo e OLHOS.")]
    public float hearingSensitivity = 0.55f;
    public float investigateDuration = 6f;

    [Header("Busca")]
    [Tooltip("Amplitude da varredura de olhar ao chegar no ponto investigado.")]
    public float scanArc = 55f;
    public float scanSpeed = 1.6f;

    [Header("Sentinela")]
    // Um guarda que anda olha para onde anda. Como as rondas correm leste-oeste
    // e o jogador sobe pro norte, os cones passam a vida apontando de lado — e
    // a cobertura instantanea do nivel desaba pra 6%.
    //
    // A sentinela resolve isso: fica no posto e varre um arco em volta de uma
    // direcao AUTORADA. Ela cobre o mesmo ponto o tempo todo, entao vale por
    // tres patrulheiros na hora de defender um gargalo, um objetivo ou a saida.
    [Tooltip("Fica no posto varrendo um arco em vez de patrulhar.")]
    public bool sentinela;
    [Tooltip("Para onde o posto olha, em graus. 0 = norte, 90 = leste, 180 = sul.")]
    public float direcaoDoPosto;
    [Tooltip("Amplitude da varredura da sentinela, em graus para cada lado.")]
    public float arcoDoPosto = 42f;
    [Tooltip("Velocidade da varredura. Baixo demais e estatua, alto demais e ventilador.")]
    public float velocidadeDoPosto = 0.5f;

    public State CurrentState { get; private set; } = State.Patrulha;
    public float Detection => detection;
    /// Alerta ou cacando: o HUD e o ESP mostram isso, e outros guardas contagiam.
    public bool Ligado => CurrentState == State.Alerta || CurrentState == State.Cacando;

    /// Alcance real de visao neste frame.
    /// Lanterna acesa estica; vegetacao encurta. Os dois se cancelam parcialmente,
    /// que e o certo: acender a lanterna dentro do mato denuncia voce de qualquer jeito.
    public float EffectiveRange =>
        viewRange
        * (Ligado ? visaoNoAlerta : 1f)
        * (flashlight != null && flashlight.IsOn ? flashlightRangeMultiplier : 1f)
        * (concealment != null ? concealment.VisibilityMultiplier : 1f);

    float detection;
    float holdTimer;
    Transform player;
    SoldierController soldier;
    Flashlight flashlight;
    PlayerConcealment concealment;
    int index;
    float pauseTimer;
    float investigateTimer;
    Vector3 investigatePoint;
    bool scanning;
    float scanYaw;
    float scanPhase;
    NavMeshAgent agent;
    Vector3 lastDestination = new Vector3(9999f, 9999f, 9999f);
    float miraTimer, tiroTimer, cacaTimer;
    Animator corpo;
    CorpoAnimado animado;
    static readonly int GatilhoAtirar = Animator.StringToHash("Atirar");
    bool temGatilho;
    bool recuando;
    Vector3 ultimoVisto;
    Vida vida;

    void OnEnable() => Noise.Listeners.Add(this);
    void OnDisable() => Noise.Listeners.Remove(this);

    void Start()
    {
        soldier = FindFirstObjectByType<SoldierController>();
        if (soldier != null) player = soldier.transform;
        flashlight = FindFirstObjectByType<Flashlight>();
        concealment = FindFirstObjectByType<PlayerConcealment>();
        vida = FindFirstObjectByType<Vida>();
        corpo = GetComponentInChildren<Animator>();
        animado = GetComponent<CorpoAnimado>();
        if (corpo != null && corpo.runtimeAnimatorController != null)
            foreach (var pa in corpo.parameters)
                if (pa.nameHash == GatilhoAtirar) temGatilho = true;

        agent = GetComponent<NavMeshAgent>();
        if (agent != null)
        {
            // a capsula tem o centro na origem: sem o offset ela afunda meio corpo na malha
            agent.baseOffset = 1f;
            agent.speed = moveSpeed;
            agent.angularSpeed = turnSpeed;
            agent.acceleration = 12f;
            agent.autoBraking = true;
        }

        postoOriginal = transform.position;
    }

    void Update()
    {
        bool sees = CanSeePlayer(out float distance);

        if (sees)
        {
            float range = EffectiveRange;
            float closeness = Mathf.InverseLerp(range, 0f, distance);
            float rate = Mathf.Lerp(farDetectionRate, nearDetectionRate, closeness * closeness);
            detection += Time.deltaTime * rate / Mathf.Max(0.05f, detectTime);
            holdTimer = detectionHold;
        }
        else if (holdTimer > 0f)
        {
            holdTimer -= Time.deltaTime;          // a suspeita segura antes de comecar a cair
        }
        else
        {
            detection -= Time.deltaTime / Mathf.Max(0.05f, detectTime * 1.6f);
        }
        detection = Mathf.Clamp01(detection);

        // Detectado nao e mais "morreu". Ele te viu, grita, levanta a arma e
        // ATIRA — voce ainda tem o que fazer: sumir, correr ou encarar.
        if (detection >= 1f)
        {
            if (CurrentState != State.Alerta)
            {
                CurrentState = State.Alerta;
                miraTimer = tempoDeMira;
                Tiro.Barulho(transform.position, 22f);   // o grito dele chama os outros
            }
            if (sees) { ultimoVisto = player.position; cacaTimer = tempoDeCaca; }
        }

        if (sees && CurrentState != State.Alerta)
        {
            CurrentState = State.Investigando;
            investigatePoint = player.position;
            investigateTimer = investigateDuration;
            scanning = false;
        }

        switch (CurrentState)
        {
            case State.Patrulha: Patrol(); break;
            case State.Investigando: Investigate(); break;
            case State.Alerta: Combater(sees, distance); break;
            case State.Cacando: Cacar(); break;
        }
    }

    /// Te viu: encara, mantem distancia de tiro e dispara em intervalos.
    /// Perdeu de vista: passa a cacar (ronda agressiva no ultimo lugar).
    void Combater(bool vendo, float distancia)
    {
        if (player == null) { CurrentState = State.Patrulha; return; }

        if (!vendo)
        {
            CurrentState = State.Cacando;
            cacaTimer = tempoDeCaca;
            return;
        }

        ultimoVisto = player.position;
        cacaTimer = tempoDeCaca;

        // encara o alvo (o agente nao gira sozinho enquanto para pra atirar)
        if (agent != null && agent.isOnNavMesh) agent.updateRotation = false;
        var para = player.position - transform.position; para.y = 0f;
        if (para.sqrMagnitude > 0.01f)
            transform.rotation = Quaternion.RotateTowards(transform.rotation,
                Quaternion.LookRotation(para.normalized, Vector3.up), turnSpeed * 1.6f * Time.deltaTime);

        // longe demais pra acertar: fecha a distancia. Colado: recua um passo.
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = moveSpeed * pressaNoAlerta;
            recuando = false;
            if (distancia > distanciaDeCombate * 1.15f) MoveTowards(player.position, distanciaDeCombate);
            else if (distancia < distanciaDeCombate * 0.55f)
            {
                // colado demais: anda PRA TRAS sem tirar os olhos de voce
                recuando = true;
                MoveTowards(transform.position - para.normalized * 2f, 0.4f);
            }
            else { agent.isStopped = true; lastDestination = new Vector3(9999f, 9999f, 9999f); }
        }

        // recuar de verdade: quem cuida do ritmo da animacao e o CorpoAnimado,
        // entao o sinal tem que ser pedido pra ele (senao um sobrescreve o outro)
        if (animado != null) animado.DeCostas = recuando;
        if (recuando)
        {
            miraTimer = Mathf.Max(miraTimer, 0.25f);   // recuando ele nao atira: ganha folego e volta a mirar
            return;
        }

        if (!armado) return;

        if (miraTimer > 0f) { miraTimer -= Time.deltaTime; return; }   // erguer a arma demora
        tiroTimer -= Time.deltaTime;
        if (tiroTimer > 0f) return;
        tiroTimer = intervaloDoTiro * Random.Range(0.85f, 1.2f);
        if (corpo != null && temGatilho) corpo.SetTrigger(GatilhoAtirar);
        Atirar(distancia);
    }

    /// Resolve um disparo. Acertar depende da distancia, do que voce esta
    /// fazendo e de estar no mato — correr em campo aberto e o pior lugar.
    void Atirar(float distancia)
    {
        var cano = transform.position + transform.forward * 0.45f + Vector3.up * 1.15f;
        var alvo = player.position + Vector3.up * (soldier != null && soldier.IsCrouching ? 0.45f : 1f);

        if (distancia > alcanceDoTiro) return;

        float chance = Mathf.Lerp(pontariaPerto, pontariaLonge, Mathf.InverseLerp(1.5f, alcanceDoTiro, distancia));
        if (soldier != null && soldier.IsCrouching) chance -= 0.15f;
        if (concealment != null) chance -= (1f - concealment.VisibilityMultiplier) * 0.45f;   // no mato ele chuta
        chance = Mathf.Clamp(chance, 0.05f, 0.95f);

        bool acertou = Random.value < chance && !DevMode.Fantasma;
        var ponto = acertou ? alvo : alvo + new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(-0.5f, 0.6f), Random.Range(-1.2f, 1.2f));
        Tiro.Disparar(cano, ponto, acertou, transform);
        if (acertou && vida != null) vida.Levar(danoDoTiro, transform.position);
    }

    /// Caca: vasculha em volta do ultimo lugar onde te viu, mais rapido e com
    /// o cone mais aberto. Se o tempo acaba sem achar nada, ele esfria e volta
    /// pra ronda normal — alerta eterno e so frustracao.
    void Cacar()
    {
        if (animado != null) animado.DeCostas = false;
        recuando = false;
        cacaTimer -= Time.deltaTime;
        if (cacaTimer <= 0f)
        {
            CurrentState = State.Patrulha;
            detection = Mathf.Min(detection, 0.35f);
            if (agent != null && agent.isOnNavMesh) { agent.speed = moveSpeed; agent.updateRotation = true; }
            return;
        }

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = false;
            agent.speed = moveSpeed * pressaNoAlerta;
            agent.updateRotation = true;
        }

        if (MoveTowards(pontoDaCaca, 0.7f))
        {
            // chegou: escolhe outro canto perto do ultimo lugar onde te viu
            var r = Random.insideUnitCircle * raioDaCaca;
            var tentativa = ultimoVisto + new Vector3(r.x, 0f, r.y);
            if (NavMesh.SamplePosition(tentativa, out var hit, 3f, NavMesh.AllAreas)) pontoDaCaca = hit.position;
            else pontoDaCaca = ultimoVisto;
        }
    }

    Vector3 pontoDaCaca;

    void Patrol()
    {
        if (sentinela) { Vigiar(); return; }

        if (waypoints == null || waypoints.Length == 0) return;
        if (pauseTimer > 0f) { pauseTimer -= Time.deltaTime; return; }

        Transform wp = waypoints[index];
        if (wp == null) { index = (index + 1) % waypoints.Length; return; }

        if (MoveTowards(wp.position, 0.35f))
        {
            index = (index + 1) % waypoints.Length;
            pauseTimer = waypointPause;
        }
    }

    /// O posto: varre o arco em volta da direcao autorada — e VOLTA pra ele.
    ///
    /// A primeira versao disto so parava o agente. Parecia certo ate rodar:
    /// ao investigar um ruido a sentinela andava ate a origem e, terminada a
    /// investigacao, ficava de posto EXATAMENTE onde tinha parado. Uma delas
    /// caminhou ate a rota obrigatoria da Selva e virou um muro permanente ali:
    /// 33 mortes em 90 s, sempre no mesmo ponto, sem jeito de passar.
    ///
    /// Posto que nao volta pro posto nao e sentinela, e patrulha de um passo.
    void Vigiar()
    {
        if (Vector3.Distance(transform.position, postoOriginal) > 0.6f)
        {
            if (agent != null && agent.isOnNavMesh) agent.isStopped = false;
            MoveTowards(postoOriginal, 0.3f);
            return;
        }

        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.updateRotation = false;
        }

        scanPhase += Time.deltaTime * velocidadeDoPosto;
        transform.rotation = Quaternion.Euler(0f, direcaoDoPosto + Mathf.Sin(scanPhase) * arcoDoPosto, 0f);
    }

    Vector3 postoOriginal;

    void Investigate()
    {
        investigateTimer -= Time.deltaTime;
        if (investigateTimer <= 0f) { CurrentState = State.Patrulha; scanning = false; return; }

        if (agent != null && agent.isOnNavMesh) agent.isStopped = false;
        if (!MoveTowards(investigatePoint, 0.6f)) { scanning = false; return; }

        // chegou no ponto: varre o olhar em vez de congelar parado
        if (!scanning)
        {
            scanning = true;
            scanYaw = transform.eulerAngles.y;
            scanPhase = 0f;
        }
        if (agent != null) agent.updateRotation = false;          // a varredura manda na rotacao agora
        scanPhase += Time.deltaTime * scanSpeed;
        transform.rotation = Quaternion.Euler(0f, scanYaw + Mathf.Sin(scanPhase) * scanArc, 0f);
    }

    /// Retorna true quando chegou. O caminho e calculado pelo NavMesh,
    /// entao ele contorna geometria em vez de tentar atravessar.
    bool MoveTowards(Vector3 target, float stopDistance)
    {
        if (agent == null || !agent.isOnNavMesh) return true;   // sem malha, nao trava a maquina de estado

        agent.speed = moveSpeed;                                 // permite afinar pelo Inspector rodando
        agent.angularSpeed = turnSpeed;
        agent.stoppingDistance = stopDistance;
        agent.updateRotation = true;

        // so recalcula rota quando o destino muda de verdade
        if ((target - lastDestination).sqrMagnitude > 0.09f)
        {
            lastDestination = target;
            agent.SetDestination(target);
        }

        if (agent.pathPending) return false;
        return agent.remainingDistance <= Mathf.Max(stopDistance, agent.stoppingDistance);
    }

    bool CanSeePlayer(out float distance)
    {
        distance = 0f;
        if (player == null) return false;
        if (DevMode.Fantasma) return false;

        Vector3 to = player.position - transform.position;
        to.y = 0f;
        distance = to.magnitude;

        if (distance > EffectiveRange) return false;
        if (Vector3.Angle(transform.forward, to.normalized) > viewAngle * 0.5f) return false;
        if (soNaTela && !NaTela()) return false;
        if (Time.time < PrototypeDirector.GracaAte) return false;

        // agachado o alvo desce abaixo do topo das caixas — e a cobertura baixa passa a valer
        float mark = (soldier != null && soldier.IsCrouching) ? markOffsetCrouched : markOffsetStanding;

        Vector3 eye = transform.position + Vector3.up * eyeOffset;
        Vector3 target = player.position + Vector3.up * mark;

        if (Physics.Linecast(eye, target, out RaycastHit hit, sightBlockers, QueryTriggerInteraction.Ignore))
            return hit.transform == player || hit.transform.IsChildOf(player);

        return true;
    }

    Camera cameraDoJogo;

    bool NaTela()
    {
        if (cameraDoJogo == null)
        {
            var rig = FindFirstObjectByType<CameraRig>();
            cameraDoJogo = rig != null && rig.topDownCamera != null ? rig.topDownCamera : Camera.main;
            if (cameraDoJogo == null) return true;
        }
        var v = cameraDoJogo.WorldToViewportPoint(transform.position + Vector3.up);
        return v.z > 0f && v.x > margemDaTela && v.x < 1f - margemDaTela && v.y > margemDaTela && v.y < 1f - margemDaTela;
    }

    public void OnNoise(Vector3 position, float radius)
    {
        if (Vector3.Distance(transform.position, position) > radius * hearingSensitivity) return;
        AlertTo(position);
    }

    /// Manda o guarda checar um ponto. E por aqui que o Listener o comanda.
    public void AlertTo(Vector3 point)
    {
        investigatePoint = point;
        investigateTimer = investigateDuration;
        scanning = false;
        if (CurrentState == State.Patrulha) CurrentState = State.Investigando;
    }

    /// Tiro ouvido, grito, corpo achado: entra em caca direto, sem precisar ver.
    public void Cacar(Vector3 ponto)
    {
        ultimoVisto = ponto;
        pontoDaCaca = ponto;
        cacaTimer = tempoDeCaca;
        if (CurrentState != State.Alerta) CurrentState = State.Cacando;
    }

    public void ResetToPatrol()
    {
        if (animado != null) animado.DeCostas = false;
        recuando = false;
        lastDestination = new Vector3(9999f, 9999f, 9999f);
        if (agent != null && agent.isOnNavMesh)
        {
            agent.ResetPath();
            agent.isStopped = false;
            agent.speed = moveSpeed;
            agent.updateRotation = true;
        }
        detection = 0f;
        holdTimer = 0f;
        investigateTimer = 0f;
        cacaTimer = 0f;
        miraTimer = 0f;
        scanning = false;
        CurrentState = State.Patrulha;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.7f);
        Vector3 a = Quaternion.Euler(0f, -viewAngle * 0.5f, 0f) * transform.forward * viewRange;
        Vector3 b = Quaternion.Euler(0f, viewAngle * 0.5f, 0f) * transform.forward * viewRange;
        Gizmos.DrawRay(transform.position, a);
        Gizmos.DrawRay(transform.position, b);

        Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, viewRange * 0.25f);

        // a linha de visao real, na altura em que ela e de fato tracada
        Vector3 eye = transform.position + Vector3.up * eyeOffset;
        Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.8f);
        Gizmos.DrawLine(eye, eye + transform.forward * viewRange);
    }
}
