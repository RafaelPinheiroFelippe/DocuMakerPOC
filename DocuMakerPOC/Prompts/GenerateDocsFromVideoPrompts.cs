using System.ComponentModel;

namespace DocuMakerPOC.Prompts;

public record GenerateDocsFromVideoPrompts(
    string ImproveTranscription = """
                                      [GENERATION RULES]
                                      YOUR ROLE IS TO IMPROVE A TRANSCRIPTION
                                      YOU WILL RECEIVE A RAW TRANSCRIPTION AND WILL RETURN A PROCESSED TRANSCRIPTION, ADJUSTING TYPOS AND REFORMULATING PHRASES SO THAT THEY MAKE SENSE
                                      YOU WILL RECEIVE THE TRANSCRIPTION IN PLAIN TEXT, IT IS ONLY A CHUNK OF A FULL TRANSCRIPTION
                                      TRY YOUR BEST TO SEPARATE PHRASES
                                      
                                      RETURN ONLY THE PROCESSED TRANSCRIPTION IN PLAIN TEXT IN THIS FORMAT:
                                      - PROCESSED_PHRASE
                                      - SECOND_PROCESSED_PHRASE
                                      - THIRD_PROCESSED_PHRASE
                                      END OF EXAMPLE, IN YOUR RESULT REPLACE THE VARIABLES 'PROCESSED_PHRASE' WITH ACTUAL PROCESSED CONTENT

                                      Generate processed transcription based on the following transcription
                                      {transcription}
                                      +++++
                                      """,
    string TranscriptionToDocument = """
                                     [GENERATION RULES]
                                     GENERATE A DOCUMENT WITH THE TRANSCRIPTIONS CONTENT
                                     BE ORGANIZED AND CONCISE
                                     YOU WILL RECEIVE THE TRANSCRIPTION IN PLAIN TEXT, IT IS ONLY A CHUNK OF A FULL TRANSCRIPTION
                                     IGNORE CONTENTS OF THE TRANSCRIPTION THAT DONT FIT IN A DOCUMENTATION, FOR EXAMPLE IF YOU RECEIVE A TRANSCRIPTION OF SOMEONE DESCRIBING HOW A SOFTWARE WORKS, IGNORE JOKES, UNRELATED COMMENTARIES, ETC
                                     RETURN THE DOCUMENT IN MARKDOWN (.md) FORMAT

                                     Generate document based on the following transcription
                                     {transcription}
                                     +++++
                                     """
    );