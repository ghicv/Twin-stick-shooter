using UnityEditor;
using UnityEngine;

// Tools > Generate Sounds: makes the soft in-game sounds into Assets/Audio (overwriting Shoot and HitWall).
// Frequent sounds stay short and gentle: triangle / sine tones with a quick fade, a little filtered noise for bite.
public static class SoundGenerator
{
    private const int Rate = 44100;

    [MenuItem("Tools/Generate Sounds")]
    public static void GenerateAll()
    {
        Save("Assets/Audio/Shoot.wav", Shoot());
        Save("Assets/Audio/HitWall.wav", WallTick());
        Save("Assets/Audio/HitConfirm.wav", HitConfirm());
        Save("Assets/Audio/HurtThud.wav", HurtThud());
        AssetDatabase.Refresh();
    }

    private static float Triangle(float phase) => 4f * Mathf.Abs(Mathf.Repeat(phase, 1f) - 0.5f) - 1f;

    // A shot: a short "pew" that drops in pitch, with a soft click of noise at the start.
    private static float[] Shoot()
    {
        int length = (int)(Rate * 0.09f);
        var data = new float[length];
        var random = new System.Random(1);
        float phase = 0f, noise = 0f;
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / Rate;
            float frequency = Mathf.Lerp(620f, 260f, t / 0.09f);
            phase += frequency / Rate;
            float tone = Triangle(phase) * Mathf.Exp(-t * 38f);
            noise = Mathf.Lerp(noise, (float)random.NextDouble() * 2f - 1f, 0.25f); // low-passed noise
            float click = noise * Mathf.Exp(-t * 160f) * 0.6f;
            float attack = Mathf.Min(1f, t / 0.002f);
            data[i] = (tone * 0.7f + click) * attack * 0.8f;
        }
        return data;
    }

    // A bullet on a wall: a tiny dull tick.
    private static float[] WallTick()
    {
        int length = (int)(Rate * 0.045f);
        var data = new float[length];
        var random = new System.Random(2);
        float noise = 0f;
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / Rate;
            noise = Mathf.Lerp(noise, (float)random.NextDouble() * 2f - 1f, 0.18f);
            float tone = Mathf.Sin(2f * Mathf.PI * 340f * t);
            data[i] = (noise * 0.7f + tone * 0.3f) * Mathf.Exp(-t * 90f) * Mathf.Min(1f, t / 0.001f) * 0.8f;
        }
        return data;
    }

    // "I hit someone": a short bright tick, two quick notes.
    private static float[] HitConfirm()
    {
        int length = (int)(Rate * 0.07f);
        var data = new float[length];
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / Rate;
            float frequency = t < 0.025f ? 1320f : 1760f;
            float local = t < 0.025f ? t : t - 0.025f;
            float tone = Mathf.Sin(2f * Mathf.PI * frequency * t) * 0.8f + Triangle(frequency * t) * 0.2f;
            data[i] = tone * Mathf.Exp(-local * 70f) * Mathf.Min(1f, local / 0.0015f) * 0.6f;
        }
        return data;
    }

    // "I got hit": a low soft thud.
    private static float[] HurtThud()
    {
        int length = (int)(Rate * 0.16f);
        var data = new float[length];
        var random = new System.Random(3);
        float phase = 0f, noise = 0f;
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / Rate;
            float frequency = Mathf.Lerp(170f, 70f, t / 0.16f);
            phase += frequency / Rate;
            noise = Mathf.Lerp(noise, (float)random.NextDouble() * 2f - 1f, 0.08f);
            float body = Triangle(phase) * Mathf.Exp(-t * 22f);
            float hit = noise * Mathf.Exp(-t * 60f) * 0.8f;
            data[i] = (body * 0.8f + hit) * Mathf.Min(1f, t / 0.003f) * 0.85f;
        }
        return data;
    }

    private static void Save(string path, float[] data)
    {
        using (var stream = new System.IO.FileStream(path, System.IO.FileMode.Create))
        using (var writer = new System.IO.BinaryWriter(stream))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + data.Length * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1); // PCM
            writer.Write((short)1); // mono
            writer.Write(Rate);
            writer.Write(Rate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(data.Length * 2);
            foreach (float sample in data)
                writer.Write((short)(Mathf.Clamp(sample, -1f, 1f) * 32000f));
        }
    }
}
