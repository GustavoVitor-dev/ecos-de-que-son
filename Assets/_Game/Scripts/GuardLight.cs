using UnityEngine;

/// A lanterna do guarda E o campo de visao dele.
/// Nada de cone desenhado por cima da tela: o que voce ve iluminado no chao
/// e literalmente o que ele enxerga. Um numero so, duas leituras.
///
/// A cor conta o estado dele sem interface nenhuma:
/// branco morno = patrulhando, ambar pulsando = ouviu algo, vermelho = te achou.
[RequireComponent(typeof(Light))]
public class GuardLight : MonoBehaviour
{
    public GuardAI guard;

    [Header("Cor por estado")]
    public Color patrolColor = new Color(1f, 0.93f, 0.78f);
    public Color investigateColor = new Color(1f, 0.66f, 0.25f);
    public Color alertColor = new Color(1f, 0.28f, 0.2f);

    [Header("Ajuste")]
    [Tooltip("Compensa a inclinacao: facho inclinado alcanca menos chao que o raio bruto.")]
    public float rangeCompensation = 1.15f;
    public float baseIntensity = 6f;

    Light spot;

    void Awake()
    {
        spot = GetComponent<Light>();
        if (guard == null) guard = GetComponentInParent<GuardAI>();
    }

    void LateUpdate()
    {
        if (guard == null || spot == null) return;

        // fonte unica de verdade: mexeu no cone do guarda, a luz acompanha
        spot.spotAngle = guard.viewAngle;
        spot.range = guard.viewRange * rangeCompensation;

        Color target = patrolColor;
        float intensity = baseIntensity;

        if (guard.CurrentState == GuardAI.State.Investigando)
        {
            target = investigateColor;
            intensity = baseIntensity * (1f + 0.22f * Mathf.Sin(Time.time * 8f));
        }
        else if (guard.CurrentState == GuardAI.State.Alerta)
        {
            target = alertColor;
            intensity = baseIntensity * 1.4f;
        }

        spot.color = Color.Lerp(spot.color, target, 1f - Mathf.Exp(-6f * Time.deltaTime));
        spot.intensity = intensity;
    }
}
