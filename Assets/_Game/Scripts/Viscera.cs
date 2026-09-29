using UnityEngine;

/// Marca um pedaço de tripa pendurada no cadáver.
///
/// Existe só pra o Cadaver saber que aquele Rigidbody faz parte do corpo:
/// entra na mesma lista dos ossos, ignora colisão com o resto do corpo,
/// congela junto quando ele assenta. Sem isto as tripas ficariam se
/// debatendo depois que o corpo já parou.
[DisallowMultipleComponent]
public class Viscera : MonoBehaviour
{
}
