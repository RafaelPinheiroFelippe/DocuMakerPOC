using System.ComponentModel;
using Microsoft.SemanticKernel.SkillDefinition;

namespace DocuMakerPOC.Services.SemanticKernel;

public class DocSkill
{
    private const string TranscriptionToDocumentationDefaultPrompt = @"
[PROMPT GENERATION RULES]
    GENERATE A DOCUMENTATION ABOUT THE TRANSCRIPTIONS SUBJECT
        FOR EXAMPLE IF IT IS THE TRANSCRIPTION OF SOMEONE DESCRIBING HOW A SOFTWARE WORKS, GENERATE THE SOFTWARE'S DOCUMENTATION BASED ON THE TRANSCRIPTION DATA
    BE ORGANIZED AND CONCISE
    USE GOOD DOCUMENTATION PRACTICES FOR THE SPECIFIC SUBJECT, FOR EXAMPLE IF THE SUBJECT OF THE TRANSCRIPTION IS A SOFTWARE UTILIZE SOFTWARE DOCUMENTATION TECHNIQUES  
    YOU WILL RECEIVE THE TRANSCRIPTION IN PLAIN TEXT
    IGNORE CONTENTS OF THE TRANSCRIPTION THAT DONT FIT IN A DOCUMENTATION, FOR EXAMPLE IF YOU RECEIVE A TRANSCRIPTION OF SOMEONE DESCRIBING HOW A SOFTWARE WORKS, IGNORE JOKES, UNRELATED COMMENTARIES, ETC 
    RETURN THE DOCUMENTATION IN MARKDOWN (.md) FORMAT

        Generate documentation based on the following transcription
    {0}
+++++";

    public record TranscriptionToDocumentationParams(string Input);

    [SKFunction, Description("Given an e-mail and message body, send an email")]
    public string TranscriptionToDocumentation(
        [Description("Video transcription.")] string input,
        [Description("Prompt to be executed.")]
        string prompt = TranscriptionToDocumentationDefaultPrompt)
        => string.Format(prompt, input);
    
    //TODO implement semantic function
    [SKFunction, Description("Given an e-mail and message body, send an email")]
    public string DocumentToTableOfContents(
        [Description("Video transcription.")]
        string input,
        [Description("Prompt to be executed.")]
        string prompt = TranscriptionToDocumentationDefaultPrompt)
        => string.Format(prompt, input);
}