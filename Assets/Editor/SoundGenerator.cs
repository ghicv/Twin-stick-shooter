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
        Save("Assets/Audio/UI/RouletteTick.wav", RouletteTick());
        Save("Assets/Audio/UI/RouletteWin.wav", RouletteWin());
        Save("Assets/Audio/UI/RoundWin.wav", Notes(new[] { 523f, 659f, 784f, 1047f }, 0.07f, 0.35f));
        Save("Assets/Audio/UI/RoundDraw.wav", Notes(new[] { 392f, 392f }, 0.12f, 0.2f));
        Save("Assets/Audio/UI/Victory.wav", Notes(new[] { 523f, 523f, 523f, 659f, 784f, 659f, 784f, 1047f }, 0.11f, 0.6f));
        Save("Assets/Audio/UI/Defeat.wav", Notes(new[] { 494f, 440f, 392f, 330f }, 0.18f, 0.5f));
        Save("Assets/Audio/Boom.wav", Boom());
        Save("Assets/Audio/Boing.wav", Boing());
        Save("Assets/Audio/Thunder.wav", Thunder());
        Save("Assets/Audio/SwordSwoosh.wav", SwordSwoosh());
        Save("Assets/Audio/SwordClang.wav", SwordClang());
        Save("Assets/Audio/SwordDraw.wav", SwordDraw());
        AssetDatabase.Refresh();
    }

    // A sword slash: air torn by the blade, a hiss of noise whose pitch sweeps down fast, with a soft whistle on top.
    private static float[] SwordSwoosh()
    {
        int length = (int)(Rate * 0.22f);
        var data = new float[length];
        var random = new System.Random(6);
        float bright = 0f, dark = 0f, phase = 0f;
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / Rate;
            float white = (float)random.NextDouble() * 2f - 1f;
            float sweep = Mathf.Lerp(0.55f, 0.08f, t / 0.22f); // the band slides down: "fwip"
            bright = Mathf.Lerp(bright, white, sweep);
            dark = Mathf.Lerp(dark, bright, sweep * 0.35f);
            float band = bright - dark; // band-passed noise
            phase += Mathf.Lerp(1900f, 700f, t / 0.22f) / Rate;
            float whistle = Mathf.Sin(phase * 2f * Mathf.PI) * 0.12f;
            float envelope = Mathf.Min(1f, t / 0.035f) * Mathf.Exp(-Mathf.Max(0f, t - 0.035f) * 22f);
            data[i] = (band * 1.6f + whistle) * envelope * 0.8f;
        }
        return data;
    }

    // A bullet cut in two: a bright metal "keng" (clashing, out-of-tune partials ringing down) on a sharp click.
    private static float[] SwordClang()
    {
        int length = (int)(Rate * 0.5f);
        var data = new float[length];
        var random = new System.Random(7);
        float[] partials = { 1180f, 1735f, 2410f, 3150f, 4420f };
        float[] decays = { 7f, 9f, 12f, 16f, 24f };
        float[] levels = { 0.5f, 0.38f, 0.3f, 0.2f, 0.12f };
        float click = 0f;
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / Rate;
            float ring = 0f;
            for (int p = 0; p < partials.Length; p++)
                ring += Mathf.Sin(2f * Mathf.PI * partials[p] * t * (1f + 0.002f * Mathf.Sin(t * 40f))) * levels[p] * Mathf.Exp(-t * decays[p]);
            click = Mathf.Lerp(click, (float)random.NextDouble() * 2f - 1f, 0.8f);
            float hit = click * Mathf.Exp(-t * 300f);
            data[i] = (ring * 0.75f + hit * 0.6f) * Mathf.Min(1f, t / 0.0008f) * 0.75f;
        }
        return data;
    }

    // The sword drawn: steel scraping out of the sheath ("shhhing"), a rising hiss that ends in a clear ring.
    private static float[] SwordDraw()
    {
        int length = (int)(Rate * 0.55f);
        var data = new float[length];
        var random = new System.Random(8);
        float bright = 0f, dark = 0f;
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / Rate;
            float white = (float)random.NextDouble() * 2f - 1f;
            bright = Mathf.Lerp(bright, white, 0.5f);
            dark = Mathf.Lerp(dark, bright, 0.12f);
            float scrape = (bright - dark) * Mathf.Min(1f, t / 0.12f) * Mathf.Exp(-Mathf.Max(0f, t - 0.16f) * 30f);
            float ringTime = Mathf.Max(0f, t - 0.14f);
            float ring = (Mathf.Sin(2f * Mathf.PI * 2640f * ringTime) * 0.5f + Mathf.Sin(2f * Mathf.PI * 3960f * ringTime) * 0.25f)
                         * (t > 0.14f ? Mathf.Exp(-ringTime * 6f) * Mathf.Min(1f, ringTime / 0.004f) : 0f);
            data[i] = (scrape * 1.1f + ring * 0.6f) * 0.7f;
        }
        return data;
    }

    // The Thunder core's bolt: a sharp bright crack, then a short low rumble.
    private static float[] Thunder()
    {
        int length = (int)(Rate * 0.45f);
        var data = new float[length];
        var random = new System.Random(5);
        float crackNoise = 0f, rumbleNoise = 0f;
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / Rate;
            float white = (float)random.NextDouble() * 2f - 1f;
            crackNoise = Mathf.Lerp(crackNoise, white, 0.7f);
            rumbleNoise = Mathf.Lerp(rumbleNoise, white, 0.04f);
            float crack = crackNoise * Mathf.Exp(-t * 40f);
            float rumble = rumbleNoise * 3f * Mathf.Exp(-t * 7f) * Mathf.Min(1f, t / 0.03f);
            data[i] = (crack * 0.7f + rumble * 0.6f) * Mathf.Min(1f, t / 0.001f) * 0.7f;
        }
        return data;
    }

    // An explosive barrel: a deep crunchy blast (noise falling in pitch) with a low thump.
    private static float[] Boom()
    {
        int length = (int)(Rate * 0.7f);
        var data = new float[length];
        var random = new System.Random(4);
        float noise = 0f, phase = 0f;
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / Rate;
            float smoothing = Mathf.Lerp(0.5f, 0.05f, t / 0.7f); // the rumble darkens as it fades
            noise = Mathf.Lerp(noise, (float)random.NextDouble() * 2f - 1f, smoothing);
            phase += Mathf.Lerp(90f, 40f, t / 0.7f) / Rate;
            float thump = Triangle(phase) * Mathf.Exp(-t * 7f);
            data[i] = (noise * Mathf.Exp(-t * 5f) * 0.9f + thump * 0.6f) * Mathf.Min(1f, t / 0.002f) * 0.8f;
        }
        return data;
    }

    // A jump pad: a springy "boing", a tone sliding up with a wobble.
    private static float[] Boing()
    {
        int length = (int)(Rate * 0.25f);
        var data = new float[length];
        float phase = 0f;
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / Rate;
            float frequency = Mathf.Lerp(220f, 660f, t / 0.25f) * (1f + 0.06f * Mathf.Sin(t * 2f * Mathf.PI * 18f));
            phase += frequency / Rate;
            data[i] = Triangle(phase) * Mathf.Exp(-t * 9f) * Mathf.Min(1f, t / 0.002f) * 0.5f;
        }
        return data;
    }

    // A little chiptune tune: the notes one after another (Step seconds each), the last one ringing for Tail more.
    private static float[] Notes(float[] notes, float step, float tail)
    {
        int length = (int)(Rate * (step * notes.Length + tail));
        var data = new float[length];
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / Rate;
            int note = Mathf.Min(notes.Length - 1, (int)(t / step));
            float local = t - note * step;
            float square = Mathf.Repeat(notes[note] * t, 1f) < 0.5f ? 1f : -1f; // a soft square for the 8-bit sound
            float tone = Triangle(notes[note] * t) * 0.7f + square * 0.12f;
            float fade = note == notes.Length - 1 ? Mathf.Exp(-local * 5f) : Mathf.Exp(-local * 14f);
            data[i] = tone * fade * Mathf.Min(1f, local / 0.003f) * 0.45f;
        }
        return data;
    }

    // The map roulette passing a card: a tiny wooden click.
    private static float[] RouletteTick()
    {
        int length = (int)(Rate * 0.03f);
        var data = new float[length];
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / Rate;
            float tone = Triangle(1500f * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * 900f * t) * 0.4f;
            data[i] = tone * Mathf.Exp(-t * 180f) * Mathf.Min(1f, t / 0.0008f) * 0.5f;
        }
        return data;
    }

    // The map roulette stopped: three rising notes.
    private static float[] RouletteWin()
    {
        float[] notes = { 784f, 988f, 1319f };
        const float step = 0.08f;
        int length = (int)(Rate * (step * notes.Length + 0.25f));
        var data = new float[length];
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / Rate;
            int note = Mathf.Min(notes.Length - 1, (int)(t / step));
            float local = t - note * step;
            float tone = Triangle(notes[note] * t) * 0.6f + Mathf.Sin(2f * Mathf.PI * notes[note] * 2f * t) * 0.2f;
            float fade = note == notes.Length - 1 ? Mathf.Exp(-local * 9f) : Mathf.Exp(-local * 20f);
            data[i] = tone * fade * Mathf.Min(1f, local / 0.003f) * 0.5f;
        }
        return data;
    }

    private static float Triangle(float phase) => 4f * Mathf.Abs(Mathf.Repeat(phase, 1f) - 0.5f) - 1f;

    // A shot: a short "pew" that drops in pitch, a crack of noise at the start and a low punch under it, so it
    // sounds solid without getting harsh.
    private static float[] Shoot()
    {
        int length = (int)(Rate * 0.12f);
        var data = new float[length];
        var random = new System.Random(1);
        float phase = 0f, punchPhase = 0f, noise = 0f;
        for (int i = 0; i < length; i++)
        {
            float t = (float)i / Rate;
            float frequency = Mathf.Lerp(620f, 240f, t / 0.09f);
            phase += frequency / Rate;
            float tone = Triangle(phase) * Mathf.Exp(-t * 38f);
            noise = Mathf.Lerp(noise, (float)random.NextDouble() * 2f - 1f, 0.3f); // low-passed noise
            float crack = noise * Mathf.Exp(-t * 110f) * 0.7f;
            punchPhase += Mathf.Lerp(150f, 60f, t / 0.12f) / Rate;
            float punch = Mathf.Sin(punchPhase * 2f * Mathf.PI) * Mathf.Exp(-t * 30f) * 0.7f;
            float attack = Mathf.Min(1f, t / 0.0015f);
            data[i] = (tone * 0.6f + crack + punch) * attack * 0.7f;
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
