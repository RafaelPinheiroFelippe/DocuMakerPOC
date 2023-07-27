using DocuMakerPOC.TransactionScripts;
using Microsoft.AspNetCore.Mvc;

namespace DocuMakerPOC.Controllers;

[ApiController]
[Route("Video")]
public class VideoController : ControllerBase
{
    [HttpPost("{videoPath}", Name = "Vid2C4")]
    public async Task<IActionResult> Vid2C4(string videoPath)
    {
        return await GenerateC4Script.RunAsync(videoPath)
            ? Ok()
            : Problem("Generation Failed");
    }
}