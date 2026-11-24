using System.Speech.Recognition;
using System.Speech.Synthesis;

namespace SpeechApp;

internal sealed class SpeechSession : IDisposable
{
    private SpeechSynthesizer? speaker;
    internal SpeechSynthesizer Speaker => speaker ??= new SpeechSynthesizer();
    internal SpeechRecognitionEngine? Recognizer { get; private set; }
    private TaskCompletionSource? completion;

    internal async Task RecognizeAsync(RecognizerInfo language, string? wav, Action<string> onText)
    {
        using var engine = new SpeechRecognitionEngine(language);
        engine.LoadGrammar(new DictationGrammar());
        if (wav is null) engine.SetInputToDefaultAudioDevice();
        else engine.SetInputToWaveFile(wav);
        var result = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        engine.SpeechRecognized += (_, e) => onText(e.Result.Text);
        engine.RecognizeCompleted += (_, e) =>
        {
            if (e.Error is { } error) result.TrySetException(error);
            else result.TrySetResult();
        };
        Recognizer = engine;
        completion = result;
        try
        {
            engine.RecognizeAsync(RecognizeMode.Multiple);
            await result.Task;
        }
        finally
        {
            Recognizer = null;
            completion = null;
        }
    }

    internal void StopRecognition() => Recognizer?.RecognizeAsyncCancel();

    public void Dispose()
    {
        StopRecognition();
        completion?.TrySetCanceled();
        speaker?.Dispose();
    }
}
