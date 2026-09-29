using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// Capítulo I — a corrente da Selva. Ver Docs/HISTORIA_SELVA.md.
///
/// Um operador de rádio perdido conserta um rádio quebrado, ouve nele o melhor
/// amigo pedindo socorro — e vai atrás dele sem lembrar que foi ele quem o
/// deixou. No fim descobre que a bateria estava em ruínas: o rádio nunca
/// funcionou.
///
/// A corrente é obrigatória: pegar o que caiu com você (rádio, faca, caderno),
/// revistar os corpos atrás de peças pro receptor, e só então o Doyle fala. O
/// portão é parede até a chave, a chave está no poço, o poço precisa da
/// manivela, a manivela está na cabana. Matar guarda não abre nada. O Doyle
/// aponta os elos só com o que ele sabia antes de morrer e com onde ele está —
/// ele está no fundo de um poço, não vê nada.
public class Historia : MonoBehaviour
{
    public enum Passo { Acordar, Consertar, BuscarChave, IrAldeia, Poco, Portao, Saindo }

    public static Historia Instance { get; private set; }

    [Header("Cena")]
    public Transform folhaEsquerda;
    public Transform folhaDireita;
    public Collider bloqueioDoPortao;
    public GameObject corpoDoDoyle;
    [Tooltip("A bateria do rádio, escondida: só aparece na inspeção do fim.")]
    public GameObject bateriaPodre;
    public int pecasNecessarias = 3;
    public Transform ponto_TrilhaDoMeio;
    public Transform checkpoint;
    public string proximaCena = "Acampamento|II.  O ACAMPAMENTO|";

    [Header("Manivela")]
    public float raioDoRangido = 13f;

    public Passo Atual { get; private set; } = Passo.Acordar;

    bool viuPortao, falouPrimeiro, temFuzil, viuWard, temManivela, temChave, portaoAberto;
    bool temRadio, temFaca, temCaderno, consertando;
    int pecas;
    MeleeKill faca;
    Cutscene cutscene;
    float progressoPoco;
    int falaDoPoco;
    float rangido;
    Transform jogador;
    RadioDoyle radio;
    string carta; float cartaAlfa;
    Interagivel perto;
    GUIStyle estiloAviso;

    // estático sobrevive entre sessões de Play: o piloto automático liga, aqui desliga
    void Awake() { Instance = this; Inspecao.Pular = false; }

    IEnumerator Start()
    {
        var sc = FindFirstObjectByType<SoldierController>();
        if (sc != null) jogador = sc.transform;
        radio = RadioDoyle.Instance != null ? RadioDoyle.Instance : gameObject.AddComponent<RadioDoyle>();
        if (corpoDoDoyle != null) corpoDoDoyle.SetActive(false);
        if (bateriaPodre != null) bateriaPodre.SetActive(false);
        Liberar("fuzil", false); Liberar("ward", false); Liberar("mochila", false); Liberar("doyle", false);
        LiberarCorpos(false);
        // a faca está no chão: sem ela, não tem como matar ninguém
        faca = FindFirstObjectByType<MeleeKill>();
        if (faca != null) faca.enabled = false;
        Objetivo("");

        // espera a abertura acabar (o chiado e o "Merda de rádio!" estão nela)
        yield return null;
        var cs = FindFirstObjectByType<Cutscene>();
        cutscene = cs;
        while (cs != null && cs.Tocando) yield return null;
        Objetivo(Pista());
    }

    // ---------------------------------------------------------------- eventos de área

    public void Evento(string id)
    {
        switch (id)
        {
            case "portao":
                if (viuPortao) break;
                viuPortao = true;
                radio.Pensa("Trancado. Corrente e cadeado.");
                break;


            case "saida":
                if (Atual == Passo.Portao) StartCoroutine(Fim());
                break;
        }
    }

    /// O receptor consertado. É a primeira vez que o Doyle fala — e ele não
    /// devia conseguir: a bateria está morta faz semanas (ver OCorpo).
    IEnumerator Conserto()
    {
        consertando = true;
        while (radio.Ocupado) yield return null;
        var sc = jogador != null ? jogador.GetComponent<SoldierController>() : null;
        bool travAntes = sc != null && sc.Travado, agAntes = sc != null && sc.ForcarAgachado;
        if (sc != null) { sc.Travado = true; sc.ForcarAgachado = true; }
        radio.Pensa("Fio de cobre no lugar do que queimou.", 0.6f);
        radio.Pensa("A válvula. Fita por cima.", 0.4f);
        radio.Pensa("Vamos... liga.", 0.8f);
        while (radio.Ocupado) yield return null;
        yield return new WaitForSeconds(1.2f);
        if (sc != null) { sc.Travado = travAntes; sc.ForcarAgachado = agAntes; }
        falouPrimeiro = true;
        radio.Doyle("Miller? ...Miller, é o Doyle. Tô te ouvindo respirar.");
        radio.Doyle("Não vai embora. Por favor.", 0.6f);
        radio.Doyle("O portão da base tá trancado. A chave tá com o Ward, no posto dos caixotes. A oeste.", 1.2f);
        radio.Doyle("Pega ela e vem me buscar.", 0.6f);
        Avancar(Passo.BuscarChave);
        Liberar("fuzil", true); Liberar("ward", true);
        if (temFuzil) StartCoroutine(DepoisDoFuzil(3f));
    }

