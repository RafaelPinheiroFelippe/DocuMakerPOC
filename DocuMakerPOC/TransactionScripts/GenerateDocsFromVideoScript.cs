using DocuMakerPOC.DTOs;
using DocuMakerPOC.Prompts;
using FFmpeg.NET;
using Firebase.Database;
using Firebase.Database.Query;
using Microsoft.SemanticKernel;
using static System.String;

namespace DocuMakerPOC.TransactionScripts;

public class GenerateDocsFromVideoScript
{
    private readonly FirebaseClient _firebaseClient;
    private readonly string _openAiToken;
    private GenerateDocsFromVideoPrompts? _prompts;
    private string _fileName;
    private readonly IKernel _kernel;
    private const string LlmModel = "gpt-3.5-turbo-16k";

    private const string SpeechToTextUrl = "https://api.openai.com/v1/audio/transcriptions";
    private const string SpeechToTextModel = "whisper-1";

    private const string AudioFolderName = "AudioFiles";
    private const string FfmpegPath = """C:\ProgramData\chocolatey\bin\ffmpeg.exe""";

    private const string GeneralRulesPrompt = """
                                              [GENERAL RULES]
                                              YOUR RETURN MUST BE IN PT-BR
                                              [BANNED TERMS]
                                              "THIS DOCUMENT", "THIS TRANSCRIPTION" SWEARS OR BAD WORDS
                                              """;

    public GenerateDocsFromVideoScript(IConfiguration configuration, FirebaseClient firebaseClient)
    {
        _firebaseClient = firebaseClient;
        _openAiToken = configuration["OpenAI:Auth"] ?? Empty;

        _kernel = Kernel.Builder
            .WithOpenAIChatCompletionService(LlmModel, _openAiToken)
            .Build();
    }

    private record AudioSlice(string Name, string Path, int Order);

    private record TranscriptionSlice(string Name, string Content, int Order);

    private record DocumentChunk(string Name, string TableOfContents, string Content, string Transcription, int Order);

    public async Task<bool> RunAsync(GenerateDocsFromVideoDTO dto)
    {
        _prompts = dto.Prompts ?? new GenerateDocsFromVideoPrompts();

        _fileName = Path.GetFileNameWithoutExtension(dto.VideoPath);

        var transcriptions = await GetTranscriptionsFromFirebase(_fileName);

        if (!transcriptions.Any())
        {
            var audioDirectory = CreateAudioDirectory(dto.VideoPath);

            var audioSlices = await ExtractAndSliceAudio(dto.VideoPath, audioDirectory);

            transcriptions = await GenerateTranscriptions(_fileName, audioSlices);
        }

        var processedTranscriptions = await GenerateProcessedTranscriptions(transcriptions);

        var documentationChunks = await GenerateFinalDocs(processedTranscriptions);

        await _firebaseClient
            .Child("finalDocs")
            .Child(_fileName)
            .PutAsync(documentationChunks);

        return true;
    }

    private async Task<List<TranscriptionSlice>> GenerateProcessedTranscriptions(
        List<TranscriptionSlice> transcriptions)
    {
        //todo implement

        var processedTranscriptions = new List<TranscriptionSlice>();
        foreach (var transcription in transcriptions)
        {
            var improveTranscriptionPrompt = $"""
                                                 {GeneralRulesPrompt}
                                                 {_prompts.ImproveTranscription.Replace("{transcription}", transcription.Content)}
                                                 """;

            var transcriptionToDocumentSkill =
                _kernel.CreateSemanticFunction(
                    improveTranscriptionPrompt,
                    maxTokens: 11000, temperature: 0.0, topP: 1);

            var transcriptionToDocument =
                await transcriptionToDocumentSkill.InvokeAsync();

            processedTranscriptions.Add(transcription with { Content = transcriptionToDocument.Result });
        }

        return processedTranscriptions;
    }

    private async Task<List<TranscriptionSlice>> GetTranscriptionsFromFirebase(string fileName)
    {
        var firebaseTranscriptions = await _firebaseClient
            .Child("transcriptions")
            .Child(fileName)
            .OnceAsListAsync<TranscriptionSlice>();

        var transcriptions = firebaseTranscriptions.Select(x => x.Object).ToList();
        return transcriptions;
    }

