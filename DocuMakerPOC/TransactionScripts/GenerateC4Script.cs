using FFmpeg.NET;
using Firebase.Database;
using Microsoft.SemanticKernel;
using Newtonsoft.Json;
using static System.String;

namespace DocuMakerPOC.TransactionScripts;

public class GenerateC4Script
{
    private readonly FirebaseClient _firebaseClient;
    private readonly string _openAiToken;
    private const string LlmModel = "gpt-3.5-turbo-16k";

    private const string SpeechToTextUrl = "https://api.openai.com/v1/audio/transcriptions";
    private const string SpeechToTextModel = "whisper-1";

    private const string AudioFolderName = "AudioFiles";
    private const string FfmpegPath = "C:\\ProgramData\\chocolatey\\bin\\ffmpeg.exe";

    public GenerateC4Script(IConfiguration configuration, FirebaseClient firebaseClient)
    {
        _firebaseClient = firebaseClient;
        _openAiToken = configuration["OpenAI:Auth"] ?? Empty;
    }

    private record AudioSlice(string Name, string Path, int Order);

    private record TranscriptionSlice(string Name, string Content, int Order);

    private record DocumentChunk(string Name, string TableOfContents, string Content, string RawText, int Order);

    public async Task<bool> RunAsync(string videoPath)
    {
        try
        {
            //TODO implement transcription storage with firebase, create wrapper for deserialization 
            var a = new
            {
                poggers = "aaaa"
            };
            
            var test = await _firebaseClient
                .Child("docs")
                .PostAsync(JsonConvert.SerializeObject(a));
            
            
            var audioDirectory = CreateAudioDirectory(videoPath);

            var audioSlices = await ExtractAndSliceAudio(videoPath, audioDirectory);

            var transcriptions = new List<TranscriptionSlice>();
            foreach (var audioSlice in audioSlices)
                transcriptions.Add(await GetTranscription(audioSlice));

            var documentationChunks = await ProcessTranscriptions(transcriptions);

            //var firebaseResult = await _firebaseClient.PushAsync("docs", documentationChunks);
            //generate structured docs

            return true;
        }
        catch (Exception _)
        {
            return false;
        }
    }

    private async Task<List<DocumentChunk>> ProcessTranscriptions(List<TranscriptionSlice> transcriptionSlices)
    {
        var tableOfContents = Empty;

        var kernel = Kernel.Builder
            .WithOpenAIChatCompletionService(LlmModel, _openAiToken)
            .Build();

        var documentChunks = new List<DocumentChunk>();
        foreach (var currentSlice in transcriptionSlices)
        {
            var (rawText, processedText) = await ProcessTranscription(transcriptionSlices, currentSlice, kernel);
            
            var functionDefinition = @$"
[GENERATION RULES]
[PROMPT GENERATION RULES]
GENERATE A TABLE OF CONTENTS ABOUT THE DOCUMENTATION CHUNK'S SUBJECT
FOR EXAMPLE IF IT IS THE DOCUMENTATION OF A SOFTWARE, GENERATE THE SOFTWARE'S DOCUMENTATION'S TABLE OF CONTENTS BASED ON THE DOCUMENTATION CHUNK DATA
BE ORGANIZED AND CONCISE
USE GOOD DOCUMENTATION PRACTICES FOR THE SPECIFIC SUBJECT, FOR EXAMPLE IF THE SUBJECT OF THE DOCUMENTATION IS A SOFTWARE UTILIZE SOFTWARE DOCUMENTATION TECHNIQUES  
YOU WILL RECEIVE THE DOCUMENTATION CHUNK IN PLAIN TEXT
YOU MAY WILL RECEIVE AN ALREADY CREATED TABLE OF CONTENTS, IF THIS DOES HAPPEN, YOU MUST INCREMENT THE RECEIVED TABLE WITH THE INFORMATION CONTAINED IN THE DOCUMENTATION CHUNK
RETURN THE TABLE OF DOCUMENTS IN MARKDOWN (.md) FORMAT
THE DOCUMENTATION CHUNK IS ONLY A SMALL PART OF THE FULL DOCUMENTATION, KEEP THIS IN MIND WHEN UPDATING OR CREATING THE TABLE OF CONTENTS

Already existing table of documents:
--START OF THE TABLE
{tableOfContents}
--END OF THE TABLE

Generate or update table of contents based on the following documentation chunk
{processedText}
+++++
";

            var documentToTableOfContentsSkill = kernel.CreateSemanticFunction(functionDefinition, maxTokens: 11000, temperature: 0.0, topP: 1);
        
            var documentToTableOfContents =
                await documentToTableOfContentsSkill.InvokeAsync();

            tableOfContents = documentToTableOfContents.Result;
            
            documentChunks.Add(new DocumentChunk(
                currentSlice.Name,
                documentToTableOfContents.Result,
                processedText,
                rawText,
                currentSlice.Order));
        }

        return documentChunks;
    }