    // ---------------------------------------------------------------- o que caiu com você

    const string TxtRadio = "AN/PRC-25. O meu rádio.\nO painel afundou, o receptor está estraçalhado: a válvula partida, os fios queimados.\nAinda chia. Preciso de peças.";
    const string TxtFaca = "A minha faca. O couro do cabo inchou de chuva.\nTem sangue seco na lâmina. Não lembro de quem.\n\n[E] nas costas de um inimigo: matar em silêncio.";
    const string TxtCaderno = "O meu caderno de operador. Mofado, colado de lama.\nO que os batedores passaram pelo rádio antes de sumir: o mapa, onde viram inimigo.\nO plano da tropa nova. E uma página arrancada.\n\n[N] abrir o caderno";

    void Pegar(Interagivel i, string nome, string texto, System.Action depois, float zoom = 1f)
    {
        Inspecao.Mostrar(i.gameObject, nome, texto, depois, zoom);
        Esconder(i);
    }

    void ItemDoChao()
    {
        Objetivo(Pista());
        if (!(temRadio && temFaca && temCaderno) || Atual != Passo.Acordar) return;
        radio.Pensa("Os inimigos que caíram ontem à noite... ainda estão por aqui.", 0.8f);
        radio.Pensa("Um deles carregava rádio. Válvula, fio, fita. Qualquer coisa.", 0.4f);
        Avancar(Passo.Consertar);
        LiberarCorpos(true);
    }

    /// Revistar um corpo: a peça é um filho escondido com Espolio.
    void Revistar(Interagivel i)
    {
        i.enabled = false;
        var e = i.GetComponentInChildren<Espolio>(true);
        if (e == null)
        {
            radio.Pensa(i.id.EndsWith("_vazio") ? "Nada. Os bolsos já estão pra fora. Alguém chegou antes." : "Nada que sirva.");
            return;
        }
        Inspecao.Mostrar(e.gameObject, e.nome, e.texto, () =>
        {
            pecas++;
            Objetivo(Pista());
            if (pecas >= pecasNecessarias && !consertando) StartCoroutine(Conserto());
        });
    }

    // ---------------------------------------------------------------- interação

    void Interagir(Interagivel i)
    {
        switch (i.id)
        {
            case "radio": Pegar(i, "O RÁDIO", TxtRadio, () => { temRadio = true; ItemDoChao(); }, 1.9f); break;
            case "faca": Pegar(i, "A FACA", TxtFaca, () => { temFaca = true; if (faca != null) faca.enabled = true; ItemDoChao(); }); break;
            case "caderno": Pegar(i, "O CADERNO", TxtCaderno, () => { temCaderno = true; if (Caderno.Instance != null) Caderno.Instance.Tem = true; ItemDoChao(); }); break;

            case "portao":
                if (!temChave) { radio.Pensa(falouPrimeiro ? "Preciso da chave." : "Trancado. Corrente e cadeado."); if (!viuPortao) Evento("portao"); break; }
                if (Atual == Passo.Portao) StartCoroutine(AbrirPortao());
                i.enabled = false;
                break;

            case "ward":
                if (viuWard) { radio.Pensa("Sargento Ward."); break; }
                viuWard = true;
                radio.Pensa("Sargento Ward.");
                radio.Pensa("Sem a chave. Alguém tirou do pescoço dele.");
                StressSystem.Instance?.Spike(10f);
                break;

            case "fuzil":
                temFuzil = true;
                Esconder(i);
                radio.Pensa("Número de série... é o meu.");
                radio.Pensa("Por que o meu fuzil tá aqui?");
                if (falouPrimeiro) StartCoroutine(DepoisDoFuzil(0.5f));
                break;

            case "poco":
                if (!temManivela) { radio.Pensa("A corda tá aí. Falta a manivela."); break; }
                break; // segurar: tratado em Segurando()

            case "mochila":
                StartCoroutine(Mochila(i));
                break;

            case "doyle":
                StartCoroutine(OCorpo(i));
                break;

            default:
                if (i.id.StartsWith("lembranca")) StartCoroutine(Lembranca(i));
                else if (i.id.StartsWith("corpo_")) Revistar(i);
                break;
        }
    }

