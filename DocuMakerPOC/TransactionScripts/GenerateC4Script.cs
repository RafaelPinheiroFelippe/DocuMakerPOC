using FFmpeg.NET;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using FileNotFoundException = System.IO.FileNotFoundException;

namespace DocuMakerPOC.TransactionScripts;

public class GenerateC4Script
{
    private readonly IDistributedCache _cache;
    private readonly string? _openAiToken;
    
    private const string SpeechToTextUrl = "https://api.openai.com/v1/audio/transcriptions";
    private const string SpeechToTextModel = "whisper-1";
    
    private const string AudioFolderName = "AudioFiles";
    private const string FfmpegPath = "C:\\ProgramData\\chocolatey\\bin\\ffmpeg.exe";

    public GenerateC4Script(IConfiguration configuration, IDistributedCache cache)
    {
        _cache = cache;
        _openAiToken = configuration["OpenAIToken"];
    }
    
    public async Task<bool> RunAsync(string videoPath)
    {
        try
        {   
            var audioDirectory = CreateAudioDirectory(videoPath);

            await ExtractAudioAsync(videoPath, audioDirectory);

            var transcription = await GetTranscription(audioDirectory);

            //proccess??

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

        return Path.Combine(currentAudioDirectoryPath, $"{Path.GetFileNameWithoutExtension(videoPath)}.mp3");
    }

    private async Task ExtractAudioAsync(string videoPath, string audioDirectory)
    {
        var ffmpeg = new Engine(FfmpegPath);
        var inputFile = new InputFile(videoPath);
        var outputFile = new OutputFile(audioDirectory);

        await ffmpeg.ConvertAsync(inputFile,
            outputFile,
            new ConversionOptions { ExtraArguments = "-vn -ar 44100 -ac 2 -b:a 192k" },
            default);
    }

    private async Task<string> GetTranscription(string audioPath)
    {
        var fileName = Path.GetFileNameWithoutExtension(audioPath);

        var cachedTranscription = await _cache.GetStringAsync(fileName);
        if (cachedTranscription is not null) 
            return cachedTranscription;
        
        var transcript = await TranscriptAudioFile(audioPath);
        await _cache.SetStringAsync(fileName, transcript);
        
        return transcript;
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