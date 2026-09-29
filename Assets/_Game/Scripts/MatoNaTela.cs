using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// Quando o jogador entra no mato, a tela fecha: folhas escuras invadem pelas
/// bordas, a imagem escurece e perde cor, a vinheta aperta. Saindo, abre de novo.
///
/// Também é leitura de jogo: a força do efeito acompanha o quanto ele está
/// escondido (PlayerConcealment.VisibilityMultiplier). Parado no mato, as folhas
/// fecham as bordas; correndo, a tela mal escurece — porque correndo o mato não
/// esconde.
[DisallowMultipleComponent]
public class MatoNaTela : MonoBehaviour
{
    [Tooltip("Textura de folha com alfa (a mesma da folhagem do cenário).")]
    public Texture2D folha;
    [Tooltip("Cor das folhas na frente da câmera. Quase preto, um fio de verde.")]
    public Color corDasFolhas = new Color(0.02f, 0.032f, 0.022f, 0.66f);
    [Tooltip("Quantas folhas em volta da tela.")]
    public int quantidade = 8;
    [Tooltip("Velocidade de entrar e sair do efeito, por segundo.")]
    public float velocidade = 2.5f;
    [Tooltip("Força mínima do efeito só por estar no mato (correndo).")]
    [Range(0f, 1f)] public float pisoNoMato = 0.3f;
    [Tooltip("Raio livre em volta do centro da tela (onde está o jogador), em pixels numa tela de 1080 de altura.")]
    public float raioLivre = 250f;

    PlayerConcealment ocultacao;
    CameraRig rig;
    Volume volume;
    RectTransform raiz;
    CanvasGroup grupo;
    float peso;

    struct Folha { public RectTransform rt; public Vector2 fora, dentro; public float giro, fase; }
    readonly List<Folha> folhas = new List<Folha>();

    void Start()
    {
        ocultacao = FindFirstObjectByType<PlayerConcealment>();
        rig = FindFirstObjectByType<CameraRig>();
        if (ocultacao == null) { enabled = false; return; }

        CriarVolume();
        CriarFolhas();
        Aplicar(0f);
    }

    void CriarVolume()
    {
        var perfil = ScriptableObject.CreateInstance<VolumeProfile>();
        var vg = perfil.Add<Vignette>(true);
        vg.intensity.Override(0.62f);
        vg.smoothness.Override(0.85f);
        vg.color.Override(new Color(0.0f, 0.02f, 0.01f));
        var ca = perfil.Add<ColorAdjustments>(true);
        ca.postExposure.Override(-0.45f);
        ca.saturation.Override(-35f);
        ca.colorFilter.Override(new Color(0.82f, 0.92f, 0.84f));
        var ab = perfil.Add<ChromaticAberration>(true);
        ab.intensity.Override(0.35f);

        var go = new GameObject("Volume_Mato");
        go.transform.SetParent(transform, false);
        volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 20f;
        volume.sharedProfile = perfil;
        volume.weight = 0f;
    }

    void CriarFolhas()
    {
        var canvasGo = new GameObject("Mato_Tela", typeof(Canvas), typeof(CanvasScaler), typeof(CanvasGroup));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = -10;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 1f;
        grupo = canvasGo.GetComponent<CanvasGroup>();
        grupo.blocksRaycasts = false;
        grupo.interactable = false;
        raiz = (RectTransform)canvasGo.transform;
        if (folha == null) return;

        // semente fixa: a moldura de folhas é sempre a mesma, não pisca
        var sorte = new System.Random(1968);
        for (int i = 0; i < quantidade; i++)
        {
            // espalha em volta da borda: ângulo em volta do centro da tela
            float ang = (i + (float)sorte.NextDouble() * 0.6f) / quantidade * Mathf.PI * 2f;
            var dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            // leva o ponto até a borda de um retângulo 16:9
            float k = 1f / Mathf.Max(Mathf.Abs(dir.x) / 960f, Mathf.Abs(dir.y) / 540f);
            Vector2 borda = dir * k;

            var go = new GameObject("Folha" + i, typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(raiz, false);
            var img = go.GetComponent<RawImage>();
            img.texture = folha;
            img.color = corDasFolhas;
            img.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            float tam = 620f + (float)sorte.NextDouble() * 480f;
            rt.sizeDelta = new Vector2(tam, tam);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            float giro = ang * Mathf.Rad2Deg + 90f + (float)(sorte.NextDouble() * 70.0 - 35.0);

            folhas.Add(new Folha
            {
                rt = rt,
                fora = borda + dir * (tam * 0.55f),
                // A borda da tela já está escura (fora do cone), folha preta lá
                // não aparece. O efeito tem que invadir a parte iluminada, mas
                // parar antes do jogador: a folha nunca entra no raioLivre.
                dentro = dir * Mathf.Max(borda.magnitude - tam * (0.15f + 0.15f * (float)sorte.NextDouble()), raioLivre + tam * 0.42f),
                giro = giro,
                fase = (float)sorte.NextDouble() * 10f
            });
        }
    }

    void LateUpdate()
    {
        bool fps = rig != null && rig.mode == CameraMode.FirstPerson;
        float quer = 0f;
        if (ocultacao.InConcealment && !fps)
            quer = Mathf.Lerp(pisoNoMato, 1f, 1f - ocultacao.VisibilityMultiplier);
        peso = Mathf.MoveTowards(peso, quer, velocidade * Time.deltaTime);
        Aplicar(peso);
    }

    void Aplicar(float p)
    {
        if (volume != null) volume.weight = p;
        if (grupo != null) grupo.alpha = Mathf.Clamp01(p * 1.4f);

        float t = Time.time;
        float e = p * p * (3f - 2f * p);
        foreach (var f in folhas)
        {
            // balança devagar, como mato mexido pelo corpo
            float bal = Mathf.Sin(t * 0.9f + f.fase) * 4f;
            f.rt.anchoredPosition = Vector2.LerpUnclamped(f.fora, f.dentro, e)
                + new Vector2(Mathf.Sin(t * 0.7f + f.fase), Mathf.Cos(t * 0.6f + f.fase)) * 10f * e;
            f.rt.localRotation = Quaternion.Euler(0f, 0f, f.giro + bal);
        }
    }

    void OnDisable()
    {
        if (volume != null) volume.weight = 0f;
        if (grupo != null) grupo.alpha = 0f;
    }
}