    /// Fora da corrente: o que o mato guardou daquela noite. Uma isca de
    /// recompensa — explorar custa tempo e risco, tem que pagar.
    IEnumerator Lembranca(Interagivel i)
    {
        i.enabled = false;
        // o Doyle só fala depois do conserto e até o poço
        bool radioVivo = Atual >= Passo.BuscarChave && Atual < Passo.Portao;
        switch (i.id)
        {
            case "lembranca_oeste":
                radio.Pensa("Uma mochila rasgada. Uma foto dentro, molhada.");
                radio.Pensa("Eu e o Doyle em Da Nang. Ele tá rindo.", 0.4f);
                if (radioVivo) radio.Doyle("Você tinha prometido que a gente voltava junto.", 0.8f);
                break;
            case "lembranca_noroeste":
                radio.Pensa("Cartuchos. Muitos. Alguém atirou até acabar.");
                if (radioVivo) radio.Doyle("Fui eu. Atirei até acabar. Você já tinha ido.", 0.8f);
                break;
            case "lembranca_leste":
                radio.Pensa("Pegadas na lama. Dois pares, lado a lado, até a beira.");
                radio.Pensa("Só um par volta.", 0.8f);
                break;
        }
        while (radio.Ocupado) yield return null;
        var isca = FindFirstObjectByType<BaitThrower>();
        if (isca != null) { isca.count++; radio.Pensa("Uma lata vazia. Serve pra fazer barulho longe de mim."); }
    }

    IEnumerator DepoisDoFuzil(float antes)
    {
        yield return new WaitForSeconds(antes);
        radio.Doyle("Você largou ele. Quando correu.");
        radio.Doyle("Eu peguei a chave do Ward. Ia abrir o portão pra nós dois.", 0.6f);
        while (radio.Ocupado) yield return null;
        yield return new WaitForSeconds(3f);
        radio.Doyle("Tô no poço. Na aldeia, a leste.");
        radio.Doyle("Tá escuro aqui embaixo, Miller. Vem.", 0.8f);
        Avancar(Passo.IrAldeia);
        Liberar("mochila", true);
    }

    IEnumerator Mochila(Interagivel i)
    {
        i.enabled = false;
        radio.Pensa("A minha mochila.");
        yield return new WaitForSeconds(1.2f);
        carta = "Senhora Doyle,\n\neu estava com ele até o fim.";
        cartaAlfa = 1f;
        yield return new WaitForSeconds(5f);
        cartaAlfa = 0f;
        radio.Pensa("É a minha letra. Eu nunca mandei.");
        radio.Doyle("Estava?", 1.2f);
        radio.Pensa("Embaixo da carta... a manivela do poço.", 1.5f);
        temManivela = true;
        Avancar(Passo.Poco);
    }

    /// Manivela: segura [E]. Cada volta range e chama quem estiver perto.
    void Segurando(Interagivel i, float dt)
    {
        if (i.id != "poco" || !temManivela || Atual != Passo.Poco) return;

        progressoPoco += dt / Mathf.Max(0.1f, i.tempoSegurando);
        rangido -= dt;
        if (rangido <= 0f)
        {
            rangido = 0.8f;
            Noise.Emit(i.transform.position, raioDoRangido, NoiseSource.Outro);
        }
        string[] falas = { "Tá frio aqui embaixo.", "Faz dois dias que tá frio.", "Você ouviu eu chamando, Miller. Você ouviu." };
        float[] quando = { 0.12f, 0.45f, 0.8f };
        if (falaDoPoco < falas.Length && progressoPoco >= quando[falaDoPoco])
            radio.Doyle(falas[falaDoPoco++]);

        if (progressoPoco >= 1f)
        {
            i.enabled = false;
            StartCoroutine(OBaldeSobe());
        }
    }

    /// A cena do balde: a corda traz o corpo pra cima, ele tomba da borda e cai
    /// no chão ao lado do poço. Uma queda, um baque, acabou.
    ///
    /// A câmera do jogo é ORTOGRÁFICA olhando 80° pra baixo. Subir ou descer
    /// essa câmera não aproxima nada (ortográfica ignora distância) e o pitch
    /// de 80° deixava o poço fora do quadro: o centro da tela caía 3,9 m à
    /// frente dele. Aqui a câmera vira pra 45°, quase terceira pessoa, e o
    /// enquadramento é calculado pelo ponto onde o raio da câmera cruza o chão.
    IEnumerator OBaldeSobe()
    {
        StressSystem.Instance?.Spike(20f);

        var sc = jogador != null ? jogador.GetComponent<SoldierController>() : null;
        var rig = FindFirstObjectByType<CameraRig>();
        var cam = rig != null && rig.topDownCamera != null ? rig.topDownCamera : Camera.main;
        bool travadoAntes = sc != null && sc.Travado;
        if (sc != null) { sc.Travado = true; sc.ForcarAgachado = true; }

        var poco = GameObject.Find("Poco");
        Vector3 boca = poco != null ? poco.transform.position : (corpoDoDoyle != null ? corpoDoDoyle.transform.position : Vector3.zero);
        boca.y = 0f;
        // onde ele vai parar: à frente do poço (lado da câmera), longe o
        // bastante pra não acabar deitado em cima das pedras da boca — na
        // primeira medição ele assentou a 0,90 m do centro, com a mão dentro
        // do buraco, e o corpo desliza ~0,4 m pro poço depois de bater
        Vector3 queda = boca + new Vector3(0.8f, 0f, -1.3f);

        // a corda e o balde do poço: quem sobe agora é ele, não o balde
        Transform corda = null, balde = null;
        if (poco != null)
            foreach (var t in poco.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Corda") corda = t;
                else if (t.name.StartsWith("bucket")) balde = t;
            }
        MedirCorda(corda);
        if (balde != null) balde.gameObject.SetActive(false);

        // enquadramento: o centro da tela é onde o raio da câmera bate no chão,
        // então a posição sai do alvo, não de um offset chutado
        Vector3 alvo = Vector3.Lerp(boca, queda, 0.55f) + Vector3.up * 0.45f;
        const float pitch = 45f, altura = 3.4f;
        Vector3 camDe = Vector3.zero, camPara = alvo + new Vector3(0f, altura, -altura / Mathf.Tan(pitch * Mathf.Deg2Rad));
        Quaternion rotDe = Quaternion.identity, rotPara = Quaternion.Euler(pitch, 0f, 0f);
        float tamDe = 0f;
        const float tamPara = 1.95f;

        if (cam != null && rig != null)
        {
            rig.Cinematica = true;
            camDe = cam.transform.position;
            rotDe = cam.transform.rotation;
            tamDe = cam.orthographicSize;
            for (float t = 0f; t < 1.1f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 1.1f);
                cam.transform.position = Vector3.Lerp(camDe, camPara, k);
                cam.transform.rotation = Quaternion.Slerp(rotDe, rotPara, k);
                cam.orthographicSize = Mathf.Lerp(tamDe, tamPara, k);
                yield return null;
            }
        }

