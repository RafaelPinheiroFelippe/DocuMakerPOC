# DocuMakerPOC
## Introduction
Welcome to DocuMakerPOC, a proof-of-concept application built with .NET 7, demonstrating an innovative pipeline for video processing and documentation generation.

This project explores the capabilities of modern AI technologies, focusing on the conversion of video content into structured documents. The main goal of the POC is to explore, learn, and validate the potential of these technologies for practical application scenarios.

## Technology Stack
DocuMakerPOC utilizes the following technologies:

.NET: A cross-platform framework for building modern cloud-based web applications.

Ffmpeg.NET: A .NET wrapper for the Ffmpeg project, used for handling multimedia data.

Semantic Kernel: A library from Microsoft, designed to orchestrate LLMs.

Whisper: An AI audio model developed by OpenAI, used in the processing pipeline to convert audio data into text.

## Setup

### Ffmpeg
#### Windows
Run on admin shell
- Install chocolatey: `Set-ExecutionPolicy Bypass -Scope Process -Force; [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072; iex ((New-Object System.Net.WebClient).DownloadString('https://community.chocolatey.org/install.ps1'))`
- Install ffmpeg: `choco install ffmpeg`
- Get ffmpeg path: `get-command ffmpeg`