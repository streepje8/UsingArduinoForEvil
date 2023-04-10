using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions;
using Debug = UnityEngine.Debug;

public class AppMain : MonoBehaviour
{
    
    void Start()
    {
        Boot().ContinueWith(t =>
        {
            if (!t.IsCompletedSuccessfully)
            {
                Debug.LogException(t.Exception);
                EditorApplication.ExitPlaymode();
            } else Debug.Log("AInime Assistant booted successfully!");
        });
    }

    public async Task Boot()
    {
        Debug.Log("Booting AInime Assistant");
        Debug.Log("Checking for voicevox engine...");
        HttpClient client = new HttpClient();
        using HttpResponseMessage response = await client.GetAsync("http://localhost:50021/"); //Idk why im using async here tbh
        using HttpContent content = response.Content;
        var json = await content.ReadAsStringAsync(); //Same for this
        if (json.Equals("{\"detail\":\"Not Found\"}", StringComparison.OrdinalIgnoreCase))
        {
            Debug.Log("Connection Successful!");
        }
        else
        {
            throw new Exception("Could not communicate with voicevox engine. Please make sure it is running and try again.");
        }
        Debug.Log("Checking for whisper installation...");
        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.FileName = "cmd.exe";
        startInfo.Arguments = @"/c " + Application.streamingAssetsPath + "/checkwhisper.cmd";
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        Process process = new Process();
        process.StartInfo = startInfo;
        process.Start();
        process.WaitForExit();
        string output = process.StandardError.ReadToEnd();
        if (output.Contains("--model"))
        {
            Debug.Log("Whisper found!");
        }
        else
            throw new Exception("Could not find whisper. Please make sure python3 is installed and run the batch file in the streaming assets folder.");
        Debug.Log("Checking for GPT...");
        if(File.Exists(Application.streamingAssetsPath + "/GPT/gpt4all/chat/gpt4all-lora-quantized-win64.exe") && File.Exists(Application.streamingAssetsPath + "/GPT/gpt4all/chat/gpt4all-lora-unfiltered-quantized.bin"))
        {
            Debug.Log("GPT found!");
            Debug.Log("Running test prompt: What color is grass?");
            Debug.Log("GPT Output: " + await GPTPrompt("What color is grass?"));
        }
        else
            throw new Exception("Could not find GPT. Please make sure you have it installed using the bat script in the streaming assets folder.");
        
    }

    public async Task<string> GPTPrompt(string prompt)
    {
        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.FileName = Application.streamingAssetsPath + "/promptgpt.cmd";
        startInfo.Arguments = @"What color is grass?";
        startInfo.WorkingDirectory = Application.streamingAssetsPath;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = false;
        Process process = new Process();
        process.StartInfo = startInfo;
        process.Start();
        process.WaitForExit();
        if (process.ExitCode != 0) throw new Exception(process.StandardError.ReadToEnd());
        string output = process.StandardError.ReadToEnd();
        Debug.Log(output);
        string GPTOutput = "";
        if (output.Contains("\n\n") && output.Contains("[end of text]"))
        {
            int Start, End;
            Start = output.IndexOf("\n\n", 0, StringComparison.Ordinal) + "\n\n".Length;
            End = output.IndexOf("[end of text]", Start, StringComparison.Ordinal);
            GPTOutput = output.Substring(Start, End - Start);
        }
        return GPTOutput;
    }

}
