using System.Collections.Generic;
using UnityEngine;

/// Canal global de ruido do prototipo.
/// Quem faz barulho chama Noise.Emit. Quem escuta implementa INoiseListener,
/// ou assina o evento Emitted se so quer reagir (audio, VFX, a entidade depois).
public static class Noise
{
    public static readonly List<INoiseListener> Listeners = new List<INoiseListener>();

    /// Disparado a cada ruido. position: onde nasceu. radius: quao longe chega, em metros.
    /// A origem existe porque quem REAGE ao ruido depende dela: o guarda investiga
    /// qualquer barulho, mas o audio de passo so vale para o jogador.
    public static event System.Action<Vector3, float, NoiseSource> Emitted;

    /// Estado estatico nao sobrevive a um play limpo. Se o domain reload for desligado
    /// pra iterar mais rapido, sem isso a lista acumula lixo do play anterior.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetState()
    {
        Listeners.Clear();
        Emitted = null;
    }

    public static void Emit(Vector3 position, float radius)
        => Emit(position, radius, NoiseSource.Jogador);

    public static void Emit(Vector3 position, float radius, NoiseSource source)
    {
        if (DevMode.Fantasma && source == NoiseSource.Jogador) return;
        for (int i = Listeners.Count - 1; i >= 0; i--)
        {
            if (Listeners[i] == null) { Listeners.RemoveAt(i); continue; }
            Listeners[i].OnNoise(position, radius);
        }

        Emitted?.Invoke(position, radius, source);

        Debug.DrawLine(position, position + Vector3.up * 2f, Color.yellow, 0.4f);
    }
}

/// De onde veio o barulho. Guardas ignoram a origem; audio e VFX nao.
public enum NoiseSource { Jogador, Isca, Entidade, Outro }

public interface INoiseListener
{
    void OnNoise(Vector3 position, float radius);
}