    private async Task<List<DocumentChunk>> GenerateFinalDocs(List<TranscriptionSlice> transcriptionSlices)
    {
        var tableOfContents = Empty;

        var documentChunks = new List<DocumentChunk>();
        foreach (var currentSlice in transcriptionSlices)
        {
            const int overlapCharactersCount = 500;

            var overlappingTranscription =
                GenerateOverlappingTranscription(transcriptionSlices, currentSlice, overlapCharactersCount);

            var transcriptionDocument
                = await GenerateDocumentFromTranscription(overlappingTranscription);

            tableOfContents =
                await IncrementTableOfContentsFromDocument(tableOfContents, transcriptionDocument);

            documentChunks.Add(new DocumentChunk(
                currentSlice.Name,
                tableOfContents,
                transcriptionDocument,
                overlappingTranscription,
                currentSlice.Order));
        }

        return documentChunks;
    }

    private async Task<string> IncrementTableOfContentsFromDocument(string tableOfContents,
        string transcriptionDocument)
    {
        var functionDefinition = $"""
                                  {GeneralRulesPrompt}
                                  [GENERATION RULES]
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

                                  Generate or update table of contents in PT-BR based on the following documentation chunk
                                  {transcriptionDocument}
                                  +++++
                                  """;

        var documentToTableOfContentsSkill =
            _kernel.CreateSemanticFunction(functionDefinition, maxTokens: 11000, temperature: 0.0, topP: 1);

        var documentToTableOfContents =
            await documentToTableOfContentsSkill.InvokeAsync();

        return documentToTableOfContents.Result;
    }

    private static string GenerateOverlappingTranscription(List<TranscriptionSlice> transcriptionSlices,
        TranscriptionSlice currentSlice,
        int overlapCharactersCount)
    {
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

        var overlappingTranscription = $"{previousContentOverlap}{currentSlice.Content}{nextContentOverlap}";
        return overlappingTranscription;
    }

    private async Task<string> GenerateDocumentFromTranscription(string transcription)
    {
        var transcriptionToDocumentPrompt = $"""
                                             {GeneralRulesPrompt}
                                             {_prompts.TranscriptionToDocument.Replace("{transcription}", transcription)}
                                             """;

        var transcriptionToDocumentSkill =
            _kernel.CreateSemanticFunction(
                transcriptionToDocumentPrompt,
                maxTokens: 11000, temperature: 0.0, topP: 1);

        var transcriptionToDocument =
            await transcriptionToDocumentSkill.InvokeAsync();

        return transcriptionToDocument.Result;
    }

    private string CreateAudioDirectory(string videoPath)
    {
        var generalAudioDirectoryPath = Path.Combine(Directory.GetCurrentDirectory(), AudioFolderName);

        var currentAudioDirectoryPath = Path.Combine(generalAudioDirectoryPath, _fileName);

        Directory.CreateDirectory(currentAudioDirectoryPath);

        if (!File.Exists(videoPath)) throw new FileNotFoundException("File not found", currentAudioDirectoryPath);

        return currentAudioDirectoryPath;
    }

    private async Task<string> ExtractAudioAsync(string videoPath, string audioDirectory)
    {
        var audioPath = Path.Combine(audioDirectory, $"{_fileName}.mp3");

        var ffmpeg = new Engine(FfmpegPath);
        var inputFile = new InputFile(videoPath);
        var outputFile =
            new OutputFile(Path.Combine(audioDirectory, $"{_fileName}.mp3"));

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

            var sliceName = $"{_fileName}_{segmentNumber}.mp3";
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


    private async Task<List<TranscriptionSlice>> GenerateTranscriptions(string fileName, List<AudioSlice> audioSlices)
    {
        var transcriptions = new List<TranscriptionSlice>();
        foreach (var audioSlice in audioSlices)
        {
            var transcription = await TranscriptAudioFile(audioSlice.Path);
            transcriptions.Add(new TranscriptionSlice(audioSlice.Name, transcription, audioSlice.Order));
        }

        await _firebaseClient
            .Child("transcriptions")
            .Child(fileName)
            .PutAsync(transcriptions);

        return transcriptions;
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

        return responseBody;
    }
}