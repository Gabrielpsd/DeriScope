using System.Speech.Synthesis;
using static SkiaSharp.HarfBuzz.SKShaper;

public class SpeechService
{
    private readonly SpeechSynthesizer _synthesizer;


    public SpeechService()
    {
        _synthesizer = new SpeechSynthesizer();

        // Configure the audio output.
        _synthesizer.SetOutputToDefaultAudioDevice();
    }

    public void SpeakAsync(string message)
    {
        // Speak a string, synchronously
        _synthesizer.SpeakAsync(message);
    }
}