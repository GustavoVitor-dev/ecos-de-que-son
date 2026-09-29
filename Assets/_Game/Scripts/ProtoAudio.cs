using UnityEngine;

/// Audio de rascunho, sintetizado em runtime.
/// Mesmo espirito da caixa cinza: existe pra o sistema ser testavel hoje,
/// e pra ser trocado por audio de verdade depois sem mexer em codigo.
public static class ProtoAudio
{
    const int Rate = 44100;

    /// Passo: estalo de ruido filtrado com um baque grave por baixo.
    public static AudioClip Footstep(string name, float pitch = 1f, float duration = 0.16f, int seed = 1)
    {
        int n = Mathf.CeilToInt(Rate * duration);
        var data = new float[n];
        var rnd = new System.Random(seed);
        float low = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float env = Mathf.Exp(-13f * t);

            float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
            low = Mathf.Lerp(low, noise, 0.25f);

            float thump = Mathf.Sin(2f * Mathf.PI * 68f * pitch * i / Rate) * Mathf.Exp(-25f * t);

            data[i] = Mathf.Clamp(low * 0.75f * env + thump * 0.55f, -1f, 1f);
        }

        var clip = AudioClip.Create(name, n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// Grunhido curto e gutural: o guarda reagindo de forma audivel.
    public static AudioClip Grunt(string name, float freq = 130f, float duration = 0.32f, int seed = 7)
    {
        int n = Mathf.CeilToInt(Rate * duration);
        var data = new float[n];
        var rnd = new System.Random(seed);
        float low = 0f;

        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t)) * Mathf.Exp(-2.2f * t);

            float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
            low = Mathf.Lerp(low, noise, 0.08f);

            float glide = freq * (1f - 0.25f * t);
            float voice = Mathf.Sin(2f * Mathf.PI * glide * i / Rate)
                        + 0.4f * Mathf.Sin(4f * Mathf.PI * glide * i / Rate);

            data[i] = Mathf.Clamp((voice * 0.35f + low * 1.6f) * env, -1f, 1f);
        }

        var clip = AudioClip.Create(name, n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    /// Drone de ambiente em loop. As pontas sao cruzadas pra nao estalar na emenda.
    public static AudioClip Drone(string name, float duration = 6f, int seed = 3)
    {
        int n = Mathf.CeilToInt(Rate * duration);
        var data = new float[n];
        var rnd = new System.Random(seed);
        float low = 0f, lower = 0f;

        for (int i = 0; i < n; i++)
        {
            float noise = (float)(rnd.NextDouble() * 2.0 - 1.0);
            low = Mathf.Lerp(low, noise, 0.010f);
            lower = Mathf.Lerp(lower, low, 0.010f);

            float breath = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 0.07f * i / Rate);
            data[i] = Mathf.Clamp(lower * 26f * breath, -1f, 1f);
        }

        // crossfade das pontas pro loop nao estalar
        int fade = Mathf.Min(Rate / 2, n / 4);
        for (int i = 0; i < fade; i++)
        {
            float k = (float)i / fade;
            data[i] = Mathf.Lerp(data[n - fade + i], data[i], k);
        }

        var clip = AudioClip.Create(name, n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
