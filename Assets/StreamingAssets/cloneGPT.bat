@echo off
color 0b
title GPTCloner
cls
cd GPT
git clone https://github.com/nomic-ai/gpt4all.git --recursive
cd gpt4all
git submodule update --init
python3 -m pip install -r requirements.txt
cd transformers
pip install -e . 
cd ../peft
pip install -e .
cd ..
cd chat
echo downloading gpt4 model...
curl https://the-eye.eu/public/AI/models/nomic-ai/gpt4all/gpt4all-lora-unfiltered-quantized.bin>gpt4all-lora-quantized.bin
echo GPT 4 Fully set up!
pause>nul