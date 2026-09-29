using UnityEngine;

/// Travessia rasa do córrego: dá pra passar, mas a água denuncia. Enquanto o
/// jogador se mexe aqui dentro, faz ruído como se estivesse correndo — mesmo
/// agachado. É a travessia sem guarda, e o preço dela é esse.
[RequireComponent(typeof(Collider))]
public class VauBarulhento : MonoBehaviour
{
    public float raio = 9f;
    public float intervalo = 0.6f;

    CharacterController dentro;
    float timer;
    bool avisou;

    void OnTriggerEnter(Collider c)
    {
        var cc = c.GetComponentInParent<CharacterController>();
        if (cc == null || c.GetComponentInParent<SoldierController>() == null) return;
        dentro = cc;
        if (!avisou && RadioDoyle.Instance != null) { avisou = true; RadioDoyle.Instance.Pensa("Água até o joelho. Barulho demais."); }
    }

    void OnTriggerExit(Collider c)
    {
        if (dentro != null && c.GetComponentInParent<CharacterController>() == dentro) dentro = null;
    }

    void Update()
    {
        if (dentro == null) return;
        var v = dentro.velocity; v.y = 0f;
        if (v.magnitude < 0.2f) return;
        timer -= Time.deltaTime;
        if (timer > 0f) return;
        timer = intervalo;
        Noise.Emit(dentro.transform.position, raio, NoiseSource.Jogador);
    }
}
