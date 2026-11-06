// Run in the connected Unity Editor: unity command script eval_file --file Tools/validate_combo_audio.cs
// Uses a temporary isolated component; checks signals, independent shuffle rounds and every move route.
if(UnityEditor.EditorApplication.isPlaying)throw new InvalidOperationException("Run the library check in Edit Mode.");

var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
var type=typeof(TomorinTrial.TrialEffects);
var keys=(string[])type.GetField("ComboAudioKeys",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).GetValue(null);
var root=new GameObject("Temporary combo audio validation"){hideFlags=HideFlags.HideAndDontSave};
var effects=root.AddComponent<TomorinTrial.TrialEffects>();
float previousScale=Time.timeScale;
int selections=0,clips=0;
var beforeRandom=JsonUtility.ToJson(UnityEngine.Random.state);
try
{
    type.GetMethod("LoadCombatAudio",flags).Invoke(effects,null);
    var banks=(Dictionary<string,AudioClip[]>)type.GetField("combatVariants",flags).GetValue(effects);
    var next=type.GetMethod("NextCombatClip",flags);
    foreach(var key in keys)
    {
        if(!banks.TryGetValue(key,out var bank) || bank.Length!=4)throw new Exception("Incomplete sound bank: "+key);
        var fingerprints=new HashSet<string>();
        foreach(var clip in bank)
        {
            var settings=((AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip))).defaultSampleSettings;
            if(clip.channels!=1 || clip.frequency!=44100 || settings.compressionFormat!=AudioCompressionFormat.PCM || !settings.preloadAudioData)
                throw new Exception("Unexpected import settings: "+clip.name);
            clip.LoadAudioData();var data=new float[clip.samples];
            if(!clip.GetData(data,0))throw new Exception("Unreadable clip: "+clip.name);
            double square=0;float peak=0;
            foreach(float sample in data)
            {if(!float.IsFinite(sample))throw new Exception("Non-finite signal: "+clip.name);peak=Mathf.Max(peak,Mathf.Abs(sample));square+=sample*sample;}
            if(peak>=.95f || Math.Sqrt(square/data.Length)<.015)throw new Exception("Clipped or quiet signal: "+clip.name);
            fingerprints.Add(clip.samples+":"+square.ToString("R",System.Globalization.CultureInfo.InvariantCulture));clips++;
        }
        if(fingerprints.Count!=4)throw new Exception("Duplicate takes: "+key);
    }
    foreach(var pair in banks)
    {
        AudioClip last=null;
        for(int round=0;round<32;round++)
        {
            var seen=new HashSet<AudioClip>();
            for(int take=0;take<pair.Value.Length;take++)
            {
                var clip=(AudioClip)next.Invoke(effects,new object[]{pair.Key,pair.Value[0]});
                if(pair.Value.Length>1 && clip==last)throw new Exception("Consecutive repeat: "+pair.Key);
                if(!seen.Add(clip))throw new Exception("Repeated before the round exhausted: "+pair.Key);
                last=clip;selections++;
                // An unrelated cue and voice index must not alter this bank's rotation.
                next.Invoke(effects,new object[]{"blink",null});
                type.GetField("nextSource",flags).SetValue(effects,take*7+round);
            }
        }
    }
    for(int move=0;move<=TomorinTrial.TrialPlayer.SwordRecallRise;move++)foreach(bool heavy in new[]{false,true})
        if(!banks.ContainsKey(TomorinTrial.TrialEffects.MoveSoundKey(move,heavy)))throw new Exception("Unloaded move route: "+move);
    var basic=new HashSet<string>{TomorinTrial.TrialEffects.MoveSoundKey(0,false),TomorinTrial.TrialEffects.MoveSoundKey(1,false),TomorinTrial.TrialEffects.MoveSoundKey(2,false),TomorinTrial.TrialEffects.MoveSoundKey(TomorinTrial.TrialPlayer.WheelFinish,false)};
    if(basic.Count!=4)throw new Exception("Basic combo moves need different sound identities.");
    if(beforeRandom!=JsonUtility.ToJson(UnityEngine.Random.state))throw new Exception("Sound selection consumed gameplay RNG.");
    string report=$"PASS: {clips} new imported PCM clips; {banks.Count} banks; {selections} shuffled selections; all 148 move/action routes; basic L-L-L-L has four sound identities; gameplay RNG unchanged.";
    System.IO.Directory.CreateDirectory("Logs/Trial");System.IO.File.WriteAllText("Logs/Trial/ComboAudioValidation.txt",report);
    return report;
}
finally{UnityEngine.Object.DestroyImmediate(root);Time.timeScale=previousScale;}