        // A cena é escura de propósito, mas um plano fechado só existe se der
        // pra ver o que está nele. Um resto de luz fria, fraca, que nasce com
        // o corpo e morre com a cena — a lanterna do jogador sozinha depende
        // de pra onde ele está olhando.
        // Os anéis de respingo da chuva são do tamanho certo pra câmera de
        // cima; num close de 3x eles viram argolas brancas de 35 cm boiando
        // na tela inteira. Somem enquanto a cutscene roda.
        ParticleSystem aneis = null;
        var tempestade = FindFirstObjectByType<Tempestade>();
        if (tempestade != null)
            foreach (var ps in tempestade.GetComponentsInChildren<ParticleSystem>(true))
                if (ps.name == "Aneis") { aneis = ps; var em = ps.emission; em.enabled = false; }

        Light luz = null;
        if (corpoDoDoyle != null)
        {
            var luzGo = new GameObject("LuzDoPoco");
            luzGo.transform.position = boca + Vector3.up * 2.3f;
            luz = luzGo.AddComponent<Light>();
            luz.type = LightType.Point;
            luz.range = 7f;
            luz.color = new Color(0.64f, 0.72f, 0.92f);
            luz.intensity = 0f;
            luz.shadows = LightShadows.None;
        }

        if (corpoDoDoyle != null)
        {
            var cadaver = corpoDoDoyle.GetComponent<Cadaver>();
            corpoDoDoyle.SetActive(true);

            if (cadaver != null)
            {
                // o corpo nunca briga com o poço: içado por dentro dele, tomba
                // rente à borda. Um empurrão do colisor da boca bastava pra
                // mandar o cadáver voando.
                if (poco != null)
                    foreach (var c in poco.GetComponentsInChildren<Collider>())
                        cadaver.Ignorar(c);

                // içado pelo quadril, com o resto pendendo mole: enquanto está
                // abaixo do chão, o terreno esconde
                Vector3 fundo = boca + Vector3.down * 1.9f;
                Vector3 topo = boca + Vector3.up * 1.05f;
                cadaver.Pendurar(fundo);
                yield return new WaitForSeconds(0.25f);

                // Ele sobe AOS TRANCOS, uma volta de manivela por vez, e a
                // corda acompanha esticando até ele. Subindo liso e sem corda
                // o corpo parecia flutuar pra fora do buraco sozinho.
                const int puxadas = 7;
                const float subida = 2.6f;
                int ultima = -1;
                for (float t = 0f; t < subida; t += Time.deltaTime)
                {
                    float passo = subida / puxadas;
                    int i = Mathf.Min(puxadas - 1, Mathf.FloorToInt(t / passo));
                    float dentro = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - i * passo) / (passo * 0.62f)));
                    float k = (i + dentro) / puxadas;

                    Vector3 p = Vector3.Lerp(fundo, topo, k);
                    p.x += Mathf.Sin(t * 2.3f) * 0.035f;        // o peso gira devagar na corda
                    p.z += Mathf.Cos(t * 1.7f) * 0.03f;
                    cadaver.MoverGancho(p);
                    EsticarCorda(corda, p.y + 0.12f);
                    if (luz != null) luz.intensity = k * 2.6f;

