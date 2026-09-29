using System.Collections.Generic;
using UnityEngine;

/// Copa e galho alto passando por cima da tela, com paralaxe — o primeiro
/// plano escuro de Darkwood.
///
/// São silhuetas chapadas (fotografadas de cima das árvores do projeto pelo
/// construtor da floresta), penduradas entre a câmera e o chão. A câmera é
/// ortográfica, e em ortográfica a altura não gera paralaxe sozinha: então
/// cada silhueta é redesenhada a cada quadro como se estivesse mais perto da
/// lente — ela anda na tela (1 + fator) vezes o que o chão anda. Camada com
/// fator maior parece mais perto e passa mais rápido.
///
/// Em volta do jogador ela desbota, pra nunca cobrir o personagem.
[DisallowMultipleComponent]
public class CopaParalaxe : MonoBehaviour
{
    [System.Serializable]
    public class Folha
    {
        public Transform t;
        public Vector3 ancora;
        [Tooltip("0 = anda junto com o chão. 0,8 = passa quase o dobro mais rápido (bem perto da lente).")]
        public float fator = 0.35f;
        public float altura = 12f;
        [Range(0f, 1f)] public float alfa = 0.9f;
        public float giro;
        public float fase;
    }

    public List<Folha> folhas = new List<Folha>();

    [Header("Em volta do jogador")]
    [Tooltip("Distância (m) entre o jogador e a BORDA da silhueta abaixo da qual ela some. " +
             "Medir pelo centro não serve: a copa tem 10 m e a borda cobria o personagem.")]
    public float raioLivre = 1.2f;
    [Tooltip("A partir desta distância da borda a silhueta volta inteira.")]
    public float raioCheio = 3.8f;

    [Header("Vento")]
    [Tooltip("Graus de balanço.")]
    public float balanco = 2.5f;
    public float velocidadeDoVento = 0.35f;

    static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    Camera cam;
    CameraRig rig;
    Transform jogador;
    MaterialPropertyBlock bloco;
    float tamanhoDeJogo = 6f;

    void Start()
    {
        rig = FindFirstObjectByType<CameraRig>();
        cam = rig != null ? rig.topDownCamera : Camera.main;
        var sol = FindFirstObjectByType<SoldierController>();
        if (sol != null) jogador = sol.transform;
        bloco = new MaterialPropertyBlock();
        if (cam == null || jogador == null) enabled = false;
    }

    void LateUpdate()
    {
        bool deCima = rig == null || rig.mode == CameraMode.TopDown;

        // Quanto o quadro está mais aberto que o zoom de jogo.
        //
        // Estas silhuetas são um primeiro plano: existem pra passar rente à
        // lente e cobrir os cantos de um plano FECHADO. Quando a cutscene de
        // abertura abre o zoom pra 22 (contra 6 do jogo), as 103 viram manchas
        // escuras espalhadas por uma mata de 104 m — e, como andam 1,35x o que
        // a câmera anda, atravessam a tela voando. Acima de 1,15x de abertura
        // elas começam a sumir; em 1,8x não existem mais.
        if (!rig.Cinematica && cam.orthographicSize > 0.1f) tamanhoDeJogo = cam.orthographicSize;
        float aberto = tamanhoDeJogo > 0.1f ? cam.orthographicSize / tamanhoDeJogo : 1f;
        float doZoom = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.8f, 1.15f, aberto));
        // e também somem no caminho contrário: num plano MUITO fechado (a luta
        // de faca fecha em 2,3) a silhueta de 10 m vira uma mancha preta em
        // cima da cena. Elas são calibradas pro zoom de jogo, nos dois sentidos.
        doZoom *= Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.5f, 0.78f, aberto));
        if (doZoom <= 0.001f) deCima = false;

        // o buraco que elas abrem tem que ficar onde a CÂMERA olha: na
        // cutscene, abrir em volta do jogador é abrir fora do quadro
        Vector3 quemLimpa = rig != null && rig.Cinematica ? CameraRig.Foco : jogador.position;

        Vector3 fwd = cam.transform.forward;
        // ponto do chão para onde a câmera olha: é em relação a ele que as
        // camadas deslizam
        float chao = jogador.position.y;
        float passo = fwd.y < -0.05f ? (cam.transform.position.y - chao) / -fwd.y : 0f;
        Vector3 foco = cam.transform.position + fwd * passo;

        var deitar = Quaternion.FromToRotation(Vector3.up, -fwd);
        float t = Time.time * velocidadeDoVento;

        foreach (var f in folhas)
        {
            if (f.t == null) continue;
            var r = f.t.GetComponent<Renderer>();
            if (r == null) continue;
            r.enabled = deCima;
            if (!deCima) continue;

            Vector3 rel = f.ancora - foco; rel.y = 0f;
            Vector3 p = foco + rel * (1f + f.fator);
            p.y = chao + f.altura;
            f.t.position = p;
            float oscila = Mathf.Sin(t * 6.28f + f.fase) * balanco;
            f.t.rotation = deitar * Quaternion.Euler(0f, f.giro + oscila, 0f);

            Vector2 dj = new Vector2(p.x - quemLimpa.x, p.z - quemLimpa.z);
            // a folhagem ocupa uns 70% do quadrado; mede até onde ela termina
            float borda = dj.magnitude - f.t.localScale.x * 0.5f * 0.7f;
            float a = f.alfa * doZoom * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(raioLivre, raioCheio, borda));
            r.GetPropertyBlock(bloco);
            bloco.SetColor(BaseColor, new Color(1f, 1f, 1f, a));
            r.SetPropertyBlock(bloco);
        }
    }
}
