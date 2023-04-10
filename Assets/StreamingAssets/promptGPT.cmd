@echo off
set gptPrompt=^"%*^"
cd ./GPT/gpt4all/chat
echo Prompt: %gptPrompt%
gpt4all-lora-quantized-win64.exe --model gpt4all-lora-unfiltered-quantized.bin --prompt %gptPrompt%
cd ../../..
pause>nul