                    if (i != ultima)
                    {
                        ultima = i;
                        Noise.Emit(boca, raioDoRangido * 0.6f, NoiseSource.Outro);
                        Rangido(boca + Vector3.up * 1.7f);
                    }
                    yield return null;
                }

                // as moscas saem junto com o cheiro, antes de você entender o quê
                if (cadaver.Osso != null) Moscas.Em(cadaver.Osso, 1.0f);
                StartCoroutine(Sangue.Pingar(topo + Vector3.down * 0.3f, 4f, 0.45f));
                yield return new WaitForSeconds(0.5f);

                // a corda cede: ele tomba pra fora girando INTEIRO, do
                // pendurado pro deitado, cabeça pro lado do poço
                Vector3 fora = (queda - boca); fora.y = 0f; fora.Normalize();
                yield return cadaver.Deitar(queda + Vector3.up * 0.55f, -fora, 0.8f);
                EsticarCorda(corda, 0.9f);                  // a corda volta, frouxa
                yield return new WaitForSeconds(0.15f);
                cadaver.Soltar(new Vector3(0f, -0.3f, 0f));

                float espera = Time.time + 4f;
                while (!cadaver.Assentado && Time.time < espera) yield return null;
                yield return new WaitForSeconds(0.35f);
                Sangue.Poca(cadaver.Centro + Vector3.down * 0.1f, 0.85f, 9f);

                // quem decide onde o corpo para é a física, então o
                // enquadramento final vai ATÉ ELE: na primeira montagem ele
                // assentou 2 m fora e ficou no canto da tela.
                if (luz != null) luz.transform.position = cadaver.Centro + Vector3.up * 1.9f;
                if (cam != null)
                {
                    // nem só o corpo (o poço ia pra borda da tela), nem só o
                    // poço: o plano final tem os dois, puxando pro corpo
                    Vector3 fecho = Vector3.Lerp(cadaver.Centro, boca, 0.3f) + Vector3.up * 0.25f;
                    camPara = fecho + new Vector3(0f, altura, -altura / Mathf.Tan(pitch * Mathf.Deg2Rad));
                    Vector3 de = cam.transform.position;
                    for (float t = 0f; t < 0.8f; t += Time.deltaTime)
                    {
                        cam.transform.position = Vector3.Lerp(de, camPara, Mathf.SmoothStep(0f, 1f, t / 0.8f));
                        yield return null;
                    }
                }
            }
            else
            {
                corpoDoDoyle.transform.position = queda;
            }
            StressSystem.Instance?.Spike(15f);
        }

        yield return new WaitForSeconds(1.1f);
        if (cam != null && rig != null)
        {
            for (float t = 0f; t < 0.9f; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / 0.9f);
                cam.transform.position = Vector3.Lerp(camPara, camDe, k);
                cam.transform.rotation = Quaternion.Slerp(rotPara, rotDe, k);
                cam.orthographicSize = Mathf.Lerp(tamPara, tamDe, k);
                if (luz != null) luz.intensity = 2.6f * (1f - k);
                yield return null;
            }
            cam.orthographicSize = tamDe;
            rig.Cinematica = false;
        }
        if (luz != null) Destroy(luz.gameObject);
        if (aneis != null) { var em = aneis.emission; em.enabled = true; }
        SoltarCorda(corda);
        if (balde != null) balde.gameObject.SetActive(true);
        if (sc != null) { sc.Travado = travadoAntes; sc.ForcarAgachado = false; }

        // Aqui o rádio quebra.
        //
        // A revelação de que ele nunca funcionou só vem lá no fim, com a
        // bateria — e até então o "Doyle" seguia conversando normalmente
        // com o corpo do Doyle no chão, o que lia como bug em vez de terror.
        // Agora a voz muda no mesmo instante: grave, afogada no chiado, e
        // daqui pra frente só repete o que já disse. A pergunta que ela faz
        // logo depois é a virada — ela procura o Miller enquanto ele está
        // olhando pro corpo de quem deveria estar falando.
        radio.Pensa("Doyle.");
        while (radio.Ocupado) yield return null;
        radio.Surto(2.2f);
        yield return new WaitForSeconds(1.4f);
        radio.VozQuebrada = true;
        StressSystem.Instance?.Spike(18f);

        radio.DoyleInedito("Miller? Cadê você?");
        while (radio.Ocupado) yield return null;
        radio.Pensa("...", 0.5f);
        radio.Pensa("Ele tá na minha frente.", 0.6f);
        Liberar("doyle", true);
    }

    IEnumerator OCorpo(Interagivel i)
    {
        i.enabled = false;
        while (radio.Ocupado) yield return null;
        radio.Pensa("Faz dias. Ele tá aqui faz dias.");
        radio.Pensa("Na mão dele... a chave do portão.", 0.4f);
        radio.Pensa("E uma plaqueta arrebentada.", 0.6f);
        radio.Pensa("MILLER, J.  —  É a minha.", 0.8f);
        while (radio.Ocupado) yield return null;
        temChave = true;
        yield return new WaitForSeconds(1.5f);
        radio.Diga(RadioDoyle.Quem.MillerNoRadio, "Solta, Doyle! Solta!");
        while (radio.Ocupado) yield return null;
        StressSystem.Instance?.Spike(25f);
        radio.Pensa("O rádio apagou.", 1f);
        radio.Pensa("Abro a tampa da bateria.", 0.8f);
        while (radio.Ocupado) yield return null;
        yield return new WaitForSeconds(0.6f);
        if (bateriaPodre != null)
        {
            bool fechou = false;
            Inspecao.Mostrar(bateriaPodre, "A BATERIA",
                "Inchada. Rachada. Verde de corrosão por dentro, o ácido seco escorrido pela lateral.\nIsso não morreu anteontem.", () => fechou = true);
            while (!fechou) yield return null;
        }
        radio.Pensa("Em ruínas. Faz semanas.", 0.4f);
        radio.Pensa("Esse rádio nunca funcionou.", 0.8f);
        radio.Pensa("Então quem é que tava falando comigo?", 1f);
        Avancar(Passo.Portao);
        // o relâmpago seguinte mostra ele. Atrás de você.
        yield return new WaitForSeconds(6f);
        var tem = FindFirstObjectByType<Tempestade>();
        if (tem != null) tem.Relampejar(true);
    }

    IEnumerator AbrirPortao()
    {
        radio.Pensa("A chave serve.");
        // gira em volta do eixo vertical do MUNDO, na dobradiça (o pivô da
        // folha): o eixo local do modelo importado não aponta pra cima
        float girado = 0f, alvo = 100f;
        while (girado < alvo)
        {
            float passo = Mathf.Min(alvo - girado, alvo * Time.deltaTime / 1.6f);
            if (folhaEsquerda) folhaEsquerda.RotateAround(folhaEsquerda.position, Vector3.up, -passo);
            if (folhaDireita) folhaDireita.RotateAround(folhaDireita.position, Vector3.up, passo);
            girado += passo;
            yield return null;
        }
        if (bloqueioDoPortao != null) bloqueioDoPortao.enabled = false;
        portaoAberto = true;
        Noise.Emit(jogador != null ? jogador.position : transform.position, 8f, NoiseSource.Outro);
    }

    IEnumerator Fim()
    {
        Avancar(Passo.Saindo);
        yield return new WaitForSeconds(1.2f);
        // a última fala é nova de propósito: a voz quebrada só repete, e é
        // justamente ela quebrar a própria regra que fecha o capítulo
        radio.DoyleInedito("Miller... espera eu.");
        while (radio.Ocupado) yield return null;
        yield return new WaitForSeconds(1.5f);
        if (Transicao.Instance != null) Transicao.Instance.Ir(proximaCena);
        else UnityEngine.SceneManagement.SceneManager.LoadScene(proximaCena.Split('|')[0]);
    }

    // ---------------------------------------------------------------- quadro a quadro

    void Update()
    {
        if (jogador == null) return;
        if (Inspecao.Aberta || Caderno.Aberto || cutscene != null && cutscene.Tocando) { perto = null; return; }

        // interagível mais perto no raio
        perto = null; float melhor = float.MaxValue;
        foreach (var i in Interagivel.Todos)
        {
            if (!i.isActiveAndEnabled) continue;
            var d = i.transform.position - jogador.position; d.y = 0f;
            float m = d.magnitude;
            if (m <= i.raio && m < melhor) { melhor = m; perto = i; }
        }

        var kb = Keyboard.current;
        var faca = FindFirstObjectByType<MeleeKill>();
        bool facaTemAlvo = faca != null && faca.Target != null;
        if (perto != null && kb != null && !facaTemAlvo)
        {
            if (perto.segurar) { if (kb.eKey.isPressed) { if (perto.id == "poco" && !temManivela) { if (kb.eKey.wasPressedThisFrame) Interagir(perto); } else Segurando(perto, Time.deltaTime); } }
            else if (kb.eKey.wasPressedThisFrame) Interagir(perto);
        }

        LugarSeguro();

    }

    Vector3 ultimoSeguro; bool temSeguro; float cdSeguro;

    /// Uma vez por segundo: nenhum guarda a menos de 11 m e ninguém
    /// desconfiando = aqui é um bom lugar pra renascer.
    void LugarSeguro()
    {
        cdSeguro -= Time.deltaTime;
        if (cdSeguro > 0f) return;
        cdSeguro = 1f;
        foreach (var g in FindObjectsByType<GuardAI>(FindObjectsSortMode.None))
        {
            if (g.Detection > 0.02f) return;
            var d = g.transform.position - jogador.position; d.y = 0f;
            if (d.magnitude < 12f) return;
            // longe da RONDA inteira, não só de onde o guarda está agora: o
            // primeiro "seguro" caiu dentro do retângulo do guarda do portão
            if (g.waypoints != null)
                foreach (var w in g.waypoints)
                {
                    if (w == null) continue;
                    var dw = w.position - jogador.position; dw.y = 0f;
                    if (dw.magnitude < 9f) return;
                }
            if (g.sentinela && d.magnitude < g.viewRange + 4f) return;
        }
        ultimoSeguro = jogador.position; temSeguro = true;
    }

    /// Teste automatizado: o mesmo que apertar [E] no objeto (ou segurar por N s).
    public bool Testar(string id, float segurarPor = 0f)
    {
        foreach (var i in Interagivel.Todos)
        {
            if (i.id != id || !i.isActiveAndEnabled) continue;
            if (segurarPor > 0f) { for (float t = 0f; t < segurarPor; t += 0.1f) Segurando(i, 0.1f); }
            else Interagir(i);
            return true;
        }
        return false;
    }

    /// Pro piloto automático: onde fica o próximo elo e o que fazer nele.
    /// id nulo = só chegar lá (gatilho de área) ou esperar o rádio.
    public bool ProximoElo(out Vector3 pos, out string id, out bool segurar)
    {
        pos = Vector3.zero; id = null; segurar = false;
        string alvo = null;
        switch (Atual)
        {
            case Passo.Acordar: alvo = !temRadio ? "radio" : !temFaca ? "faca" : !temCaderno ? "caderno" : null; break;
            case Passo.Consertar:
                // corpo ainda não revistado mais perto (os vazios também: o jogador não sabe)
                float melhor = float.MaxValue;
                if (pecas < pecasNecessarias && jogador != null)
                    foreach (var i in Interagivel.Todos)
                    {
                        if (!i.isActiveAndEnabled || !i.id.StartsWith("corpo_")) continue;
                        float d = Vector3.Distance(i.transform.position, jogador.position);
                        if (d < melhor) { melhor = d; alvo = i.id; }
                    }
                break;
            case Passo.BuscarChave: alvo = !viuWard ? "ward" : !temFuzil ? "fuzil" : null; break;
            case Passo.IrAldeia: alvo = "mochila"; break;
            case Passo.Poco: alvo = progressoPoco < 1f ? "poco" : !temChave ? "doyle" : null; segurar = alvo == "poco"; break;
            case Passo.Portao: alvo = portaoAberto ? null : "portao"; break;
        }
        if (Atual == Passo.Portao && portaoAberto) { pos = new Vector3(1.5f, 0f, 43.8f); return true; }
        if (alvo == null) return false;
        id = alvo;
        pos = Posicao(alvo);
        return true;
    }

    Vector3 Posicao(string id)
    {
        foreach (var i in FindObjectsByType<Interagivel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (i.id == id) return i.transform.position;
        return Vector3.zero;
    }

    public bool Livre(string id)
    {
        foreach (var i in Interagivel.Todos) if (i.id == id && i.isActiveAndEnabled) return true;
        return false;
    }

    public void BotSegurar(float dt)
    {
        foreach (var i in Interagivel.Todos) if (i.id == "poco" && i.isActiveAndEnabled) { Segurando(i, dt); return; }
    }

    // ---- a corda do poço, esticada até o que está pendurado nela ----
    float cordaTopo, cordaComp, cordaEscalaY, cordaAlturaY;
    AudioClip somDaCorda;

    void MedirCorda(Transform corda)
    {
        cordaComp = 0f;
        if (corda == null) return;
        var r = corda.GetComponent<Renderer>();
        if (r == null) return;
        cordaTopo = r.bounds.max.y;                  // onde ela sai da viga
        cordaComp = Mathf.Max(0.05f, r.bounds.size.y);
        cordaEscalaY = corda.localScale.y;
        cordaAlturaY = corda.position.y;
    }

    void EsticarCorda(Transform corda, float ateY)
    {
        if (corda == null || cordaComp <= 0f) return;
        float baixo = Mathf.Min(ateY, cordaTopo - 0.05f);
        var e = corda.localScale;
        e.y = cordaEscalaY * ((cordaTopo - baixo) / cordaComp);
        corda.localScale = e;
        var p = corda.position;
        p.y = (cordaTopo + baixo) * 0.5f;
        corda.position = p;
    }

    void SoltarCorda(Transform corda)
    {
        if (corda == null || cordaComp <= 0f) return;
        var e = corda.localScale; e.y = cordaEscalaY; corda.localScale = e;
        var p = corda.position; p.y = cordaAlturaY; corda.position = p;
    }

    /// Uma volta da manivela: a corda range com o peso em cima dela.
    void Rangido(Vector3 onde)
    {
        if (somDaCorda == null) somDaCorda = Resources.Load<AudioClip>("Corda");
        if (somDaCorda == null) return;
        AudioSource.PlayClipAtPoint(somDaCorda, onde, 0.85f);
    }

    /// Teste automatizado: roda a cena do poço na hora, sem jogar a fase
    /// inteira até chegar nela.
    public void TestarPoco()
    {
        temManivela = true;
        Atual = Passo.Poco;
        StartCoroutine(OBaldeSobe());
    }

    public bool Ocupada => radio != null && radio.Ocupado;

    public string Estado => $"{Atual} radio/faca/caderno={temRadio}/{temFaca}/{temCaderno} peças={pecas} fuzil={temFuzil} manivela={temManivela} chave={temChave} poço={progressoPoco:0.00} radio={(radio != null && radio.Ocupado ? "falando" : "quieto")}";

    void Avancar(Passo p)
    {
        if (p <= Atual) return;
        Atual = p;
        // renasce no último lugar SEGURO por onde passou, não onde o elo
        // avançou: no portão isso era dentro da ronda do guarda, e renascer
        // ali era ser pego de novo a cada 25 s.
        if (checkpoint != null && jogador != null)
        {
            checkpoint.position = temSeguro ? ultimoSeguro : jogador.position;
            checkpoint.rotation = jogador.rotation;
            var dir = FindFirstObjectByType<PrototypeDirector>();
            if (dir != null) dir.spawnPoint = checkpoint;
        }
        Objetivo(Pista());
    }

    string Pista()
    {
        switch (Atual)
        {
            case Passo.Acordar:
                var falta = new System.Collections.Generic.List<string>();
                if (!temRadio) falta.Add("o rádio"); if (!temFaca) falta.Add("a faca"); if (!temCaderno) falta.Add("o caderno");
                return "pegar do chão: " + string.Join(", ", falta);
            case Passo.Consertar: return $"peças pro receptor nos corpos dos inimigos ({pecas}/{pecasNecessarias})";
            case Passo.BuscarChave: return "a chave: com o Ward, no posto dos caixotes (oeste)";
            case Passo.IrAldeia: return "o Doyle: no poço da aldeia (leste)";
            case Passo.Poco: return "o poço: segurar [E] na manivela";
            case Passo.Portao: return "o portão";
            default: return "";
        }
    }

    void Objetivo(string pista)
    {
        var m = MissionDirector.Instance;
        if (m == null) return;
        string meta = Atual <= Passo.Consertar ? "CONSERTAR O RÁDIO" : "CHEGAR À ZONA DE POUSO AO NORTE";
        m.SetStatus(string.IsNullOrEmpty(pista) ? meta : meta + "   —   " + pista);
    }

    void Liberar(string id, bool sim)
    {
        foreach (var i in FindObjectsByType<Interagivel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (i.id == id) i.enabled = sim;
    }

    void LiberarCorpos(bool sim)
    {
        foreach (var i in FindObjectsByType<Interagivel>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (i.id != null && i.id.StartsWith("corpo_")) i.enabled = sim;
    }

    void Esconder(Interagivel i)
    {
        i.enabled = false;
        foreach (var r in i.GetComponentsInChildren<Renderer>()) r.enabled = false;
    }

    static System.Collections.Generic.List<GuardAI> PorDistancia(Vector3 p)
    {
        var l = new System.Collections.Generic.List<GuardAI>(FindObjectsByType<GuardAI>(FindObjectsSortMode.None));
        l.Sort((a, b) => Vector3.Distance(a.transform.position, p).CompareTo(Vector3.Distance(b.transform.position, p)));
        return l;
    }

    // ---------------------------------------------------------------- tela

    void OnGUI()
    {
        float esc = Screen.height / 1080f;
        if (estiloAviso == null) estiloAviso = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        estiloAviso.fontSize = Mathf.RoundToInt(17 * esc);

        // objetivo: discreto, no canto (o HUD de debug fica no F1)
        var m = MissionDirector.Instance;
        if (m != null && !PrototypeHUD.Visivel)
        {
            var so = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(16 * esc), fontStyle = FontStyle.Bold };
            var prevO = GUI.color;
            var ro = new Rect(28f * esc, 22f * esc, Screen.width * 0.8f, 30f * esc);
            GUI.color = new Color(0f, 0f, 0f, 0.8f); GUI.Label(new Rect(ro.x + 1, ro.y + 1, ro.width, ro.height), m.StatusLine, so);
            GUI.color = new Color(0.85f, 0.82f, 0.72f, 0.85f); GUI.Label(ro, m.StatusLine, so);
            GUI.color = prevO;
        }

        // aviso de [E] em cima do objeto
        var cam = Camera.main;
        var rig = FindFirstObjectByType<CameraRig>();
        if (rig != null && rig.topDownCamera != null) cam = rig.topDownCamera;
        if (perto != null && cam != null)
        {
            var sp = cam.WorldToScreenPoint(perto.transform.position + Vector3.up * 1.2f);
            if (sp.z > 0f)
            {
                string txt = perto.segurar && temManivela ? "[E] SEGURAR  —  " + perto.rotulo + (progressoPoco > 0f ? "  " + Mathf.RoundToInt(progressoPoco * 100f) + "%" : "") : "[E]  " + perto.rotulo;
                var r = new Rect(sp.x - 220f * esc, Screen.height - sp.y - 20f * esc, 440f * esc, 30f * esc);
                var prev = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.8f); GUI.Label(new Rect(r.x + 1, r.y + 1, r.width, r.height), txt, estiloAviso);
                GUI.color = new Color(0.92f, 0.88f, 0.75f, 0.95f); GUI.Label(r, txt, estiloAviso);
                GUI.color = prev;
            }
        }

        // a carta
        if (cartaAlfa > 0.01f && !string.IsNullOrEmpty(carta))
        {
            float w = 520f * esc, h = 300f * esc;
            var r = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.42f, w, h);
            var prev = GUI.color;
            GUI.color = new Color(0.78f, 0.72f, 0.58f, 0.96f * cartaAlfa);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            var st = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(26 * esc), fontStyle = FontStyle.Italic, alignment = TextAnchor.MiddleLeft, wordWrap = true };
            GUI.color = new Color(0.16f, 0.13f, 0.1f, cartaAlfa);
            GUI.Label(new Rect(r.x + 50f * esc, r.y, r.width - 100f * esc, r.height), carta, st);
            GUI.color = prev;
        }
    }
}
