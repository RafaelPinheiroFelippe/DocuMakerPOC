using DocuMakerPOC.DTOs;
using DocuMakerPOC.TransactionScripts;
using Microsoft.AspNetCore.Mvc;

namespace DocuMakerPOC.Controllers;

[ApiController]
[Route("Video")]
public class VideoController : ControllerBase
{
    private readonly GenerateDocsFromVideoScript _generateDocsFromVideoScript;

    public VideoController(GenerateDocsFromVideoScript generateDocsFromVideoScript)
    {
        _generateDocsFromVideoScript = generateDocsFromVideoScript;
    }

    /// <summary>
    /// </summary>
    /// <remarks>
    ///     *A custom prompt must always contain the same parameters as the default.
    /// 
    ///     - TranscriptionToDocument default prompt:
    ///
    ///             [GENERATION RULES]
    ///             GENERATE A DOCUMENT WITH THE TRANSCRIPTIONS CONTENT
    ///             BE ORGANIZED AND CONCISE
    ///             YOU WILL RECEIVE THE TRANSCRIPTION IN PLAIN TEXT, IT IS ONLY A CHUNK OF A FULL TRANSCRIPTION
    ///             IGNORE CONTENTS OF THE TRANSCRIPTION THAT DONT FIT IN A DOCUMENTATION, FOR EXAMPLE IF YOU RECEIVE A TRANSCRIPTION OF SOMEONE DESCRIBING HOW A SOFTWARE WORKS, IGNORE JOKES, UNRELATED COMMENTARIES, ETC
    ///             RETURN THE DOCUMENT IN MARKDOWN (.md) FORMAT
    ///
    ///             Generate document in PT-BR based on the following transcription
    ///             {transcription}
    ///             +++++
    /// </remarks>
    [HttpPost(Name = "Vid2Docs")]
    public async Task<IActionResult> Vid2Docs([FromBody] GenerateDocsFromVideoDTO dto)
    {
        return await _generateDocsFromVideoScript.RunAsync(dto)
            ? Ok()
            : Problem("Generation Failed");
    }
}