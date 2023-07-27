using DocuMakerPOC.TransactionScripts;
using Microsoft.AspNetCore.Mvc;

namespace DocuMakerPOC.Controllers;

[ApiController]
[Route("Video")]
public class VideoController : ControllerBase
{
    private readonly GenerateC4Script _generateC4Script;

    public VideoController(GenerateC4Script generateC4Script)
    {
        _generateC4Script = generateC4Script;
    }

    [HttpPost("{videoPath}", Name = "Vid2C4")]
    public async Task<IActionResult> Vid2C4(string videoPath)
    {
        return await _generateC4Script.RunAsync(videoPath)
            ? Ok()
            : Problem("Generation Failed");
    }
}