    private static async Task<(string RawText, string processedText)> ProcessTranscription(
        List<TranscriptionSlice> transcriptionSlices,
        TranscriptionSlice currentSlice,
        IKernel kernel)
    {
        const int overlapCharactersCount = 500;

        var previousContent = transcriptionSlices.IndexOf(currentSlice) != 0
            ? transcriptionSlices[transcriptionSlices.IndexOf(currentSlice) - 1].Content
            : Empty;

        var previousContentOverlap = previousContent.Length > overlapCharactersCount
            ? previousContent[^overlapCharactersCount..]
            : previousContent;

        var nextContent = currentSlice != transcriptionSlices.Last()
            ? transcriptionSlices[transcriptionSlices.IndexOf(currentSlice) + 1].Content
            : Empty;

        var nextContentOverlap = nextContent.Length > overlapCharactersCount
            ? nextContent[..overlapCharactersCount]
            : nextContent;

        var rawText = $"{previousContentOverlap}{currentSlice.Content}{nextContentOverlap}";

        // TODO transform into better transcription
        // TODO return in portuguese
        const string FunctionDefinition = @"
[GENERATION RULES]
GENERATE A DOCUMENT WITH THE TRANSCRIPTIONS CONTENT
BE ORGANIZED AND CONCISE  
YOU WILL RECEIVE THE TRANSCRIPTION IN PLAIN TEXT, IT IS ONLY A CHUNK OF A FULL TRANSCRIPTION
IGNORE CONTENTS OF THE TRANSCRIPTION THAT DONT FIT IN A DOCUMENTATION, FOR EXAMPLE IF YOU RECEIVE A TRANSCRIPTION OF SOMEONE DESCRIBING HOW A SOFTWARE WORKS, IGNORE JOKES, UNRELATED COMMENTARIES, ETC 
RETURN THE DOCUMENT IN MARKDOWN (.md) FORMAT

Generate document based on the following transcription
{{$input}}
+++++
";

        var transcriptionToDocumentSkill = kernel.CreateSemanticFunction(FunctionDefinition, maxTokens: 11000, temperature: 0.0, topP: 1);
        
        var transcriptionToDocument =
            await transcriptionToDocumentSkill.InvokeAsync(rawText);

        return (rawText, transcriptionToDocument.Result);
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
        var segmentNumber = 1;
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
        // var cachedTranscription = await _cache.GetStringAsync(fileName);
        // if (cachedTranscription is not null)
        //     return new TranscriptionSlice(audioSlice.Name, cachedTranscription, audioSlice.Order);

        var transcription = await TranscriptAudioFile(audioSlice.Path);
        //await _cache.SetStringAsync(fileName, transcription);

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
        formData.Add(new StringContent("text"), "response_format");

        var response = await httpClient.PostAsync(SpeechToTextUrl, formData);
        var responseBody = await response.Content.ReadAsStringAsync();

        // TODO Handle the response
        return responseBody;
    }
}