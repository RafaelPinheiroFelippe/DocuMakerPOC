using FFmpeg.NET;
using Microsoft.Extensions.Options;
using FileNotFoundException = System.IO.FileNotFoundException;

namespace DocuMakerPOC.TransactionScripts;

public class GenerateC4Script
{
    private readonly string? OpenAiToken;
    private const string SpeechToTextUrl = "https://api.openai.com/v1/audio/transcriptions";
    private const string SpeechToTextModel = "whisper-1";
    
    private const string AudioFolderName = "AudioFiles";
    private const string FfmpegPath = "C:\\ProgramData\\chocolatey\\bin\\ffmpeg.exe";

    public GenerateC4Script(IConfiguration configuration)
    {
        OpenAiToken = configuration["OpenAIToken"];
    }
    
    public async Task<bool> RunAsync(string videoPath)
    {
        try
        {
            var audioDirectory = GetAudioPath(videoPath);

            await ExtractAudioAsync(videoPath, audioDirectory);

            var transcription = await TranscriptAudioFile(audioDirectory);

            //proccess??

            //generare structured docs

            return true;
        }
        catch (Exception _)
        {
            return false;
        }
    }

    private string GetAudioPath(string videoPath)
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

    private async Task<string> TranscriptAudioFile(string audioPath)
    {
        using var httpClient = new HttpClient();
        
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {OpenAiToken}");

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