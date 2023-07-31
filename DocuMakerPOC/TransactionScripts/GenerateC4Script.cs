using FFmpeg.NET;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using NAudio.Wave;
using FileNotFoundException = System.IO.FileNotFoundException;

namespace DocuMakerPOC.TransactionScripts;

public class GenerateC4Script
{
    private readonly IDistributedCache _cache;
    private readonly string _openAiToken;
    private const string LlmModel = "gpt-3.5-turbo";

    private const string SpeechToTextUrl = "https://api.openai.com/v1/audio/transcriptions";
    private const string SpeechToTextModel = "whisper-1";

    private const string AudioFolderName = "AudioFiles";
    private const string FfmpegPath = "C:\\ProgramData\\chocolatey\\bin\\ffmpeg.exe";

    public GenerateC4Script(IConfiguration configuration, IDistributedCache cache)
    {
        _cache = cache;
        _openAiToken = configuration["OpenAIToken"] ?? string.Empty;
    }

    private record AudioSlice(string Name, string Path, int Order);

    private record TranscriptionSlice(string Name, string Value, int Order);

    public async Task<bool> RunAsync(string videoPath)
    {
        try
        {
            var audioDirectory = CreateAudioDirectory(videoPath);

            var audioSlices = await ExtractAndSliceAudio(videoPath, audioDirectory);

            var transcriptions = new List<TranscriptionSlice>();
            foreach (var audioSlice in audioSlices)
                transcriptions.Add(await GetTranscription(audioSlice));


            // var transcriptionTasks = audioSlices.Select(GetTranscription);
            //
            // var transcriptions = await Task.WhenAll(transcriptionTasks);

            var kernel = Kernel.Builder
                .WithOpenAIChatCompletionService(LlmModel, _openAiToken)
                .Build();

            var skillsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Services", "SemanticKernel", "Skills");

            // TODO trancript slices
            var imageGenerationSkill = kernel.ImportSemanticSkillFromDirectory(skillsDirectory, "DocumentationSkill");

            //var doc = await imageGenerationSkill["TranscriptionToDocumentation"].InvokeAsync(transcription);

            //generare structured docs

            return true;
        }
        catch (Exception _)
        {
            return false;
        }
    }

    private string CreateAudioDirectory(string videoPath)
    {
        var generalAudioDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), AudioFolderName);

        var videoFileName = Path.GetFileNameWithoutExtension(videoPath);
        var currentAudioDirectoryPath = Path.Combine(generalAudioDirectoryPath, videoFileName);

        Directory.CreateDirectory(currentAudioDirectoryPath);

        if (!File.Exists(videoPath)) throw new FileNotFoundException("File not found", currentAudioDirectoryPath);

        return currentAudioDirectoryPath;
    }

    private async Task<string> ExtractAudioAsync(string videoPath, string audioDirectory)
    {
        var audioPath = Path.Combine(audioDirectory, $"{Path.GetFileNameWithoutExtension(videoPath)}.mp3");

        var ffmpeg = new Engine(FfmpegPath);
        var inputFile = new InputFile(videoPath);
        var outputFile =
            new OutputFile(Path.Combine(audioDirectory, $"{Path.GetFileNameWithoutExtension(videoPath)}.mp3"));

        await ffmpeg.ConvertAsync(inputFile,
            outputFile,
            new ConversionOptions { ExtraArguments = "-vn -ar 44100 -ac 2 -b:a 192k" },
            default);

        return audioPath;
    }

    private async Task<List<AudioSlice>> ExtractAndSliceAudio(string videoFilePath, string outputFolderPath)
    {
        const int minutesPerSegment = 10;
        List<AudioSlice> slices = new();

        var audioPath = await ExtractAudioAsync(videoFilePath, outputFolderPath);

        var engine = new Engine(FfmpegPath);
        
        var inputFile = new InputFile(audioPath);
        var metadata = await engine.GetMetaDataAsync(inputFile, default);
        var totalDuration = metadata.Duration;
        int segmentNumber = 1;
        for (var start = TimeSpan.Zero; start < totalDuration; start += TimeSpan.FromMinutes(minutesPerSegment))
        {
            var end = start + TimeSpan.FromMinutes(minutesPerSegment);
            if (end > totalDuration)
            {
                end = totalDuration;
            }

            var sliceName = $"{Path.GetFileNameWithoutExtension(videoFilePath)}_{segmentNumber}.mp3";
            var outputPath = Path.Combine(outputFolderPath, sliceName);

            var options = new ConversionOptions
            {
                Seek = start,
                ExtraArguments = $"-t {end - start}"
            };

            var outputFile = new OutputFile(outputPath);

            await engine.ConvertAsync(inputFile, outputFile, options, default);

            var slice = new AudioSlice(sliceName, outputPath, segmentNumber);
            slices.Add(slice);
            segmentNumber++;
        }

        return slices;
    }


    private async Task<TranscriptionSlice> GetTranscription(AudioSlice audioSlice)
    {
        var fileName = audioSlice.Name;

        var cachedTranscription = await _cache.GetStringAsync(fileName);
        if (cachedTranscription is not null)
            return new TranscriptionSlice(audioSlice.Name, cachedTranscription, audioSlice.Order);

        var transcription = await TranscriptAudioFile(audioSlice.Path);
        await _cache.SetStringAsync(fileName, transcription);

        return new TranscriptionSlice(audioSlice.Name, transcription, audioSlice.Order);
    }

    private async Task<string> TranscriptAudioFile(string audioPath)
    {
        using var httpClient = new HttpClient();

        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_openAiToken}");

        using var formData = new MultipartFormDataContent();
        await using var fileStream = new FileStream(audioPath, FileMode.Open, FileAccess.Read);

        var fileContent = new StreamContent(fileStream);
        formData.Add(fileContent, "file", Path.GetFileName(audioPath));
        formData.Add(new StringContent(SpeechToTextModel), "model");

        var response = await httpClient.PostAsync(SpeechToTextUrl, formData);
        var responseBody = await response.Content.ReadAsStringAsync();

        // TODO Handle the response
        return responseBody;
    }
}