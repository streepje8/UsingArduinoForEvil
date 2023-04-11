using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class AppMain : MonoBehaviour
{
    public AudioClip lastClip;
    public string micName;
    public List<string> micNames = new List<string>();
    public TMP_Text statusText;
    [NonSerialized] public bool isInitalized = false;

    private AudioSource source;
    private bool voiceIsPlaying = false;
    public AudioSource AIAudioSource
    {
	    get
	    {
		    if (source == null) source = gameObject.AddComponent<AudioSource>();
		    return source;
	    }
    }

    void Start()
    {
        micNames = Microphone.devices.ToList();
        statusText.text = "Checking for dependencies...";
        Boot().ContinueWith(t =>
        {
            if (!t.IsCompletedSuccessfully)
            {
                Debug.LogException(t.Exception);
            }
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
        await process.WaitForExitAsync();
        string output = process.StandardError.ReadToEnd();
        if (output.Contains("--model"))
        {
            Debug.Log("Whisper found!");
        }
        else
            throw new Exception("Could not find whisper. Please make sure python3 is installed and run the batch file in the streaming assets folder.");
        
        statusText.text = "Ready!";
        isInitalized = true;
        Debug.Log("AInime Assistant booted successfully!");
        
        /* Removed GPT Code
        // Debug.Log("Checking for GPT...");
        // if(File.Exists(Application.streamingAssetsPath + "/GPT/gpt4all/chat/gpt4all-lora-quantized-win64.exe") && File.Exists(Application.streamingAssetsPath + "/GPT/gpt4all/chat/gpt4all-lora-unfiltered-quantized.bin"))
        // {
        //     Debug.Log("GPT found!");
        //     Debug.Log("Running test prompt: What color is grass?");
        //     Debug.Log("GPT Output: " + await GPTPrompt("What color is grass?"));
        // }
        // else
        //     throw new Exception("Could not find GPT. Please make sure you have it installed using the bat script in the streaming assets folder.");
        */
    }

    private void Update()
    {
        if (isInitalized && !voiceIsPlaying)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
	            statusText.text = "Listening...";
                lastClip = Microphone.Start(micName, false, 20, 44100);
            }
            if (Input.GetKeyUp(KeyCode.Space))
            {
	            Microphone.End(micName);
	            statusText.text = "Loading...";
                ProcessInput(lastClip).ContinueWith(t =>
                {
                    if(t.IsFaulted) Debug.LogException(t.Exception);
                    Debug.Log("Finished!");
                });
            }
        }

        if (voiceIsPlaying)
        {
	        if (!source.isPlaying)
	        {
		        statusText.text = "Ready!";
		        voiceIsPlaying = false;
	        }
        }
    }

    private async Task ProcessInput(AudioClip audioClip)
    {
	    byte[] input = WavUtility.FromAudioClip(ConvertToStereo(audioClip));
        File.WriteAllBytes(Application.streamingAssetsPath + "/recording.wav",input);
        Debug.Log("Recording saved to " + Application.streamingAssetsPath + "/recording.wav");
        statusText.text = "Whisper AI Processing...";
        Debug.Log("Whisper processing...");
        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.FileName = "cmd.exe";
        startInfo.Arguments = @"/c " + Application.streamingAssetsPath + "/processWithWhisper.cmd ./recording.wav";
        startInfo.WorkingDirectory = Application.streamingAssetsPath;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        Process process = new Process();
        process.StartInfo = startInfo;
        process.Start();
        await process.WaitForExitAsync();
        string errors = process.StandardError.ReadToEnd();
        if(process.ExitCode != 0) Debug.LogWarning("Whisper outputted an error: " + errors);
        Debug.Log("Whisper finished processing!");
        Debug.Log("Reading output...");
        string whisperoutput = await File.ReadAllTextAsync(Application.streamingAssetsPath + "/recording.txt");
        Debug.Log("Whisper recognized: " + whisperoutput);
        Debug.Log("Processing Question...");
        statusText.text = "Thinking of a reply...";
        HttpClient duckduckgo = new HttpClient();
        using HttpResponseMessage searchresult = await duckduckgo.GetAsync("https://api.duckduckgo.com/?format=json&q=" + WebUtility.UrlEncode(whisperoutput)); //Idk why im using async here tbh
        using HttpContent resultcontent = searchresult.Content;
        string duckjson = await resultcontent.ReadAsStringAsync();
        dynamic result = JObject.Parse(duckjson);
        string duckduckgoresult = result.Abstract;
        if (string.IsNullOrEmpty(duckduckgoresult)) duckduckgoresult = result.Answer;
        if (string.IsNullOrEmpty(duckduckgoresult)) duckduckgoresult = result.Definition;
        if (string.IsNullOrEmpty(duckduckgoresult)) duckduckgoresult = "I am sorry, I do not know the answer to that question.";
        Debug.Log("DuckDuckGo result: " + duckjson);
        Debug.Log("Extracted: " + duckduckgoresult);
        
        statusText.text = "Adding the japanese accent to it...";
        Debug.Log("Converting response to kana");
        string toVoiceVox = ConvertToKana(duckduckgoresult);
        statusText.text = "AI Generating speech with voicevox...";
        Debug.Log("Creating VoiceVox query...");
        HttpClient client = new HttpClient();
        using HttpResponseMessage response = await client.PostAsync("http://localhost:50021/audio_query?text=" + WebUtility.UrlEncode(toVoiceVox) + "&speaker=1",new StringContent("{}",Encoding.UTF8,"application/json"));
        using HttpContent content = response.Content;
        var queryJson = await content.ReadAsStringAsync(); //Same for this
        Debug.Log("Creating VoiceVox audio...");
        using HttpResponseMessage responseTwo = await client.PostAsync("http://localhost:50021/synthesis?speaker=1",new StringContent(queryJson.Replace("\"speedScale\":1.0","\"speedScale\":1.7"),Encoding.UTF8,"application/json"));
        using HttpContent contentTwo = responseTwo.Content;
        var reply = await contentTwo.ReadAsByteArrayAsync();
        statusText.text = "Converting response....";
        Debug.Log("Converting VoiceVox response to audio clip...");
        File.WriteAllBytes(Application.streamingAssetsPath + "/syntesized.wav",reply);
        Debug.Log("Playing audio...");
        AIAudioSource.PlayOneShot(WavUtility.ToAudioClip(reply));
        statusText.text = "Speaking....";
        voiceIsPlaying = true;
    }

    private static Dictionary<string, string> combos = new Dictionary<string, string>()
    {
        {"aa","ああ"},
        {"ab","あぶ"},
        {"ac","あく"},
        {"ad","あづ"},
        {"ae","あえ"},
        {"af","あふ"},
        {"ag","あぐ"},
        {"ah","あー"},
        {"ai","あい"},
        {"aj","あい"},
        {"ak","あく"},
        {"al","あぅ"},
        {"am","あむ"},
        {"an","あん"},
        {"ao","あお"},
        {"ap","あぷ"},
        {"aq","あく"},
        {"ar","ある"},
        {"as","あす"},
        {"at","あと"},
        {"au","あう"},
        {"av","あヴ"},
        {"aw","あヴ"},
        {"ax","あくす"},
        {"ay","あい"},
        {"az","あず"},
        {"ba","ば"},
        {"bb","ぶ"},
        {"bc","べくす"},
        {"bd","ぶづ"},
        {"be","べ"},
        {"bf","べふ"},
        {"bg","べぐ"},
        {"bh","べふ"},
        {"bi","び"},
        {"bj","びしゅ"},
        {"bk","べく"},
        {"bl","べる"},
        {"bm","べむ"},
        {"bn","べん"},
        {"bo","ぼ"},
        {"bp","べぷ"},
        {"bq","べく"},
        {"br","べる"},
        {"bs","べす"},
        {"bt","べと"},
        {"bu","ぶ"},
        {"bv","べヴ"},
        {"bw","べを"},
        {"bx","べくす"},
        {"by","べい"},
        {"bz","えず"},
        {"ca","か"},
        {"cb","せぶ"},
        {"cc","しー"},
        {"cd","しぢ"},
        {"ce","しー"},
        {"cf","しえふ"},
        {"cg","しぎ"},
        {"ch","しひ"},
        {"ci","し"},
        {"cj","し"},
        {"ck","しく"},
        {"cl","しぅ"},
        {"cm","じむ"},
        {"cn","じん"},
        {"co","こ"},
        {"cp","しぴ"},
        {"cq","しく"},
        {"cr","しる"},
        {"cs","しす"},
        {"ct","しと"},
        {"cu","く"},
        {"cv","しヴ"},
        {"cw","しヴ"},
        {"cx","しえくす"},
        {"cy","しえい"},
        {"cz","しぜ"},
        {"da","だ"},
        {"db","だぶ"},
        {"dc","だく"},
        {"dd","でで"},
        {"de","で"},
        {"df","でふ"},
        {"dg","でぐ"},
        {"dh","でほ"},
        {"di","ぢ"},
        {"dj","でじ"},
        {"dk","でこ"},
        {"dl","でぉ"},
        {"dm","でむ"},
        {"dn","でん"},
        {"do","ど"},
        {"dp","でぷ"},
        {"dq","でく"},
        {"dr","でる"},
        {"ds","です"},
        {"dt","でと"},
        {"du","づ"},
        {"dv","でヴ"},
        {"dw","でを"},
        {"dx","でくす"},
        {"dy","でい"},
        {"dz","でず"},
        {"ea","えあ"},
        {"eb","えぶ"},
        {"ec","えく"},
        {"ed","えづ"},
        {"ee","ええ"},
        {"ef","えふ"},
        {"eg","えぐ"},
        {"eh","えほ"},
        {"ei","えい"},
        {"ej","えい"},
        {"ek","えく"},
        {"el","える"},
        {"em","えむ"},
        {"en","えぬ"},
        {"eo","えお"},
        {"ep","えぷ"},
        {"eq","えく"},
        {"er","える"},
        {"es","えす"},
        {"et","えと"},
        {"eu","えう"},
        {"ev","えヴ"},
        {"ew","えを"},
        {"ex","えくす"},
        {"ey","えい"},
        {"ez","えず"},
        {"fa","ふぁ"},
        {"fb","ふぶ"},
        {"fc","ふく"},
        {"fd","ふづ"},
        {"fe","ふぇ"},
        {"ff","ふふ"},
        {"fg","ふぐ"},
        {"fh","ふほ"},
        {"fi","ふぃ"},
        {"fj","ふじ"},
        {"fk","ふく"},
        {"fl","ふぉ"},
        {"fm","ふむ"},
        {"fn","ふん"},
        {"fo","ふぉ"},
        {"fp","ふぷ"},
        {"fq","ふく"},
        {"fr","ふる"},
        {"fs","ふす"},
        {"ft","ふと"},
        {"fu","ふ"},
        {"fv","ふヴ"},
        {"fw","ふを"},
        {"fx","ふくす"},
        {"fy","ふい"},
        {"fz","ふず"},
        {"ga","が"},
        {"gb","ぐぶ"},
        {"gc","ぐく"},
        {"gd","ぐづ"},
        {"ge","げ"},
        {"gf","ぐふ"},
        {"gg","ぐぐ"},
        {"gh","ぐほ"},
        {"gi","ぎ"},
        {"gj","ぎじゅ"},
        {"gk","ぐく"},
        {"gl","ぐぉ"},
        {"gm","ぐむ"},
        {"gn","ぐん"},
        {"go","ご"},
        {"gp","ぐぷ"},
        {"gq","ぐく"},
        {"gr","ぐる"},
        {"gs","ぐす"},
        {"gt","ぐと"},
        {"gu","ぐ"},
        {"gv","ぐヴ"},
        {"gw","ぐを"},
        {"gx","げくす"},
        {"gy","ぐい"},
        {"gz","げず"},
        {"ha","は"},
        {"hb","はぶ"},
        {"hc","はく"},
        {"hd","はでい"},
        {"he","へ"},
        {"hf","はふ"},
        {"hg","はぐ"},
        {"hh","えしえし"},
        {"hi","ひ"},
        {"hj","ひ"},
        {"hk","はく"},
        {"hl","はる"},
        {"hm","はむ"},
        {"hn","はぬ"},
        {"ho","ほ"},
        {"hp","はぽ"},
        {"hq","はく"},
        {"hr","はる"},
        {"hs","はす"},
        {"ht","はと"},
        {"hu","ふ"},
        {"hv","はヴ"},
        {"hw","はヴ"},
        {"hx","はえくす"},
        {"hy","へえい"},
        {"hz","はず"},
        {"ia","いあ"},
        {"ib","いぶ"},
        {"ic","いく"},
        {"id","いで"},
        {"ie","いえ"},
        {"if","いふ"},
        {"ig","いぐ"},
        {"ih","いふ"},
        {"ii","いい"},
        {"ij","いし"},
        {"ik","いく"},
        {"il","いる"},
        {"im","いむ"},
        {"in","いぬ"},
        {"io","いお"},
        {"ip","いぷ"},
        {"iq","いく"},
        {"ir","いる"},
        {"is","いす"},
        {"it","いと"},
        {"iu","いう"},
        {"iv","イヴ"},
        {"iw","いを"},
        {"ix","いえくす"},
        {"iy","いし"},
        {"iz","いず"},
        {"ja","しゃ"},
        {"jb","じぶ"},
        {"jc","じく"},
        {"jd","じど"},
        {"je","じえ"},
        {"jf","しふ"},
        {"jg","しぐ"},
        {"jh","しほ"},
        {"ji","じ"},
        {"jj","じじ"},
        {"jk","じく"},
        {"jl","じる"},
        {"jm","じむ"},
        {"jn","じん"},
        {"jo","よ"},
        {"jp","じぴ"},
        {"jq","じく"},
        {"jr","じる"},
        {"js","じす"},
        {"jt","じと"},
        {"ju","ュ"},
        {"jv","じヴ"},
        {"jw","じう"},
        {"jx","じえくす"},
        {"jy","じえい"},
        {"jz","じじ"},
        {"ka","か"},
        {"kb","けぶ"},
        {"kc","けく"},
        {"kd","けど"},
        {"ke","け"},
        {"kf","けふ"},
        {"kg","けぐ"},
        {"kh","かは"},
        {"ki","ひ"},
        {"kj","かじ"},
        {"kk","けいけい"},
        {"kl","ける"},
        {"km","けむ"},
        {"kn","けの"},
        {"ko","こ"},
        {"kp","けぷ"},
        {"kq","けく"},
        {"kr","ける"},
        {"ks","けす"},
        {"kt","けと"},
        {"ku","く"},
        {"kv","けヴ"},
        {"kw","けを"},
        {"kx","けえくす"},
        {"ky","けい"},
        {"kz","けず"},
        {"la","ら"},
        {"lb","るぶ"},
        {"lc","るく"},
        {"ld","るど"},
        {"le","れ"},
        {"lf","るふ"},
        {"lg","るぐ"},
        {"lh","るほ"},
        {"li","り"},
        {"lj","りじ"},
        {"lk","るく"},
        {"ll","りる"},
        {"lm","りむ"},
        {"ln","りん"},
        {"lo","ろ"},
        {"lp","るぷ"},
        {"lq","るく"},
        {"lr","るる"},
        {"ls","るす"},
        {"lt","ると"},
        {"lu","る"},
        {"lv","るヴ"},
        {"lw","るを"},
        {"lx","れくす"},
        {"ly","れい"},
        {"lz","れず"},
        {"ma","ま"},
        {"mb","むぶ"},
        {"mc","むく"},
        {"md","むど"},
        {"me","め"},
        {"mf","むふ"},
        {"mg","むぐ"},
        {"mh","むほ"},
        {"mi","み"},
        {"mj","みじ"},
        {"mk","むく"},
        {"ml","むる"},
        {"mm","みむ"},
        {"mn","みん"},
        {"mo","も"},
        {"mp","むぷ"},
        {"mq","むく"},
        {"mr","むる"},
        {"ms","むす"},
        {"mt","むと"},
        {"mu","む"},
        {"mv","むヴ"},
        {"mw","むを"},
        {"mx","めくす"},
        {"my","めい"},
        {"mz","めず"},
        {"na","な"},
        {"nb","ぬぶ"},
        {"nc","ぬく"},
        {"nd","ぬど"},
        {"ne","ね"},
        {"nf","ぬふ"},
        {"ng","ぬぐ"},
        {"nh","ぬほ"},
        {"ni","に"},
        {"nj","にじ"},
        {"nk","ぬく"},
        {"nl","ぬる"},
        {"nm","にむ"},
        {"nn","にん"},
        {"no","の"},
        {"np","ぬぷ"},
        {"nq","ぬく"},
        {"nr","ぬる"},
        {"ns","ぬす"},
        {"nt","ぬと"},
        {"nu","ぬ"},
        {"nv","ぬヴ"},
        {"nw","ぬを"},
        {"nx","ねくす"},
        {"ny","ねい"},
        {"nz","ねず"},
        {"oa","おあ"},
        {"ob","おぶ"},
        {"oc","おく"},
        {"od","おで"},
        {"oe","おえ"},
        {"of","おふ"},
        {"og","おぐ"},
        {"oh","おほ"},
        {"oi","おい"},
        {"oj","おじ"},
        {"ok","おく"},
        {"ol","おる"},
        {"om","おむ"},
        {"on","おん"},
        {"oo","おお"},
        {"op","おぷ"},
        {"oq","おく"},
        {"or","おる"},
        {"os","おす"},
        {"ot","おと"},
        {"ou","おう"},
        {"ov","おヴ"},
        {"ow","おを"},
        {"ox","おえくす"},
        {"oy","おい"},
        {"oz","おず"},
        {"pa","ぱ"},
        {"pb","ぷぶ"},
        {"pc","ぷく"},
        {"pd","ぷど"},
        {"pe","ぺ"},
        {"pf","ぷふ"},
        {"pg","ぷぐ"},
        {"ph","ぷほ"},
        {"pi","ぴ"},
        {"pj","ぴじ"},
        {"pk","ぷく"},
        {"pl","ぷる"},
        {"pm","ぴむ"},
        {"pn","ぴん"},
        {"po","ぽ"},
        {"pp","ぷぷ"},
        {"pq","ぷく"},
        {"pr","ぷる"},
        {"ps","ぷす"},
        {"pt","ぷと"},
        {"pu","ぷ"},
        {"pv","ぷヴ"},
        {"pw","ぷう"},
        {"px","ぺくす"},
        {"py","ぴえい"},
        {"pz","ぴじ"},
        {"qa","くあ"},
        {"qb","くぶ"},
        {"qc","くく"},
        {"qd","くど"},
        {"qe","くえ"},
        {"qf","くふ"},
        {"qg","くぐ"},
        {"qh","くほ"},
        {"qi","くい"},
        {"qj","くじ"},
        {"qk","くく"},
        {"ql","くる"},
        {"qm","くむ"},
        {"qn","くん"},
        {"qo","くお"},
        {"qp","くぷ"},
        {"qq","くく"},
        {"qr","くる"},
        {"qs","くす"},
        {"qt","くと"},
        {"qu","くう"},
        {"qv","くヴ"},
        {"qw","くを"},
        {"qx","くえくす"},
        {"qy","くい"},
        {"qz","くず"},
        {"ra","ら"},
        {"rb","るぶ"},
        {"rc","るく"},
        {"rd","るど"},
        {"re","れ"},
        {"rf","るふ"},
        {"rg","るぐ"},
        {"rh","るほ"},
        {"ri","り"},
        {"rj","りじ"},
        {"rk","るく"},
        {"rl","るる"},
        {"rm","りむ"},
        {"rn","りん"},
        {"ro","ろ"},
        {"rp","るぷ"},
        {"rq","るく"},
        {"rr","りる"},
        {"rs","るす"},
        {"rt","ると"},
        {"ru","る"},
        {"rv","るヴ"},
        {"rw","るを"},
        {"rx","れくす"},
        {"ry","れい"},
        {"rz","れず"},
        {"sa","さ"},
        {"sb","すぶ"},
        {"sc","すく"},
        {"sd","すど"},
        {"se","せ"},
        {"sf","すふ"},
        {"sg","すぐ"},
        {"sh","しょ"},
        {"si","し"},
        {"sj","しじ"},
        {"sk","すく"},
        {"sl","する"},
        {"sm","しむ"},
        {"sn","しん"},
        {"so","そ"},
        {"sp","すぷ"},
        {"sq","すく"},
        {"sr","する"},
        {"ss","すす"},
        {"st","すと"},
        {"su","す"},
        {"sv","すヴ"},
        {"sw","すを"},
        {"sx","せくす"},
        {"sy","せい"},
        {"sz","せず"},
        {"ta","た"},
        {"tb","つぶ"},
        {"tc","つく"},
        {"td","つど"},
        {"te","て"},
        {"tf","つふ"},
        {"tg","つぐ"},
        {"th","てほ"},
        {"ti","ち"},
        {"tj","ちじ"},
        {"tk","つく"},
        {"tl","つる"},
        {"tm","ちむ"},
        {"tn","ちん"},
        {"to","と"},
        {"tp","つぷ"},
        {"tq","つく"},
        {"tr","つる"},
        {"ts","つす"},
        {"tt","つと"},
        {"tu","つ"},
        {"tv","つヴ"},
        {"tw","つを"},
        {"tx","てくす"},
        {"ty","てい"},
        {"tz","てず"},
        {"ua","うあ"},
        {"ub","うぶ"},
        {"uc","うく"},
        {"ud","うど"},
        {"ue","うえ"},
        {"uf","うふ"},
        {"ug","うぐ"},
        {"uh","うほ"},
        {"ui","うい"},
        {"uj","うじ"},
        {"uk","うく"},
        {"ul","うる"},
        {"um","うむ"},
        {"un","うん"},
        {"uo","うお"},
        {"up","うぷ"},
        {"uq","うく"},
        {"ur","うる"},
        {"us","うす"},
        {"ut","うと"},
        {"uu","うう"},
        {"uv","うヴ"},
        {"uw","うを"},
        {"ux","うえくす"},
        {"uy","うい"},
        {"uz","うず"},
        {"va","ヴぁ"},
        {"vb","ヴぶ"},
        {"vc","ヴく"},
        {"vd","ヴど"},
        {"ve","ヴぇ"},
        {"vf","ヴふ"},
        {"vg","ヴぐ"},
        {"vh","ヴほ"},
        {"vi","ヴぃ"},
        {"vj","ヴじ"},
        {"vk","ヴく"},
        {"vl","ヴる"},
        {"vm","ヴむ"},
        {"vn","ヴん"},
        {"vo","ヴぉ"},
        {"vp","ヴぷ"},
        {"vq","ヴく"},
        {"vr","ヴる"},
        {"vs","ヴす"},
        {"vt","ヴと"},
        {"vu","ヴ"},
        {"vv","ヴヴ"},
        {"vw","ヴを"},
        {"vx","ヴぇくす"},
        {"vy","ヴぃ"},
        {"vz","ヴず"},
        {"wa","わ"},
        {"wb","うぶ"},
        {"wc","うく"},
        {"wd","うど"},
        {"we","うえ"},
        {"wf","うふ"},
        {"wg","うぐ"},
        {"wh","うほ"},
        {"wi","うぃ"},
        {"wj","うじ"},
        {"wk","うく"},
        {"wl","うる"},
        {"wm","うむ"},
        {"wn","うん"},
        {"wo","を"},
        {"wp","うぷ"},
        {"wq","うく"},
        {"wr","うる"},
        {"ws","うす"},
        {"wt","うと"},
        {"wu","う"},
        {"wv","うヴ"},
        {"ww","うを"},
        {"wx","うえくす"},
        {"wy","うい"},
        {"wz","うず"},
        {"xa","ぁ"},
        {"xb","ぶ"},
        {"xc","く"},
        {"xd","ど"},
        {"xe","ぇ"},
        {"xf","ふ"},
        {"xg","ぐ"},
        {"xh","ほ"},
        {"xi","ぃ"},
        {"xj","じ"},
        {"xk","く"},
        {"xl","る"},
        {"xm","む"},
        {"xn","ん"},
        {"xo","ぉ"},
        {"xp","ぷ"},
        {"xq","く"},
        {"xr","る"},
        {"xs","す"},
        {"xt","と"},
        {"xu","う"},
        {"xv","ヴ"},
        {"xw","を"},
        {"xx","っ"},
        {"xy","ぃ"},
        {"xz","ず"},
        {"ya","や"},
        {"yb","ゆぶ"},
        {"yc","ゆく"},
        {"yd","ゆど"},
        {"ye","いぇ"},
        {"yf","ゆふ"},
        {"yg","ゆぐ"},
        {"yh","ゆほ"},
        {"yi","い"},
        {"yj","いじ"},
        {"yk","ゆく"},
        {"yl","ゆる"},
        {"ym","ゆむ"},
        {"yn","ゆん"},
        {"yo","よ"},
        {"yp","ゆぷ"},
        {"yq","ゆく"},
        {"yr","ゆる"},
        {"ys","ゆす"},
        {"yt","ゆと"},
        {"yu","ゆ"},
        {"yv","ゆヴ"},
        {"yw","ゆを"},
        {"yx","いぇくす"},
        {"yy","い"},
        {"yz","ゆず"},
        {"za","ざ"},
        {"zb","ずぶ"},
        {"zc","ずく"},
        {"zd","ずど"},
        {"ze","ぜ"},
        {"zf","ずふ"},
        {"zg","ずぐ"},
        {"zh","ぜほ"},
        {"zi","じ"},
        {"zj","じじ"},
        {"zk","ずく"},
        {"zl","ずる"},
        {"zm","じむ"},
        {"zn","じん"},
        {"zo","ぞ"},
        {"zp","ずぷ"},
        {"zq","ずく"},
        {"zr","ずる"},
        {"zs","ずす"},
        {"zt","ずと"},
        {"zu","ず"},
        {"zv","ずヴ"},
        {"zw","ずを"},
        {"zx","ぜくす"},
        {"zy","ぜい"},
        {"zz","ぜず"},
        {"a_","あ"},
        {"b_","ぶ"},
        {"c_","く"},
        {"d_","で"},
        {"e_","え"},
        {"f_","ふ"},
        {"g_","ぐ"},
        {"h_","えー"},
        {"i_","い"},
        {"j_","えい"},
        {"k_","く"},
        {"l_","る"},
        {"m_","む"},
        {"n_","ん"},
        {"o_","お"},
        {"p_","ぷ"},
        {"q_","く"},
        {"r_","る"},
        {"s_","す"},
        {"t_","と"},
        {"u_","う"},
        {"v_","ヴ"},
        {"w_","ヴ"},
        {"x_","えくす"},
        {"y_","えい"},
        {"z_","ぜ"},
    };
    private string ConvertToKana(string text)
    {
        string endresult = "";
        text = text.Replace(".", " ").Replace(","," ").Replace("?"," ").Replace("!"," ").Replace("\'","").Replace("the","te");
        foreach (string word in text.Split(" "))
        {
            string result = "";
            var chars = word.ToCharArray();
            if (chars.Length % 2 != 0)
            {
                chars = chars.Append('_').ToArray();
            }
            for (int i = 0; i < chars.Length; i += 2)
            {
                string prekana = chars[i] + "" + chars[i + 1];
                prekana = prekana.ToLower();
                if (combos.TryGetValue(prekana, out string kana))
                {
                    result += kana;
                }
                else
                {
                    result += prekana;
                }
            }
            endresult += result + " ";
        }
        return endresult;
    }

    private AudioClip ConvertToStereo(AudioClip audioClip)
    {
        float[] samples = new float[audioClip.samples];
        audioClip.GetData(samples, 0);
        float[] stereoSamples = new float[audioClip.samples * 2];
        for (int i = 0; i < audioClip.samples; i++)
        {
            stereoSamples[i * 2] = samples[i];
            stereoSamples[i * 2 + 1] = samples[i];
        }
        AudioClip stereoClip = AudioClip.Create(audioClip.name, audioClip.samples, 2, audioClip.frequency, false);
        stereoClip.SetData(stereoSamples, 0);
        return stereoClip;
    }

    #region Removed Code
    // public async Task<string> GPTPrompt(string prompt)
    // {
    //     ProcessStartInfo startInfo = new ProcessStartInfo();
    //     startInfo.FileName = Application.streamingAssetsPath + "/promptgpt.cmd";
    //     startInfo.Arguments = @"What color is grass?";
    //     startInfo.WorkingDirectory = Application.streamingAssetsPath;
    //     startInfo.RedirectStandardOutput = true;
    //     startInfo.RedirectStandardError = true;
    //     startInfo.UseShellExecute = false;
    //     startInfo.CreateNoWindow = false;
    //     Process process = new Process();
    //     process.StartInfo = startInfo;
    //     process.Start();
    //     process.WaitForExit();
    //     if (process.ExitCode != 0) throw new Exception(process.StandardError.ReadToEnd());
    //     string output = process.StandardError.ReadToEnd();
    //     Debug.Log(output);
    //     string GPTOutput = "";
    //     if (output.Contains("\n\n") && output.Contains("[end of text]"))
    //     {
    //         int Start, End;
    //         Start = output.IndexOf("\n\n", 0, StringComparison.Ordinal) + "\n\n".Length;
    //         End = output.IndexOf("[end of text]", Start, StringComparison.Ordinal);
    //         GPTOutput = output.Substring(Start, End - Start);
    //     }
    //     return GPTOutput;
    // }
    #endregion

    //Code joinked from https://github.com/deadlyfingers/UnityWav/blob/master/WavUtility.cs
public class WavUtility
{
	// Force save as 16-bit .wav
	const int BlockSize_16Bit = 2;

	/// <summary>
	/// Load PCM format *.wav audio file (using Unity's Application data path) and convert to AudioClip.
	/// </summary>
	/// <returns>The AudioClip.</returns>
	/// <param name="filePath">Local file path to .wav file</param>
	public static AudioClip ToAudioClip (string filePath)
	{
		if (!filePath.StartsWith (Application.persistentDataPath) && !filePath.StartsWith (Application.dataPath)) {
			Debug.LogWarning ("This only supports files that are stored using Unity's Application data path. \nTo load bundled resources use 'Resources.Load(\"filename\") typeof(AudioClip)' method. \nhttps://docs.unity3d.com/ScriptReference/Resources.Load.html");
			return null;
		}
		byte[] fileBytes = File.ReadAllBytes (filePath);
		return ToAudioClip (fileBytes, 0);
	}

	public static AudioClip ToAudioClip (byte[] fileBytes, int offsetSamples = 0, string name = "wav")
	{
		//string riff = Encoding.ASCII.GetString (fileBytes, 0, 4);
		//string wave = Encoding.ASCII.GetString (fileBytes, 8, 4);
		int subchunk1 = BitConverter.ToInt32 (fileBytes, 16);
		UInt16 audioFormat = BitConverter.ToUInt16 (fileBytes, 20);

		// NB: Only uncompressed PCM wav files are supported.
		string formatCode = FormatCode (audioFormat);
		Debug.AssertFormat (audioFormat == 1 || audioFormat == 65534, "Detected format code '{0}' {1}, but only PCM and WaveFormatExtensable uncompressed formats are currently supported.", audioFormat, formatCode);

		UInt16 channels = BitConverter.ToUInt16 (fileBytes, 22);
		int sampleRate = BitConverter.ToInt32 (fileBytes, 24);
		//int byteRate = BitConverter.ToInt32 (fileBytes, 28);
		//UInt16 blockAlign = BitConverter.ToUInt16 (fileBytes, 32);
		UInt16 bitDepth = BitConverter.ToUInt16 (fileBytes, 34);

		int headerOffset = 16 + 4 + subchunk1 + 4;
		int subchunk2 = BitConverter.ToInt32 (fileBytes, headerOffset);
		//Debug.LogFormat ("riff={0} wave={1} subchunk1={2} format={3} channels={4} sampleRate={5} byteRate={6} blockAlign={7} bitDepth={8} headerOffset={9} subchunk2={10} filesize={11}", riff, wave, subchunk1, formatCode, channels, sampleRate, byteRate, blockAlign, bitDepth, headerOffset, subchunk2, fileBytes.Length);

		float[] data;
		switch (bitDepth) {
		case 8:
			data = Convert8BitByteArrayToAudioClipData (fileBytes, headerOffset, subchunk2);
			break;
		case 16:
			data = Convert16BitByteArrayToAudioClipData (fileBytes, headerOffset, subchunk2);
			break;
		case 24:
			data = Convert24BitByteArrayToAudioClipData (fileBytes, headerOffset, subchunk2);
			break;
		case 32:
			data = Convert32BitByteArrayToAudioClipData (fileBytes, headerOffset, subchunk2);
			break;
		default:
			throw new Exception (bitDepth + " bit depth is not supported.");
		}

		AudioClip audioClip = AudioClip.Create (name, data.Length, (int)channels, sampleRate, false);
		audioClip.SetData (data, 0);
		return audioClip;
	}

	#region wav file bytes to Unity AudioClip conversion methods

	private static float[] Convert8BitByteArrayToAudioClipData (byte[] source, int headerOffset, int dataSize)
	{
		int wavSize = BitConverter.ToInt32 (source, headerOffset);
		headerOffset += sizeof(int);
		Debug.AssertFormat (wavSize > 0 && wavSize == dataSize, "Failed to get valid 8-bit wav size: {0} from data bytes: {1} at offset: {2}", wavSize, dataSize, headerOffset);

		float[] data = new float[wavSize];

		sbyte maxValue = sbyte.MaxValue;

		int i = 0;
		while (i < wavSize) {
			data [i] = (float)source [i] / maxValue;
			++i;
		}

		return data;
	}

	private static float[] Convert16BitByteArrayToAudioClipData (byte[] source, int headerOffset, int dataSize)
	{
		int wavSize = BitConverter.ToInt32 (source, headerOffset);
		headerOffset += sizeof(int);
		Debug.AssertFormat (wavSize > 0 && wavSize == dataSize, "Failed to get valid 16-bit wav size: {0} from data bytes: {1} at offset: {2}", wavSize, dataSize, headerOffset);

		int x = sizeof(Int16); // block size = 2
		int convertedSize = wavSize / x;

		float[] data = new float[convertedSize];

		Int16 maxValue = Int16.MaxValue;

		int offset = 0;
		int i = 0;
		while (i < convertedSize) {
			offset = i * x + headerOffset;
			data [i] = (float)BitConverter.ToInt16 (source, offset) / maxValue;
			++i;
		}

		Debug.AssertFormat (data.Length == convertedSize, "AudioClip .wav data is wrong size: {0} == {1}", data.Length, convertedSize);

		return data;
	}

	private static float[] Convert24BitByteArrayToAudioClipData (byte[] source, int headerOffset, int dataSize)
	{
		int wavSize = BitConverter.ToInt32 (source, headerOffset);
		headerOffset += sizeof(int);
		Debug.AssertFormat (wavSize > 0 && wavSize == dataSize, "Failed to get valid 24-bit wav size: {0} from data bytes: {1} at offset: {2}", wavSize, dataSize, headerOffset);

		int x = 3; // block size = 3
		int convertedSize = wavSize / x;

		int maxValue = Int32.MaxValue;

		float[] data = new float[convertedSize];

		byte[] block = new byte[sizeof(int)]; // using a 4 byte block for copying 3 bytes, then copy bytes with 1 offset

		int offset = 0;
		int i = 0;
		while (i < convertedSize) {
			offset = i * x + headerOffset;
			Buffer.BlockCopy (source, offset, block, 1, x);
			data [i] = (float)BitConverter.ToInt32 (block, 0) / maxValue;
			++i;
		}

		Debug.AssertFormat (data.Length == convertedSize, "AudioClip .wav data is wrong size: {0} == {1}", data.Length, convertedSize);

		return data;
	}

	private static float[] Convert32BitByteArrayToAudioClipData (byte[] source, int headerOffset, int dataSize)
	{
		int wavSize = BitConverter.ToInt32 (source, headerOffset);
		headerOffset += sizeof(int);
		Debug.AssertFormat (wavSize > 0 && wavSize == dataSize, "Failed to get valid 32-bit wav size: {0} from data bytes: {1} at offset: {2}", wavSize, dataSize, headerOffset);

		int x = sizeof(float); //  block size = 4
		int convertedSize = wavSize / x;

		Int32 maxValue = Int32.MaxValue;

		float[] data = new float[convertedSize];

		int offset = 0;
		int i = 0;
		while (i < convertedSize) {
			offset = i * x + headerOffset;
			data [i] = (float)BitConverter.ToInt32 (source, offset) / maxValue;
			++i;
		}

		Debug.AssertFormat (data.Length == convertedSize, "AudioClip .wav data is wrong size: {0} == {1}", data.Length, convertedSize);

		return data;
	}

	#endregion

	public static byte[] FromAudioClip (AudioClip audioClip)
	{
		string file;
		return FromAudioClip (audioClip, out file, false);
	}

	public static byte[] FromAudioClip (AudioClip audioClip, out string filepath, bool saveAsFile = true, string dirname = "recordings")
	{
		MemoryStream stream = new MemoryStream ();

		const int headerSize = 44;

		// get bit depth
		UInt16 bitDepth = 16; //BitDepth (audioClip);

		// NB: Only supports 16 bit
		//Debug.AssertFormat (bitDepth == 16, "Only converting 16 bit is currently supported. The audio clip data is {0} bit.", bitDepth);

		// total file size = 44 bytes for header format and audioClip.samples * factor due to float to Int16 / sbyte conversion
		int fileSize = audioClip.samples * BlockSize_16Bit + headerSize; // BlockSize (bitDepth)

		// chunk descriptor (riff)
		WriteFileHeader (ref stream, fileSize);
		// file header (fmt)
		WriteFileFormat (ref stream, audioClip.channels, audioClip.frequency, bitDepth);
		// data chunks (data)
		WriteFileData (ref stream, audioClip, bitDepth);

		byte[] bytes = stream.ToArray ();

		// Validate total bytes
		//Debug.AssertFormat (bytes.Length == fileSize, "Unexpected AudioClip to wav format byte count: {0} == {1}", bytes.Length, fileSize);

		// Save file to persistant storage location
		if (saveAsFile) {
			filepath = string.Format ("{0}/{1}/{2}.{3}", Application.persistentDataPath, dirname, DateTime.UtcNow.ToString ("yyMMdd-HHmmss-fff"), "wav");
			Directory.CreateDirectory (Path.GetDirectoryName (filepath));
			File.WriteAllBytes (filepath, bytes);
			//Debug.Log ("Auto-saved .wav file: " + filepath);
		} else {
			filepath = null;
		}

		stream.Dispose ();

		return bytes;
	}

	#region write .wav file functions

	private static int WriteFileHeader (ref MemoryStream stream, int fileSize)
	{
		int count = 0;
		int total = 12;

		// riff chunk id
		byte[] riff = Encoding.ASCII.GetBytes ("RIFF");
		count += WriteBytesToMemoryStream (ref stream, riff, "ID");

		// riff chunk size
		int chunkSize = fileSize - 8; // total size - 8 for the other two fields in the header
		count += WriteBytesToMemoryStream (ref stream, BitConverter.GetBytes (chunkSize), "CHUNK_SIZE");

		byte[] wave = Encoding.ASCII.GetBytes ("WAVE");
		count += WriteBytesToMemoryStream (ref stream, wave, "FORMAT");

		// Validate header
		Debug.AssertFormat (count == total, "Unexpected wav descriptor byte count: {0} == {1}", count, total);

		return count;
	}

	private static int WriteFileFormat (ref MemoryStream stream, int channels, int sampleRate, UInt16 bitDepth)
	{
		int count = 0;
		int total = 24;

		byte[] id = Encoding.ASCII.GetBytes ("fmt ");
		count += WriteBytesToMemoryStream (ref stream, id, "FMT_ID");

		int subchunk1Size = 16; // 24 - 8
		count += WriteBytesToMemoryStream (ref stream, BitConverter.GetBytes (subchunk1Size), "SUBCHUNK_SIZE");

		UInt16 audioFormat = 1;
		count += WriteBytesToMemoryStream (ref stream, BitConverter.GetBytes (audioFormat), "AUDIO_FORMAT");

		UInt16 numChannels = Convert.ToUInt16 (channels);
		count += WriteBytesToMemoryStream (ref stream, BitConverter.GetBytes (numChannels), "CHANNELS");

		count += WriteBytesToMemoryStream (ref stream, BitConverter.GetBytes (sampleRate), "SAMPLE_RATE");

		int byteRate = sampleRate * channels * BytesPerSample (bitDepth);
		count += WriteBytesToMemoryStream (ref stream, BitConverter.GetBytes (byteRate), "BYTE_RATE");

		UInt16 blockAlign = Convert.ToUInt16 (channels * BytesPerSample (bitDepth));
		count += WriteBytesToMemoryStream (ref stream, BitConverter.GetBytes (blockAlign), "BLOCK_ALIGN");

		count += WriteBytesToMemoryStream (ref stream, BitConverter.GetBytes (bitDepth), "BITS_PER_SAMPLE");

		// Validate format
		Debug.AssertFormat (count == total, "Unexpected wav fmt byte count: {0} == {1}", count, total);

		return count;
	}

	private static int WriteFileData (ref MemoryStream stream, AudioClip audioClip, UInt16 bitDepth)
	{
		int count = 0;
		int total = 8;

		// Copy float[] data from AudioClip
		float[] data = new float[audioClip.samples * audioClip.channels];
		audioClip.GetData (data, 0);

		byte[] bytes = ConvertAudioClipDataToInt16ByteArray (data);

		byte[] id = Encoding.ASCII.GetBytes ("data");
		count += WriteBytesToMemoryStream (ref stream, id, "DATA_ID");

		int subchunk2Size = Convert.ToInt32 (audioClip.samples * BlockSize_16Bit); // BlockSize (bitDepth)
		count += WriteBytesToMemoryStream (ref stream, BitConverter.GetBytes (subchunk2Size), "SAMPLES");

		// Validate header
		Debug.AssertFormat (count == total, "Unexpected wav data id byte count: {0} == {1}", count, total);

		// Write bytes to stream
		count += WriteBytesToMemoryStream (ref stream, bytes, "DATA");

		// Validate audio data
		//Debug.AssertFormat (bytes.Length == subchunk2Size, "Unexpected AudioClip to wav subchunk2 size: {0} == {1}", bytes.Length, subchunk2Size);

		return count;
	}

	private static byte[] ConvertAudioClipDataToInt16ByteArray (float[] data)
	{
		MemoryStream dataStream = new MemoryStream ();

		int x = sizeof(Int16);

		Int16 maxValue = Int16.MaxValue;

		int i = 0;
		while (i < data.Length) {
			dataStream.Write (BitConverter.GetBytes (Convert.ToInt16 (data [i] * maxValue)), 0, x);
			++i;
		}
		byte[] bytes = dataStream.ToArray ();

		// Validate converted bytes
		Debug.AssertFormat (data.Length * x == bytes.Length, "Unexpected float[] to Int16 to byte[] size: {0} == {1}", data.Length * x, bytes.Length);

		dataStream.Dispose ();

		return bytes;
	}

	private static int WriteBytesToMemoryStream (ref MemoryStream stream, byte[] bytes, string tag = "")
	{
		int count = bytes.Length;
		stream.Write (bytes, 0, count);
		//Debug.LogFormat ("WAV:{0} wrote {1} bytes.", tag, count);
		return count;
	}

	#endregion

	/// <summary>
	/// Calculates the bit depth of an AudioClip
	/// </summary>
	/// <returns>The bit depth. Should be 8 or 16 or 32 bit.</returns>
	/// <param name="audioClip">Audio clip.</param>
	public static UInt16 BitDepth (AudioClip audioClip)
	{
		UInt16 bitDepth = Convert.ToUInt16 (audioClip.samples * audioClip.channels * audioClip.length / audioClip.frequency);
		Debug.AssertFormat (bitDepth == 8 || bitDepth == 16 || bitDepth == 32, "Unexpected AudioClip bit depth: {0}. Expected 8 or 16 or 32 bit.", bitDepth);
		return bitDepth;
	}

	private static int BytesPerSample (UInt16 bitDepth)
	{
		return bitDepth / 8;
	}

	private static int BlockSize (UInt16 bitDepth)
	{
		switch (bitDepth) {
		case 32:
			return sizeof(Int32); // 32-bit -> 4 bytes (Int32)
		case 16:
			return sizeof(Int16); // 16-bit -> 2 bytes (Int16)
		case 8:
			return sizeof(sbyte); // 8-bit -> 1 byte (sbyte)
		default:
			throw new Exception (bitDepth + " bit depth is not supported.");
		}
	}

	private static string FormatCode (UInt16 code)
	{
		switch (code) {
		case 1:
			return "PCM";
		case 2:
			return "ADPCM";
		case 3:
			return "IEEE";
		case 7:
			return "μ-law";
		case 65534:
			return "WaveFormatExtensable";
		default:
			Debug.LogWarning ("Unknown wav code format:" + code);
			return "";
		}
	}

}
}

public static class ExtraExtensions
{
	//Joinked from https://stackoverflow.com/questions/470256/process-waitforexit-asynchronously
	public static Task WaitForExitAsync(this Process process, 
		CancellationToken cancellationToken = default(CancellationToken))
	{
		if (process.HasExited) return Task.CompletedTask;

		var tcs = new TaskCompletionSource<object>();
		process.EnableRaisingEvents = true;
		process.Exited += (sender, args) => tcs.TrySetResult(null);
		if(cancellationToken != default(CancellationToken))
			cancellationToken.Register(() => tcs.SetCanceled());

		return process.HasExited ? Task.CompletedTask : tcs.Task;
	}
}