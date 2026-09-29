using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// A faca. Silenciosa, intima, irreversivel.
///
/// Taticamente ela e perfeita: nao emite ruido, nao alerta ninguem, remove a ameaca.
/// O custo nao esta no tabuleiro, esta em voce.
///
/// Cada morte crava estresse e ADIANTA a sequencia de aparicoes da entidade.
/// "A guerra nao criou o monstro. Apenas abriu espaco para que ele fosse percebido."
/// Resolver o problema imediato piora o problema de verdade — e essa e a unica
/// ferramenta do arsenal que amarra a camada de stealth na camada de horror.
public class MeleeKill : MonoBehaviour
{
    [Header("Alcance")]
    public float range = 1.7f;
    [Tooltip("Angulo minimo entre a frente do alvo e voce. Acima de 90 significa pelas costas.")]
    public float backAngle = 95f;

    [Header("Controle")]
    public Key key = Key.E;
    [Tooltip("Tecla martelada na luta de frente. Separada do [E] de propósito: " +
             "o [E] começa o ataque, o ESPAÇO é a briga.")]
    public Key teclaDoForcejo = Key.Space;

    [Header("A execucao")]
    [Tooltip("Controlador do ATACANTE (Stealth_Kill_Active). Montado por Ecos/Selva/Montar historia.")]
    public RuntimeAnimatorController controladorDeExecucao;
    [Tooltip("Controlador da VITIMA (Stealth_Kill_Passive).")]
    public RuntimeAnimatorController controladorDaVitima;
    [Tooltip("Faca que aparece na mao durante a execucao.")]
    public GameObject facaNaMao;
    [Tooltip("Onde o Miller fica em relacao ao alvo: atras dele, a esta distancia.")]
    public float recuoDaExecucao = 0.62f;
    [Tooltip("Ate quando o jogador fica preso. Os clipes tem 3,25 s, mas param " +
             "de se mexer em 80% (2,6 s): dali eles emendam na queda e no idle.")]
    public float duracaoDaExecucao = 2.95f;

    [Header("De frente: o forcejo")]
    [Tooltip("Controlador do Miller na facada (estocada). Montado por Ecos/Selva/Montar historia.")]
    public RuntimeAnimatorController controladorDaFacada;
    [Tooltip("Controlador do guarda levando a faca no olho.")]
    public RuntimeAnimatorController controladorDoOlho;
    [Tooltip("Pose do MILLER durante a luta pela faca (empurra-empurra).")]
    public RuntimeAnimatorController controladorDoForcejo;
    [Tooltip("Pose do GUARDA na mesma luta: ele segura a lâmina com as duas mãos. " +
             "Os dois no mesmo clipe pareciam gêmeos no mesmo motor.")]
    public RuntimeAnimatorController controladorDoForcejoDoGuarda;
    [Tooltip("O Miller levando o empurrao quando perde o forcejo.")]
    public RuntimeAnimatorController controladorDoEmpurrao;
    [Tooltip("Pode atacar de frente. Ele agarra a sua faca e vira luta de botao.")]
    public bool permitirDeFrente = true;
    [Tooltip("Segundos pra vencer o forcejo antes que ele ganhe.")]
    public float tempoDoForcejo = 2.6f;
    [Tooltip("Quanto cada toque no botao empurra a faca (0 a 1).")]
    public float forcaPorToque = 0.085f;
    [Tooltip("Quanto a faca volta sozinha por segundo: parar de bater e perder.")]
    public float recuoDoForcejo = 0.3f;
    [Tooltip("Vida perdida se ele ganhar o forcejo.")]
    public int danoDoForcejoPerdido = 1;

    [Header("Sangue e som")]
    [Tooltip("Grunhidos de esforco da luta de frente (varias variacoes).")]
    public AudioClip[] esforcos;
    [Tooltip("Engasgo/afogamento no proprio sangue (varias variacoes).")]
    public AudioClip[] engasgos;
    [Tooltip("Volume do engasgo. Ele e alto de perto e some rapido com a distancia.")]
    [Range(0f, 1f)] public float volumeDoEngasgo = 0.85f;
    [Tooltip("Ate onde o engasgo e ouvido, em metros.")]
    public float alcanceDoEngasgo = 16f;
    [Tooltip("Raio da poca embaixo do corpo.")]
    public float pocaDoCorpo = 0.85f;

    [Header("Na moita")]
    [Tooltip("Segundos pra arrastar o corpo pra dentro da moita.")]
    public float tempoDePuxar = 0.9f;
    [Tooltip("Quanto o corpo afunda na moita (some na folhagem).")]
    public float afundarNaMoita = 0.55f;

    [Header("O preco")]
    public float stressPerKill = 32f;
    [Tooltip("Controlador com a animacao de morte parada no ultimo quadro (AC_Morto).")]
    public RuntimeAnimatorController controladorDeMorte;
    [Tooltip("Cada morte adianta a proxima aparicao da entidade.")]
    public bool feedsEntity = true;

    public int Kills { get; private set; }
    /// 0 a 1 enquanto a luta pela faca esta rolando (o HUD desenha).
    public float Forcejo { get; private set; }
    public bool EmForcejo { get; private set; }
    /// Enquanto a execucao roda, ninguem mexe: nem o jogador, nem outra faca.
    public bool Executando { get; private set; }
    /// Alvo valido neste instante, para o HUD avisar que a faca esta disponivel.
    public Component Target { get; private set; }

