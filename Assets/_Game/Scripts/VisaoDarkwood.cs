using System.Collections.Generic;
using UnityEngine;

/// Campo de visão no estilo Darkwood: o jogador só enxerga o que está no cone
/// para onde ele olha (o mouse) e num raio curto em volta do corpo. O resto do
/// mapa fica escuro, e quem está lá (guardas, cegos, a entidade) some da tela.
/// Você ouve, mas não vê.
///
/// Não é o AOS Fog of War porque ele trabalha numa grade de no máximo 128×128
/// com célula de 1 m. Com a câmera mostrando uns 9 m de altura, cada célula
/// virava um bloco borrado na tela, e ele só sabe fazer círculo, não cone.
/// Aqui são raios de verdade, a cada frame, contra os mesmos colisores que
/// bloqueiam o jogador.
///
/// A escuridão é uma malha em coroa desenhada num plano na altura da cintura,
/// com o buraco no formato do que o jogador vê. ZTest Always no shader faz ela
/// cobrir inclusive o que é mais alto que o plano.
[DisallowMultipleComponent]
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class VisaoDarkwood : MonoBehaviour
{
    [Header("Cone")]
    [Tooltip("Abertura total do cone, em graus.")]
    public float angulo = 100f;
    [Tooltip("Até onde o cone enxerga, em metros.")]
    public float alcance = 16f;
    [Tooltip("Raio que o jogador sempre enxerga, inclusive nas costas.")]
    public float raioProximo = 1.05f;
    [Tooltip("Graus de transição suave nas bordas do cone.")]
    public float suavizarBorda = 14f;

    [Header("Raios")]
    public int raios = 240;
    public float alturaDoOlho = 1.1f;
    [Tooltip("Quanto a visão entra além do ponto atingido, pra face da parede aparecer.")]
    public float penetrarParede = 0.35f;
    public LayerMask obstaculos = ~((1 << 2) | (1 << 5) | (1 << 8) | (1 << 9));

    [Header("Escuridão")]
    [Tooltip("Altura do plano da escuridão acima do pé do jogador.")]
    public float alturaDoPlano = 0.9f;
    [Tooltip("Largura da borda esfumada entre visto e escuro.")]
    public float penumbra = 1.1f;

    [Header("Sem lanterna")]
    [Tooltip("Quanto o cone escurece com a lanterna apagada. 0 = nada muda, " +
             "1 = o cone some. A visão nunca é total: você adivinha vulto.")]
    [Range(0f, 1f)] public float escuroSemLanterna = 0.82f;
    [Tooltip("Segundos para o olho se acostumar quando acende ou apaga.")]
    public float tempoDeAjuste = 0.35f;

    [Header("Quem some fora da visão")]
    public bool esconderForaDaVisao = true;
    [Tooltip("Folga em metros: um guarda com meio corpo no cone já aparece.")]
    public float margem = 0.45f;

    Transform jogador;
    CharacterController cc;
    CameraRig rig;
    Flashlight lanterna;
    float escuroAtual;
    MeshRenderer mr;
    Mesh malha;
    Vector3[] vertices;
    Color32[] cores;
    float[] distancia;
    readonly RaycastHit[] acertos = new RaycastHit[16];
    readonly Dictionary<Collider, bool> ehCorpo = new Dictionary<Collider, bool>();
    readonly Dictionary<Transform, Renderer[]> alvos = new Dictionary<Transform, Renderer[]>();
    readonly List<Transform> mortos = new List<Transform>();
    float proximaBusca;
    bool escondendo;

    void Start()
    {
        var soldado = FindFirstObjectByType<SoldierController>();
        if (soldado == null) { enabled = false; return; }
        jogador = soldado.transform;
        cc = soldado.GetComponent<CharacterController>();
        rig = FindFirstObjectByType<CameraRig>();
        lanterna = FindFirstObjectByType<Flashlight>();

        // a malha é montada em coordenadas de mundo
        transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        transform.localScale = Vector3.one;

        mr = GetComponent<MeshRenderer>();
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;

        // Cinco anéis: corpo (sempre limpo), a subida rápida do escuro, o
        // cone inteiro no mesmo tom, a penumbra e o longe. Com quatro anéis o
        // escuro do cone virava degradê — claro no pé, escuro na ponta — e o
        // que se quer é o cone TODO apagado quando a lanterna está desligada.
        int n = Mathf.Max(32, raios);
        raios = n;
        distancia = new float[n];
        vertices = new Vector3[n * 5];
        cores = new Color32[n * 5];
        for (int i = 0; i < n; i++)
        {
            cores[i] = new Color32(255, 255, 255, 0);
            cores[n + i] = new Color32(255, 255, 255, 0);
            cores[2 * n + i] = new Color32(255, 255, 255, 0);
            cores[3 * n + i] = new Color32(255, 255, 255, 255);
            cores[4 * n + i] = new Color32(255, 255, 255, 255);
        }

        var tri = new int[n * 24];
        for (int i = 0, t = 0; i < n; i++)
        {
            int j = (i + 1) % n;
            for (int anel = 0; anel < 4; anel++)
            {
                int a = anel * n, b = (anel + 1) * n;
                tri[t++] = a + i; tri[t++] = b + i; tri[t++] = b + j;
                tri[t++] = a + i; tri[t++] = b + j; tri[t++] = a + j;
            }
        }

        malha = new Mesh { name = "Escuridao" };
        malha.MarkDynamic();
        malha.vertices = vertices;
        malha.colors32 = cores;
        malha.triangles = tri;
        GetComponent<MeshFilter>().sharedMesh = malha;
    }

    bool Corpo(Collider c)
    {
        if (ehCorpo.TryGetValue(c, out bool e)) return e;
        e = c.GetComponentInParent<CharacterController>() != null
            || c.GetComponentInParent<GuardAI>() != null
            || c.GetComponentInParent<Listener>() != null
            || c.GetComponentInParent<EntityPresence>() != null
            || c.GetComponentInParent<Rastejar>() != null;
        ehCorpo[c] = e;
        return e;
    }

    float Raio(Vector3 origem, Vector3 dir, float max)
    {
        int n = Physics.RaycastNonAlloc(origem, dir, acertos, max, obstaculos, QueryTriggerInteraction.Ignore);
        float melhor = max;
        for (int k = 0; k < n; k++)
            if (acertos[k].distance < melhor && !Corpo(acertos[k].collider)) melhor = acertos[k].distance;
        return melhor;
    }

    void LateUpdate()
    {
        bool ativo = rig == null || (rig.mode == CameraMode.TopDown && !rig.Cinematica);
        mr.enabled = ativo;
        if (!ativo) { Mostrar(); return; }

        float pe = cc != null ? jogador.position.y + cc.center.y - cc.height * 0.5f : jogador.position.y;
        Vector3 olho = new Vector3(jogador.position.x, pe + alturaDoOlho, jogador.position.z);
        float plano = pe + alturaDoPlano;

        // sem lanterna o cone quase some: sobra vulto, não leitura
        float alvoEscuro = lanterna != null && !lanterna.IsOn ? escuroSemLanterna : 0f;
        escuroAtual = Mathf.MoveTowards(escuroAtual, alvoEscuro, Time.deltaTime / Mathf.Max(0.01f, tempoDeAjuste));

        Vector3 f = jogador.forward; f.y = 0f;
        float olhando = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
        float meia = angulo * 0.5f;
        int n = raios;
        float passo = 360f / n;

        for (int i = 0; i < n; i++)
        {
            float th = i * passo;
            float rad = th * Mathf.Deg2Rad;
            var dir = new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad));

            float fora = Mathf.Abs(Mathf.DeltaAngle(th, olhando)) - (meia - suavizarBorda * 0.5f);
            float w = 1f - Mathf.Clamp01(fora / suavizarBorda);
            w = w * w * (3f - 2f * w);
            float quer = Mathf.Lerp(raioProximo, alcance, w);

            float bate = Raio(olho, dir, quer);
            float d = bate < quer ? Mathf.Min(quer, bate + penetrarParede) : quer;
            distancia[i] = d;

            float corpo = Mathf.Min(d, raioProximo);
            float subida = Mathf.Min(d, raioProximo + 0.9f);
            vertices[i] = new Vector3(olho.x + dir.x * corpo, plano, olho.z + dir.z * corpo);
            vertices[n + i] = new Vector3(olho.x + dir.x * subida, plano, olho.z + dir.z * subida);
            vertices[2 * n + i] = new Vector3(olho.x + dir.x * d, plano, olho.z + dir.z * d);
            float p = d + penumbra * (0.4f + 0.6f * w);
            vertices[3 * n + i] = new Vector3(olho.x + dir.x * p, plano, olho.z + dir.z * p);
            vertices[4 * n + i] = new Vector3(olho.x + dir.x * 250f, plano, olho.z + dir.z * 250f);
            // o escuro do cone só vale onde há cone: o anel do corpo fica limpo
            byte a = (byte)(Mathf.Clamp01(escuroAtual * w) * 255f);
            cores[n + i] = new Color32(255, 255, 255, a);
            cores[2 * n + i] = new Color32(255, 255, 255, a);
        }

        malha.vertices = vertices;
        malha.colors32 = cores;
        malha.bounds = new Bounds(olho, new Vector3(520f, 10f, 520f));

        if (esconderForaDaVisao && !DevMode.VerTudo) Esconder(); else Mostrar();
    }

    /// true se o ponto está dentro do que o jogador vê agora
    public bool Enxerga(Vector3 p)
    {
        if (distancia == null || jogador == null) return true;
        Vector3 d = p - jogador.position; d.y = 0f;
        float th = Mathf.Repeat(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 360f);
        int i = Mathf.RoundToInt(th / (360f / raios)) % raios;
        // com a lanterna apagada o cone não revela mais: só o raio do corpo
        float limite = escuroAtual > 0.5f ? Mathf.Min(distancia[i], raioProximo + 0.6f) : distancia[i];
        return d.magnitude <= limite + margem;
    }

    void Esconder()
    {
        if (Time.time >= proximaBusca) Buscar();
        escondendo = true;

        foreach (var par in alvos)
        {
            if (par.Key == null) { mortos.Add(par.Key); continue; }
            bool ve = Enxerga(par.Key.position);
            foreach (var r in par.Value) if (r != null) r.forceRenderingOff = !ve;
        }
        foreach (var m in mortos) alvos.Remove(m);
        mortos.Clear();
    }

    void Mostrar()
    {
        if (!escondendo) return;
        escondendo = false;
        foreach (var par in alvos)
            if (par.Value != null)
                foreach (var r in par.Value) if (r != null) r.forceRenderingOff = false;
    }

    void Buscar()
    {
        proximaBusca = Time.time + 2f;
        Adicionar(FindObjectsByType<GuardAI>(FindObjectsSortMode.None));
        Adicionar(FindObjectsByType<Listener>(FindObjectsSortMode.None));
        Adicionar(FindObjectsByType<EntityPresence>(FindObjectsSortMode.None));
        Adicionar(FindObjectsByType<Rastejar>(FindObjectsSortMode.None));
    }

    void Adicionar<T>(T[] lista) where T : Component
    {
        foreach (var c in lista)
            if (!alvos.ContainsKey(c.transform))
                alvos[c.transform] = c.GetComponentsInChildren<Renderer>(true);
    }

    void OnDisable()
    {
        escondendo = true;
        Mostrar();
        if (mr != null) mr.enabled = false;
    }
}
