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

    [HttpPost(Name = "Vid2Docs")]
    public async Task<IActionResult> Vid2Docs(
        [FromBody] GenerateDocsFromVideoDTO dto
        )
    {
        return await _generateDocsFromVideoScript.RunAsync(dto)
            ? Ok()
            : Problem("Generation Failed");
    }
}