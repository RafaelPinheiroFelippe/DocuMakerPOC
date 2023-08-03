using System.ComponentModel;
using DocuMakerPOC.Prompts;

namespace DocuMakerPOC.DTOs;

public record GenerateDocsFromVideoDTO(
    string VideoPath =  null,
    string FileName = null,
    GenerateDocsFromVideoPrompts? Prompts = null
    );

