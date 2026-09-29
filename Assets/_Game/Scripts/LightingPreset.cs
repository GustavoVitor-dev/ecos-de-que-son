using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

/// Dois modos de iluminacao: AUTORIA e JOGO.
///
/// Existe porque tentar avaliar layout no escuro nao funciona — na sombra a luz
/// ambiente domina e tudo vira a mesma laje cinza, por mais que os materiais sejam
/// diferentes. E o inverso tambem vale: nao da pra julgar atmosfera de um nivel
/// cujo layout ainda nao funciona.
///
/// Entao: constroi e afina na luz, escurece depois. Tecla L alterna.
[ExecuteAlways]
public class LightingPreset : MonoBehaviour
{
    public enum Mode { Autoria, Jogo }

    public Mode mode = Mode.Autoria;
    public Key toggleKey = Key.L;
    public Light sun;

    [Header("Autoria: chapado e claro, pra ler geometria")]
    public Color autoriaAmbiente = new Color(0.52f, 0.53f, 0.55f);
    public float autoriaSol = 1.1f;
    public bool autoriaNeblina = false;

    [Header("Jogo: noite de verdade")]
    public Color jogoAmbiente = new Color(0.09f, 0.10f, 0.13f);
    public float jogoSol = 0.30f;
    public bool jogoNeblina = true;
    public float jogoDensidade = 0.026f;
    public Color jogoCorNeblina = new Color(0.05f, 0.06f, 0.08f);

    void OnEnable() => Apply();

    void Update()
    {
        if (!Application.isPlaying) return;
        if (Keyboard.current == null) return;
        if (Keyboard.current[toggleKey].wasPressedThisFrame)
        {
            mode = mode == Mode.Autoria ? Mode.Jogo : Mode.Autoria;
            Apply();
        }
    }

    public void Apply()
    {
        if (sun == null)
        {
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }
        }

        bool autoria = mode == Mode.Autoria;

        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = autoria ? autoriaAmbiente : jogoAmbiente;
        RenderSettings.fog = autoria ? autoriaNeblina : jogoNeblina;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = jogoCorNeblina;
        RenderSettings.fogDensity = jogoDensidade;

        if (sun != null) sun.intensity = autoria ? autoriaSol : jogoSol;
    }
}
