using System.Collections;
using UnityEngine;

/// Corpo caído: a animação de morte para no último quadro e fica lá.
///
/// Existe porque o clipe de morte da biblioteca está marcado como laço: sem
/// isto o soldado morto morre de novo, e de novo, na frente do jogador.
/// Quem já nasce morto (cenário) começa no fim; quem acabou de levar a faca
/// cai na frente do jogador e congela ao terminar.
[DisallowMultipleComponent]
public class MortoParado : MonoBehaviour
{
    public string estado = "Caido";
    [Tooltip("Ligado: já nasce caído. Desligado: cai agora e congela no fim.")]
    public bool comecarNoFim = true;

    Animator an;

    void OnEnable()
    {
        an = GetComponent<Animator>();
        if (an == null) an = GetComponentInChildren<Animator>();
        if (an == null) { enabled = false; return; }

        an.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        if (comecarNoFim) StartCoroutine(Congelar());
        else { an.speed = 1f; an.Play(estado, 0, 0f); }
    }

    /// Um Animator que acabou de ser LIGADO engole o Play do mesmo quadro: ele
    /// só se inicializa no fim do frame, e o corpo aparecia EM PÉ (o primeiro
    /// quadro da queda) pro resto do jogo. Foi o que aconteceu com o Doyle
    /// subindo no balde. Por isso a pose é reaplicada por alguns quadros.
    IEnumerator Congelar()
    {
        // `Animator.Update(0f)` NÃO aplica a pose num humanoide: o corpo ficava
        // parado no quadro em que o clipe estava (o Doyle subia do poço EM PÉ).
        // Com um delta pequeno de verdade, a pose entra.
        for (float t = 0f; t < 0.4f; t += Time.deltaTime)
        {
            if (an == null) yield break;
            an.speed = 1f;
            an.Play(estado, 0, 1f);
            an.Update(0.001f);
            an.speed = 0f;
            yield return null;
        }
        enabled = false;
    }

    void Update()
    {
        if (comecarNoFim || an == null) return;
        if (an.GetCurrentAnimatorStateInfo(0).normalizedTime < 1f) return;
        an.speed = 0f;
        enabled = false;
    }
}