    void Start() => Kills = RunState.Mortes;

    void Update()
    {
        if (EmForcejo) return;                      // durante a luta, o [E] e da luta
        Target = FindTarget(out alvoDeFrente);

        var kb = Keyboard.current;
        if (kb == null || Target == null || Executando || Inspecao.Aberta || Caderno.Aberto) return;
        if (!kb[key].wasPressedThisFrame) return;

        if (alvoDeFrente) StartCoroutine(Forcejar(Target));
        else StartCoroutine(Executar(Target, MoitaDeAgora()));
    }

    bool alvoDeFrente;

    /// De frente ele te ve chegando: agarra o seu pulso e a faca fica no meio
    /// dos dois. Bater no botao empurra a lamina; parar deixa ela voltar.
    /// Ganhou, ela entra no olho dele. Perdeu, ele te abre e grita pros outros.
    ///
    /// Existe pra tirar a punicao seca de errar a aproximacao: antes, ser
    /// visto de frente era so perder. Agora e uma briga que voce ainda pode
    /// ganhar — com o preco de fazer barulho e ficar exposto.
    IEnumerator Forcejar(Component alvo)
    {
        if (alvo == null || EmForcejo || Executando) yield break;
        EmForcejo = true;
        Forcejo = 0f;

        var go = alvo.gameObject;
        var inimigo = go.transform;
        var soldado = GetComponent<SoldierController>();
        var cc = GetComponent<CharacterController>();
        var ia = go.GetComponent<GuardAI>();
        var agente = go.GetComponent<UnityEngine.AI.NavMeshAgent>();

        bool travadoAntes = soldado != null && soldado.Travado;
        if (soldado != null) soldado.Travado = true;
        if (ia != null) ia.enabled = false;
        if (agente != null && agente.enabled && agente.isOnNavMesh) agente.isStopped = true;

        // os dois se encaram, colados
        var meio = (transform.position + inimigo.position) * 0.5f;
        var dir = (inimigo.position - transform.position); dir.y = 0f; dir.Normalize();
        var minhaPos = meio - dir * 0.55f;
        var delePos = meio + dir * 0.55f;
        for (float t = 0f; t < 0.14f; t += Time.deltaTime)
        {
            float k = t / 0.14f;
            if (cc != null) cc.enabled = false;
            transform.position = Vector3.Lerp(transform.position, minhaPos, k);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir, Vector3.up), k);
            inimigo.position = Vector3.Lerp(inimigo.position, delePos, k);
            inimigo.rotation = Quaternion.Slerp(inimigo.rotation, Quaternion.LookRotation(-dir, Vector3.up), k);
            yield return null;
        }

        // os dois entram na pose de briga. O CorpoAnimado tem que sair do
        // caminho: ele reescreve a velocidade da animacao todo quadro.
        var meuAn = GetComponentInChildren<Animator>();
        var deleAn = go.GetComponentInChildren<Animator>();
        var meuCorpo = GetComponent<CorpoAnimado>();
        var deleCorpo = go.GetComponent<CorpoAnimado>();
        RuntimeAnimatorController meuAntes = null, deleAntes = null;
        if (meuCorpo != null) meuCorpo.enabled = false;
        if (deleCorpo != null) deleCorpo.enabled = false;
        if (controladorDoForcejo != null)
        {
            if (meuAn != null)
            {
                meuAntes = meuAn.runtimeAnimatorController;
                meuAn.applyRootMotion = false;
                meuAn.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                meuAn.runtimeAnimatorController = controladorDoForcejo;
                meuAn.Play("Forcejo", 0, 0f);
            }
            if (deleAn != null)
            {
                deleAntes = deleAn.runtimeAnimatorController;
                deleAn.applyRootMotion = false;
                deleAn.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                // ele segura a LÂMINA; o Miller empurra. Corpos diferentes,
                // não o mesmo clipe defasado.
                deleAn.runtimeAnimatorController = controladorDoForcejoDoGuarda != null
                    ? controladorDoForcejoDoGuarda : controladorDoForcejo;
                deleAn.Play("Forcejo", 0, 0.35f);
            }
        }

        GameObject faca = null;
        if (facaNaMao != null && meuAn != null && meuAn.isHuman)
        {
            var mao = meuAn.GetBoneTransform(HumanBodyBones.RightHand);
            if (mao != null)
            {
                faca = Instantiate(facaNaMao, mao);
                faca.transform.localPosition = new Vector3(0.04f, 0.02f, 0f);
                faca.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                foreach (var c in faca.GetComponentsInChildren<Collider>(true)) Destroy(c);
                foreach (var m in faca.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(m);
            }
        }

        // a briga acorda a vizinhanca: nao da pra forcejar em silencio
        Noise.Emit(transform.position, 12f, NoiseSource.Outro);

        // e a camera entra na briga: sem isso nada disto e visivel
        lutaNaCamera = true;
        StartCoroutine(CameraDaLuta(transform, inimigo, dir));

        var kb = Keyboard.current;
        float tempo = tempoDoForcejo;
        var baseMinha = transform.position;
        var baseDele = inimigo.position;
        var lado = Vector3.Cross(Vector3.up, dir);

        // O tranco é MOLA, não interpolação.
        //
        // Antes o avanço era uma função linear do progresso e o "tremor" um
        // seno único aplicado igual nos dois — os dois deslizavam em sincronia
        // perfeita, que é exatamente a cara de robô. Aqui cada toque no botão
        // dá um IMPULSO que a mola absorve e devolve: avança com solavanco,
        // recua sozinho quando você para de bater.
        float desloc = 0f, veloc = 0f;
        float pulso = 0f;                 // eco visual de cada toque, decai
        float somEm = 0f;                 // trava pro grunhido não metralhar
        float grunhidoDele = 0.7f;
        float tamDaLente = CameraRig.Lente != null ? CameraRig.Lente.orthographicSize : 0f;
        float ruidoMeu = Random.value * 10f, ruidoDele = Random.value * 10f;

        while (tempo > 0f && Forcejo < 1f)
        {
            float dt = Time.deltaTime;
            tempo -= dt;
            Forcejo = Mathf.Max(0f, Forcejo - recuoDoForcejo * dt);

            bool bateu = kb != null && kb[teclaDoForcejo].wasPressedThisFrame;
            if (bateu)
            {
                Forcejo = Mathf.Min(1f, Forcejo + forcaPorToque);
                veloc += 1.9f;                       // o empurrão
                pulso = 1f;
                if (Time.time > somEm) { somEm = Time.time + 0.16f; Grunhir(transform.position, 1f); }
            }

            // mola: puxa pro ponto que o progresso manda, com atrito
            float alvoDesloc = (Forcejo - 0.5f) * 0.30f;
            veloc += (alvoDesloc - desloc) * 46f * dt;
            veloc -= veloc * 7.5f * dt;
            desloc += veloc * dt;
            desloc = Mathf.Clamp(desloc, -0.22f, 0.26f);

            pulso = Mathf.Max(0f, pulso - dt * 3.2f);

            // o guarda também geme, mais grave e mais espaçado, enquanto ganha
            grunhidoDele -= dt;
            if (grunhidoDele <= 0f)
            {
                grunhidoDele = Random.Range(0.55f, 1.1f);
                Grunhir(inimigo.position, 0.72f);
            }

            // ritmo: a base sobe com o progresso e cada toque dá um pico
            float ritmo = 0.55f + Forcejo * 1.35f;
            if (meuAn != null) meuAn.speed = ritmo + pulso * 1.8f;
            if (deleAn != null) deleAn.speed = Mathf.Max(0.3f, 1.4f - Forcejo * 1.0f - pulso * 0.7f);

            // tremor: frequências e fases DIFERENTES nos dois corpos, e nos
            // três eixos. Mesmo seno nos dois era o que fazia parecer um par
            // de bonecos no mesmo motor.
            float tr = Time.time;
            Vector3 tremorMeu =
                  dir * (Mathf.PerlinNoise(tr * 9f, ruidoMeu) - 0.5f) * 0.05f
                + lado * (Mathf.PerlinNoise(tr * 7f, ruidoMeu + 3f) - 0.5f) * 0.07f
                + Vector3.up * (Mathf.PerlinNoise(tr * 13f, ruidoMeu + 7f) - 0.5f) * 0.035f;
            Vector3 tremorDele =
                  dir * (Mathf.PerlinNoise(tr * 8f, ruidoDele) - 0.5f) * 0.045f
                + lado * (Mathf.PerlinNoise(tr * 6f, ruidoDele + 3f) - 0.5f) * 0.065f
                + Vector3.up * (Mathf.PerlinNoise(tr * 11f, ruidoDele + 7f) - 0.5f) * 0.03f;

            // e eles se APROXIMAM conforme você ganha: a lâmina fecha na cara
            // dele. É o que deixa a barra legível sem olhar pra barra.
            float fecha = Forcejo * 0.13f;
            transform.position = baseMinha + dir * (desloc + fecha) + tremorMeu;
            inimigo.position = baseDele + dir * (desloc * 0.75f - fecha * 0.6f) + tremorDele;

            // torção do tronco: os dois torcem pra lados opostos, e quem está
            // perdendo torce mais
            float torcao = (Forcejo - 0.5f) * 9f + pulso * 5f;
            transform.rotation = Quaternion.LookRotation(dir, Vector3.up) * Quaternion.Euler(0f, -torcao * 0.5f, 0f);
            inimigo.rotation = Quaternion.LookRotation(-dir, Vector3.up) * Quaternion.Euler(0f, torcao, 0f);

            trancoDaCamera = pulso;      // a câmera da luta lê isto

            yield return null;
        }
        trancoDaCamera = 0f;

        bool ganhou = Forcejo >= 1f;
        if (faca != null) Destroy(faca);
        if (meuAn != null) meuAn.speed = 1f;
        if (deleAn != null) deleAn.speed = 1f;
        // o do inimigo volta ao normal aqui; o meu so depois do desfecho
        if (deleAn != null && deleAntes != null && !ganhou) deleAn.runtimeAnimatorController = deleAntes;
        if (deleCorpo != null && !ganhou) deleCorpo.enabled = true;
        if (meuAn != null && meuAntes != null && ganhou) { meuAn.runtimeAnimatorController = meuAntes; meuAn.Rebind(); }

        if (ganhou) yield return NoOlho(go, inimigo, dir);
        else yield return Perdeu(go, ia, dir, meuAn, meuAntes);

        // a câmera segura até aqui: a facada no olho é o plano que importa
        lutaNaCamera = false;
        if (meuCorpo != null) meuCorpo.enabled = true;

        if (cc != null) cc.enabled = true;
        if (soldado != null) soldado.Travado = travadoAntes;
        EmForcejo = false;
        Forcejo = 0f;
    }

    float trancoDaCamera;
    bool lutaNaCamera;

    /// A câmera entra na briga.
    ///
    /// Era AQUI que a luta se perdia. A câmera de jogo está a 24 m de altura,
    /// olhando 80° pra baixo, com enquadramento 6: dois homens de 1,80 m
    /// ocupam 3% da altura da tela. Torção de tronco, tranco a cada toque,
    /// a lâmina fechando na cara do outro — tudo isso era sub-pixel. Não
    /// adiantava animar melhor: não dava pra VER.
    ///
    /// Então a luta ganha o mesmo tratamento da cena do poço, que funcionou:
    /// 48° em vez de 80°, enquadramento 2,3 em vez de 6, e de lado — de
    /// perfil você vê os dois corpos, de cima vê duas cabeças.
    IEnumerator CameraDaLuta(Transform a, Transform b, Vector3 dir)
    {
        var rig = FindFirstObjectByType<CameraRig>();
        var cam = CameraRig.Lente != null ? CameraRig.Lente : Camera.main;
        if (rig == null || cam == null || !cam.orthographic) yield break;

        bool cinematicaAntes = rig.Cinematica;
        Vector3 posAntes = cam.transform.position;
        Quaternion rotAntes = cam.transform.rotation;
        float tamAntes = cam.orthographicSize;
        rig.Cinematica = true;

        // de que lado entrar: o que estiver mais perto de onde a câmera já
        // está, pra o corte ser curto
        Vector3 lado = Vector3.Cross(Vector3.up, dir);
        if (Vector3.Dot(lado, cam.transform.position - a.position) < 0f) lado = -lado;

        const float pitch = 48f, recuo = 9f;
        float giro = Quaternion.LookRotation(-lado, Vector3.up).eulerAngles.y;
        Quaternion rotDaLuta = Quaternion.Euler(pitch, giro, 0f);

        float k = 0f;
        while (lutaNaCamera)
        {
            k = Mathf.MoveTowards(k, 1f, Time.deltaTime / 0.45f);
            float suave = Mathf.SmoothStep(0f, 1f, k);

            Vector3 meio = (a != null && b != null) ? (a.position + b.position) * 0.5f
                         : (a != null ? a.position : posAntes);
            meio += Vector3.up * 1.0f;

            float tremor = (Mathf.PerlinNoise(Time.time * 17f, 3f) - 0.5f) * 0.09f * suave;
            Quaternion rot = Quaternion.Slerp(rotAntes, rotDaLuta, suave);
            Vector3 alvo = meio - (rot * Vector3.forward) * recuo;

            cam.transform.SetPositionAndRotation(alvo, rot);
            cam.orthographicSize = Mathf.Lerp(tamAntes, 2.3f - trancoDaCamera * 0.18f, suave) + tremor;
            yield return null;
        }

        // volta deslizando pro jogo
        Vector3 deVolta = cam.transform.position;
        Quaternion rotVolta = cam.transform.rotation;
        float tamVolta = cam.orthographicSize;
        for (float t = 0f; t < 0.5f; t += Time.deltaTime)
        {
            float s = Mathf.SmoothStep(0f, 1f, t / 0.5f);
            Vector3 destino = a != null ? a.position + new Vector3(0f, rig.height, -rig.back) : posAntes;
            cam.transform.SetPositionAndRotation(
                Vector3.Lerp(deVolta, destino, s),
                Quaternion.Slerp(rotVolta, Quaternion.Euler(rig.pitch, 0f, 0f), s));
            cam.orthographicSize = Mathf.Lerp(tamVolta, tamAntes, s);
            yield return null;
        }
        cam.orthographicSize = tamAntes;
        rig.Cinematica = cinematicaAntes;
    }

    /// Grunhido de esforço. `tom` abaixo de 1 é o guarda: mesma garganta,
    /// mais grave, pra os dois não gemerem com a mesma voz.
    void Grunhir(Vector3 onde, float tom)
    {
        if (esforcos == null || esforcos.Length == 0) return;
        var clipe = esforcos[Random.Range(0, esforcos.Length)];
        if (clipe == null) return;
        var go = new GameObject("Esforco");
        go.transform.position = onde + Vector3.up * 1.4f;
        var a = go.AddComponent<AudioSource>();
        a.clip = clipe;
        a.pitch = tom * Random.Range(0.94f, 1.06f);
        a.volume = 0.7f;
        a.spatialBlend = 1f;
        a.minDistance = 1.5f;
        a.maxDistance = 16f;
        a.rolloffMode = AudioRolloffMode.Linear;
        a.Play();
        Destroy(go, clipe.length / Mathf.Max(0.1f, a.pitch) + 0.15f);
    }

    /// A faca entra no olho. Curto e sujo: ele joga a cabeca pra tras, sangra
    /// de perto e cai onde estava.
    IEnumerator NoOlho(GameObject go, Transform inimigo, Vector3 dir)
    {
        var dele = go.GetComponentInChildren<Animator>();
        var meu = GetComponentInChildren<Animator>();
        var cabeca = Osso(dele, HumanBodyBones.Head, HumanBodyBones.Neck);
        Desarmar(go);
        Kills++;
        RunState.Mortes = Kills;

        // a estocada: o Miller crava, ele joga a cabeca pra tras e desaba
        RuntimeAnimatorController antes = null;
        Vector3 baseLocal = Vector3.zero;
        if (meu != null && controladorDaFacada != null)
        {
            antes = meu.runtimeAnimatorController;
            baseLocal = meu.transform.localPosition;
            meu.applyRootMotion = false;
            meu.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            meu.runtimeAnimatorController = controladorDaFacada;
            meu.Play("Facada", 0, 0f);
        }
        GameObject faca = null;
        if (facaNaMao != null && meu != null && meu.isHuman)
        {
            var mao = meu.GetBoneTransform(HumanBodyBones.RightHand);
            if (mao != null)
            {
                faca = Instantiate(facaNaMao, mao);
                faca.transform.localPosition = new Vector3(0.04f, 0.02f, 0f);
                faca.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                foreach (var c in faca.GetComponentsInChildren<Collider>(true)) Destroy(c);
                foreach (var m in faca.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(m);
            }
        }
        if (dele != null && controladorDoOlho != null)
        {
            dele.applyRootMotion = false;
            dele.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            dele.runtimeAnimatorController = controladorDoOlho;
            dele.Play("Levou", 0, 0f);
        }

        // o golpe cai no meio da estocada, nao no comeco
        yield return new WaitForSeconds(0.22f);
        Som();
        if (cabeca != null)
        {
            Sangue.Jato(cabeca.position, -dir * 0.9f + Vector3.up * 0.55f, 1.4f, 0.7f);
            Sangue.Jato(cabeca.position, Vector3.up * 0.3f - dir * 0.2f, 0.6f, 1.6f, inimigo);
        }
        Engasgar(go);
        if (StressSystem.Instance != null) StressSystem.Instance.Spike(stressPerKill * 1.2f);

        // avanca meio passo por cima dele enquanto ele cai
        var de = transform.position;
        for (float t = 0f; t < 0.3f; t += Time.deltaTime)
        {
            transform.position = Vector3.Lerp(de, de + dir * 0.3f, t / 0.3f);
            yield return null;
        }

        if (dele != null)
        {
            Sangue.Poca(inimigo.position, pocaDoCorpo, 7f);
            StartCoroutine(Assentar(dele, dele.transform.localPosition.y));
        }
        yield return new WaitForSeconds(0.35f);
        if (faca != null) Destroy(faca);
        if (meu != null && antes != null)
        {
            meu.transform.localPosition = baseLocal;
            meu.runtimeAnimatorController = antes;
            meu.Rebind();
            meu.Update(0f);
        }
        Som(true);
        Depois();
    }

    /// Ele ganhou: te joga pra tras, te abre um talho e chama os outros.
    IEnumerator Perdeu(GameObject go, GuardAI ia, Vector3 dir, Animator meuAn, RuntimeAnimatorController meuAntes)
    {
        var vida = GetComponent<Vida>();
        if (vida != null) vida.Levar(danoDoForcejoPerdido, go.transform.position);

        // o empurrao tambem e animado: ele te arremessa e voce cambaleia
        if (meuAn != null && controladorDoEmpurrao != null)
        {
            meuAn.runtimeAnimatorController = controladorDoEmpurrao;
            meuAn.Play("Empurrado", 0, 0f);
        }
        var de = transform.position;
        var cc = GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        for (float t = 0f; t < 0.35f; t += Time.deltaTime)
        {
            transform.position = Vector3.Lerp(de, de - dir * 1.3f, Mathf.SmoothStep(0f, 1f, t / 0.35f));
            yield return null;
        }
        if (cc != null) cc.enabled = true;
        if (meuAn != null && meuAntes != null)
        {
            yield return new WaitForSeconds(0.45f);
            meuAn.runtimeAnimatorController = meuAntes;
            meuAn.Rebind();
            meuAn.Update(0f);
        }

        if (ia != null)
        {
            ia.enabled = true;
            ia.Cacar(transform.position);                 // ele sabe exatamente onde voce esta
        }
        var agente = go.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agente != null && agente.enabled && agente.isOnNavMesh) agente.isStopped = false;
        Noise.Emit(transform.position, 20f, NoiseSource.Outro);
        Debug.Log("[FACA] forcejo perdido");
    }

    /// A moita onde o Miller esta agora — se tiver uma, a execucao acaba com
    /// os dois sumindo dentro dela.
    public Moita MoitaDeAgora()
    {
        var oculto = GetComponent<PlayerConcealment>();
        return oculto != null ? oculto.MoitaAtual : null;
    }

    /// Pros testes automatizados: o mesmo que apertar [E] num alvo válido.
    public bool ExecutarAgora()
    {
        Target = FindTarget(out bool frente);
        if (Target == null || Executando || EmForcejo) return false;
        if (frente) StartCoroutine(Forcejar(Target));
        else StartCoroutine(Executar(Target, MoitaDeAgora()));
        return true;
    }

    /// Alvo valido AGORA e se ele esta de costas (execucao) ou de frente
    /// (forcejo). De frente tambem vale: so custa caro.
    Component FindTarget() => FindTarget(out _);

    Component FindTarget(out bool deFrente)
    {
        Component melhor = null;
        float perto = range;
        deFrente = false;

        foreach (var g in FindObjectsByType<GuardAI>(FindObjectsSortMode.None))
        {
            float d = Distancia(g.transform);
            if (d > perto) continue;
            bool costas = Vector3.Angle(g.transform.forward, transform.position - g.transform.position) >= backAngle;
            if (!costas && !permitirDeFrente) continue;
            perto = d; melhor = g; deFrente = !costas;
        }

        foreach (var l in FindObjectsByType<Listener>(FindObjectsSortMode.None))
        {
            float d = Distancia(l.transform);
            if (d > perto) continue;
            perto = d; melhor = l; deFrente = false;      // o ouvinte e cego: angulo nao importa
        }

        return melhor;
    }

    float Distancia(Transform t)
    {
        Vector3 v = t.position - transform.position;
        v.y = 0f;
        return v.magnitude;
    }

    /// A execucao, do encosto ao corpo no chao.
    ///
    /// Sao DUAS animacoes de um par so (Stealth_Kill_Active / _Passive, ambas
    /// humanoides, 3,25 s): o Miller entra por tras, tapa a boca e corta; o
    /// outro se debate e cai. Elas foram feitas na mesma origem, entao os dois
    /// tem que comecar encaixados — o Miller desliza pro lugar certo antes de
    /// tocar, e ninguem controla nada enquanto corre.
    ///
    /// Antes disto a faca era um [E] e um corpo que aparecia deitado: sem
    /// animacao, matar nao custava nada nem na tela nem na barriga.
    IEnumerator Executar(Component alvo, Moita moita = null)
    {
        if (EmForcejo) yield break;
        if (Executando || alvo == null) yield break;
        Executando = true;

        var go = alvo.gameObject;
        var vitima = go.transform;
        var meu = GetComponentInChildren<Animator>();
        var dele = go.GetComponentInChildren<Animator>();
        var cc = GetComponent<CharacterController>();
        var soldado = GetComponent<SoldierController>();
        var corpoAnimado = GetComponent<CorpoAnimado>();

        // 1. ele para de pensar AGORA: no meio da animacao ninguem anda nem ve
        Desarmar(go);
        float baseVitimaY = dele != null ? dele.transform.localPosition.y : 0f;

        // 2. o Miller escorrega pra tras dele (as duas animacoes partem daqui)
        bool travadoAntes = soldado != null && soldado.Travado;
        if (soldado != null) soldado.Travado = true;
        if (corpoAnimado != null) corpoAnimado.enabled = false;
        if (cc != null) cc.enabled = false;

        Vector3 dePos = transform.position, paraPos = vitima.position - vitima.forward * recuoDaExecucao;
        paraPos.y = dePos.y;
        Quaternion deRot = transform.rotation, paraRot = Quaternion.LookRotation(vitima.forward, Vector3.up);
        for (float t = 0f; t < 0.16f; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / 0.16f);
            transform.SetPositionAndRotation(Vector3.Lerp(dePos, paraPos, k), Quaternion.Slerp(deRot, paraRot, k));
            yield return null;
        }
        transform.SetPositionAndRotation(paraPos, paraRot);

        // 3. o par toca junto
        // ROOT MOTION LIGADO nos dois: a queda da vitima esta no deslocamento
        // da raiz do clipe. Sem isso os dois terminavam de pe, um dentro do
        // outro. O Animator dos dois mora num filho ("Corpo"), entao o que anda
        // e o filho — no fim a gente devolve esse deslocamento pro pai.
        RuntimeAnimatorController antes = null;
        Vector3 baseLocal = meu != null ? meu.transform.localPosition : Vector3.zero;
        Quaternion baseLocalRot = meu != null ? meu.transform.localRotation : Quaternion.identity;
        if (meu != null && controladorDeExecucao != null)
        {
            antes = meu.runtimeAnimatorController;
            meu.applyRootMotion = true;
            meu.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            meu.runtimeAnimatorController = controladorDeExecucao;
            meu.Play("Executar", 0, 0f);
        }
        if (dele != null && controladorDaVitima != null)
        {
            dele.applyRootMotion = true;
            dele.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            dele.runtimeAnimatorController = controladorDaVitima;
            dele.Play("Executado", 0, 0f);
        }
        GameObject faca = null;
        if (facaNaMao != null && meu != null)
        {
            var mao = meu.isHuman ? meu.GetBoneTransform(HumanBodyBones.RightHand) : null;
            if (mao != null)
            {
                faca = Instantiate(facaNaMao, mao);
                faca.transform.localPosition = new Vector3(0.04f, 0.02f, 0f);
                faca.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                faca.transform.localScale = Vector3.one * 0.85f;
                foreach (var c in faca.GetComponentsInChildren<Collider>(true)) Destroy(c);
                foreach (var m in faca.GetComponentsInChildren<MonoBehaviour>(true)) Destroy(m);
            }
        }

        Kills++;
        RunState.Mortes = Kills;
        Debug.Log($"[FACA] {go.name} removido. Total: {Kills}");
        Som();

        // 4. o corte: sangue e o engasgo dele. O preco cai aqui, no meio do
        // golpe, nao no fim — e o som fica com o CORPO, nao com o jogador,
        // porque quem gorgoleja e ele e quem passar perto tem que ouvir.
        yield return new WaitForSeconds(Mathf.Min(1.1f, duracaoDaExecucao * 0.4f));
        if (StressSystem.Instance != null) StressSystem.Instance.Spike(stressPerKill);
        Som(true);
        Sangrar(dele, go.transform);
        Engasgar(go);
        yield return new WaitForSeconds(Mathf.Max(0.1f, duracaoDaExecucao - 1.1f));

        // 5. ele fica caido; o Miller volta a ser jogavel
        // o corpo termina de cair por conta propria: o jogador nao espera por isso
        if (dele != null && moita == null) StartCoroutine(Assentar(dele, baseVitimaY));
        if (moita != null) StartCoroutine(PuxarProMato(go.transform, dele, moita));
        if (faca != null) Destroy(faca);
        // o corpo andou; o jogador (com o CharacterController) ficou parado.
        // Passa o deslocamento pro pai e recoloca o corpo no lugar dele.
        if (meu != null)
        {
            Vector3 mundo = meu.transform.position;
            float giro = meu.transform.eulerAngles.y - (transform.eulerAngles.y + baseLocalRot.eulerAngles.y);
            meu.transform.localPosition = baseLocal;
            meu.transform.localRotation = baseLocalRot;
            transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y + giro, 0f);
            Vector3 destino = mundo - transform.rotation * Vector3.Scale(baseLocal, transform.localScale);
            destino.y = transform.position.y;
            transform.position = destino;
            if (antes != null) { meu.applyRootMotion = false; meu.runtimeAnimatorController = antes; meu.Rebind(); meu.Update(0f); }
        }
        if (cc != null) cc.enabled = true;
        if (corpoAnimado != null) corpoAnimado.enabled = true;
        if (soldado != null) soldado.Travado = travadoAntes;
        Executando = false;

        Depois();
    }

    /// O par de animacoes acaba com ele AJOELHADO (foi feito pra emendar numa
    /// queda). O controlador dele emenda na mesma queda dos outros corpos;
    /// aqui a gente so espera terminar, devolve o corpo pra altura do chao (o
    /// root motion levantou o filho) e congela no ultimo quadro.
    IEnumerator Assentar(Animator dele, float baseY)
    {
        for (float t = 0f; t < 4f && dele != null; t += Time.deltaTime)
        {
            var st = dele.GetCurrentAnimatorStateInfo(0);
            if (st.IsName("Caido") && st.normalizedTime >= 0.98f) break;
            yield return null;
        }
        if (dele == null) yield break;
        var local = dele.transform.localPosition;
        local.y = baseY;
        dele.transform.localPosition = local;
        dele.speed = 0f;

        // sangue que junta embaixo dele depois de cair
        var peito = Osso(dele, HumanBodyBones.Chest, HumanBodyBones.Spine, HumanBodyBones.Hips);
        Sangue.Poca(peito.position, pocaDoCorpo, 7f);
        Sangue.Jato(peito.position + Vector3.up * 0.06f, Vector3.up * 0.2f + Random.insideUnitSphere * 0.4f, 0.35f, 1.8f);
    }

    /// Osso do avatar, com plano B: no modelo dos soldados o "peito" e o
    /// "pescoco" sao opcionais e vinham nulos — e a poca de sangue nunca
    /// aparecia, sem erro nenhum no console.
    static Transform Osso(Animator an, params HumanBodyBones[] tentativas)
    {
        if (an != null && an.isHuman)
            foreach (var b in tentativas)
            {
                var t = an.GetBoneTransform(b);
                if (t != null) return t;
            }
        return an != null ? an.transform : null;
    }

    /// A execucao que acaba na moita: o Miller puxa o corpo pra dentro da
    /// folhagem, ele afunda e some. Fica o barulho dele se afogando e o sangue
    /// escorrendo pra fora do mato.
    IEnumerator PuxarProMato(Transform corpo, Animator dele, Moita moita)
    {
        if (corpo == null || moita == null) yield break;
        var de = corpo.position;
        var para = moita.Centro + Vector3.down * afundarNaMoita;
        var peito = Osso(dele, HumanBodyBones.Chest, HumanBodyBones.Spine, HumanBodyBones.Hips) ?? corpo;

        // o rastro: sangue no caminho entre onde ele caiu e a moita
        Sangue.Poca(Vector3.Lerp(de, moita.Centro, 0.35f), 0.4f, 4f);

        for (float t = 0f; t < tempoDePuxar; t += Time.deltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / tempoDePuxar);
            corpo.position = Vector3.Lerp(de, para, k);
            yield return null;
        }
        corpo.position = para;
        if (dele != null) dele.speed = 0f;

        // some de vez: dentro da moita nao se ve corpo, se ve sangue
        foreach (var r in corpo.GetComponentsInChildren<Renderer>(true)) r.enabled = false;

        var borda = moita.Centro + (de - moita.Centro).normalized * (moita.raio * 0.8f);
        Sangue.Poca(borda, moita.raio * 0.9f, 8f);
        Sangue.Jato(moita.Centro + Vector3.up * 0.5f, (borda - moita.Centro).normalized + Vector3.up * 0.3f, 0.6f, 0.8f);
        StartCoroutine(Sangue.Pingar(moita.Centro + Vector3.up * 0.35f, 6f));
    }

    /// Jato do pescoco na hora do corte, e um fio de sangue escorrendo depois.
    void Sangrar(Animator dele, Transform corpo)
    {
        var pescoco = Osso(dele, HumanBodyBones.Neck, HumanBodyBones.Head, HumanBodyBones.UpperChest, HumanBodyBones.Chest);
        var pos = pescoco != null ? pescoco.position : corpo.position + Vector3.up * 1.4f;
        var frente = corpo.forward;
        Sangue.Jato(pos, frente * 0.8f + Vector3.up * 0.35f + corpo.right * 0.25f, 1.15f, 0.9f);
        Sangue.Jato(pos, frente * 0.5f + Vector3.down * 0.4f, 0.5f, 1.6f, corpo);
        // ja pinga no chao enquanto ele ainda esta de pe: quando o corpo cair,
        // a poca grande cresce por cima desta
        Sangue.Poca(corpo.position + corpo.forward * 0.25f, 0.42f, 3.5f);
    }

    /// O engasgo sai DO corpo: fonte 3D nele, que morre com a distancia.
    void Engasgar(GameObject corpo)
    {
        if (engasgos == null || engasgos.Length == 0) return;
        var fonte = corpo.AddComponent<AudioSource>();
        fonte.clip = engasgos[Random.Range(0, engasgos.Length)];
        fonte.spatialBlend = 1f;
        fonte.volume = volumeDoEngasgo;
        fonte.minDistance = 1.5f;
        fonte.maxDistance = alcanceDoEngasgo;
        fonte.rolloffMode = AudioRolloffMode.Linear;
        fonte.dopplerLevel = 0f;
        fonte.Play();
    }

    /// Som do golpe: pano e faca (agudo curto) e o baque do corpo (grave).
    void Som(bool queda = false)
    {
        var fonte = GetComponent<AudioSource>();
        if (fonte == null) return;
        int taxa = 22050, n = taxa / (queda ? 3 : 6);
        var d = new float[n];
        var rnd = new System.Random(queda ? 7 : 3);
        float lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)n;
            float ruido = (float)(rnd.NextDouble() * 2 - 1);
            lp = Mathf.Lerp(lp, ruido, queda ? 0.06f : 0.25f);
            float env = Mathf.Exp(-t * (queda ? 7f : 16f));
            float grave = Mathf.Sin(i * (queda ? 0.018f : 0.03f)) * (queda ? 0.8f : 0.3f);
            d[i] = Mathf.Clamp((lp * (queda ? 1.6f : 0.9f) + grave) * env, -1f, 1f) * 0.7f;
        }
        var c = AudioClip.Create(queda ? "baque" : "golpe", n, 1, taxa, false);
        c.SetData(d, 0);
        fonte.PlayOneShot(c, queda ? 0.6f : 0.45f);
    }

    void Depois()
    {

        if (feedsEntity)
        {
            var dir = FindFirstObjectByType<EntityDirector>();
            if (dir != null)
            {
                var kind = Kills == 1 ? EntityPresence.Manifestation.Confirmada
                                      : EntityPresence.Manifestation.EntreEles;
                dir.Trigger(kind);
            }
        }

        var amb = FindFirstObjectByType<Ambience>();
        if (amb != null) amb.Hush(2.5f);     // o mundo emudece por um instante

        if (MissionDirector.Instance != null)
            MissionDirector.Instance.SetStatus(Kills == 1
                ? "ELE PARA DE SE MEXER. VOCE NAO."
                : "CADA UM DELES A TRAZ MAIS PERTO.");
    }

    /// Tira dele tudo que pensa, anda, colide e ilumina — mas deixa o Animator,
    /// que ainda tem uma animacao pra tocar: a de ser morto.
    ///
    /// Os componentes morrem de verdade: ferramentas que varrem a cena
    /// (vigilancia, gargalo) contam componente desligado, nao contam destruido.
    void Desarmar(GameObject go)
    {
        go.name = "Corpo_" + go.name;

        foreach (var c in go.GetComponents<MonoBehaviour>()) if (c != null && c != this) Destroy(c);
        var agente = go.GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agente != null) Destroy(agente);
        foreach (var col in go.GetComponentsInChildren<Collider>(true)) col.enabled = false;
        foreach (var luz in go.GetComponentsInChildren<Light>(true)) luz.enabled = false;
        foreach (var som in go.GetComponentsInChildren<AudioSource>(true)) som.Stop();
    }

    GUIStyle estiloForcejo;

    void OnGUI()
    {
        if (!EmForcejo) return;
        float esc = Screen.height / 1080f;
        if (estiloForcejo == null) estiloForcejo = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        estiloForcejo.fontSize = Mathf.RoundToInt(30 * esc);

        // Barra magra e embaixo. A anterior tinha 420 px de tarja preta e
        // "!!!" em corpo 30: era maior que os dois homens na tela e lia como
        // interface de depuração em cima da cena.
        estiloForcejo.fontSize = Mathf.RoundToInt(19 * esc);
        float w = 190f * esc, h = 7f * esc;
        var r = new Rect((Screen.width - w) * 0.5f, Screen.height * 0.78f, w, h);
        var prev = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(r.x - 1, r.y - 1, r.width + 2, r.height + 2), Texture2D.whiteTexture);
        GUI.color = new Color(0.62f, 0.09f, 0.07f, 0.95f);
        GUI.DrawTexture(new Rect(r.x, r.y, r.width * Forcejo, r.height), Texture2D.whiteTexture);
        // o aviso pulsa: e pra bater sem parar
        float p = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 12f));
        GUI.color = new Color(0.95f, 0.9f, 0.8f, p);
        string nomeDaTecla = teclaDoForcejo == Key.Space ? "ESPAÇO" : teclaDoForcejo.ToString();
        GUI.Label(new Rect(r.x, r.y + h + 4f * esc, r.width, 28f * esc), nomeDaTecla, estiloForcejo);
        GUI.color = prev;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.9f, 0.2f, 0.2f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, range);
    }
}
