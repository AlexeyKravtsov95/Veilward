// Earned production replay, explicit test clock. No money/HP/position/DTO edits.
using System;
using System.IO;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Veilward.Domain;
using Veilward.Presentation;
using Veilward.Tests.Shared;
public sealed class CaptureArt4Combat : MonoBehaviour
{
    private const string Output="Docs/review/ART.4/";
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    private static void Call(object o,string m)=>o.GetType().GetMethod(m,Flags).Invoke(o,null);
    private static void Require(bool ok,string why){if(!ok)throw new Exception(why);}
    public static string Capture()
    {
        var go=new GameObject("ART4 earned test-clock capture");go.AddComponent<CaptureArt4Combat>().StartCoroutine("Flow");
        return "Started earned public-command test-clock replay; inspect earned-combat-completion.json";
    }
    private IEnumerator Flow()
    {
        Require(EditorApplication.isPlaying,"Actual Play Mode required");
        var b=UnityEngine.Object.FindFirstObjectByType<BattleSceneController>();var s=b.Simulation;
        Require(s.State.Towers.Count==0&&s.State.Gold==150&&s.State.Tick==0,"fresh direct Battle required");
        b.enabled=false;var script=AbilityBossTrace.Load("flame");var ability=b.GetComponent<AbilityController>();
        var control=new AbilityBossTrace.Run(AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWaveAbilityBossV2.asset").CreateSimulation(),script);control.Play();
        var camera=Camera.main;var label=new GameObject("ART4 explicit test-clock label");
        label.transform.position=camera.ViewportToWorldPoint(new Vector3(.5f,.94f,10));label.transform.rotation=camera.transform.rotation;
        var text=label.AddComponent<TextMesh>();text.fontSize=48;text.characterSize=.062f;text.anchor=TextAnchor.MiddleCenter;
        text.text="EARNED PRODUCTION REPLAY | TEST CLOCK fixed30Hz | original Battle camera | no resource substitution";
        var rows=new List<object>();bool dense=false,boss=false,castBoss=false;var maxEnemies=0;
        var shaderAsync=ShaderUtil.allowAsyncCompilation;ShaderUtil.allowAsyncCompilation=false;var captureRate=Time.captureFramerate;Time.captureFramerate=30;bool success=false;
        Require(b.ChooseElement(new ChooseElementCommand("start-resonance",script.start)).Success,"start choice");
        try
        {
            while(s.State.Phase==BattlePhase.Preparation)
            {
                var wave=s.State.WaveIndex+1;
                foreach(var command in script.commands.Where(c=>c.wave==wave))
                {
                    Require(command.op=="place"?b.PlaceTower(new TowerDefinitionId(command.type),new GridCoordinate(command.x,command.y)).Success:
                        command.op=="upgrade"?b.UpgradeTower(new TowerId(command.id)).Success:b.SellTower(new TowerId(command.id)).Success,"public economy command");
                    rows.Add(new{wave,tick=s.State.Tick,command=JsonUtility.ToJson(command),gold=s.State.Gold});
                }
                Require(b.StartWave()==WaveTransitionResult.Started,"StartWave");var guard=0;
                while(s.State.Phase==BattlePhase.Combat&&guard++<20000)
                {
                    if(script.TryCenter(s,out var center))
                    {
                        Require(ability.BeginPreview()&&ability.SelectTarget(center),"public ability preview");
                        var target=s.State.Enemies.FirstOrDefault(e=>e.Definition.Id.Value=="rift-warden");
                        if(target!=null&&!castBoss){Call(b,"LateUpdate");Pixels(camera,"earned-boss-ability-preview.png");}
                        var bossHealth=target!=null?target.Health:0;
                        Require(ability.ConfirmPreview(),"public ability Confirm");Call(b,"LateUpdate");
                        Require(ability.LastCast.HasValue&&ability.LastCast.Value.Succeeded,"guarded intent dispatched at this exact simulation tick");
                        var bossDamage=target!=null?bossHealth-target.Health:0;
                        rows.Add(new{wave,tick=s.State.Tick,ability=true,bossDamage,gold=s.State.Gold,hp=s.State.BaseHealth,sequence=s.AbilityState.NextCastSequence});
                        if(target!=null&&!castBoss){Call(b,"LateUpdate");Pixels(camera,"earned-boss-ability-impact.png");castBoss=true;}
                    }
                    if(s.State.Phase==BattlePhase.Combat)s.Step();
                    Call(b,"ProcessAttackEvents");b.GetComponent<BattleCheckpointController>().ObservePhaseTransition();Call(b,"LateUpdate");
                    typeof(BattleSceneController).GetField("_accumulator",Flags).SetValue(b,-1f);Call(b,"Update");
                    maxEnemies=Math.Max(maxEnemies,s.State.Enemies.Count);
                    if(s.State.Enemies.Count>=5&&!dense){Pixels(camera,"earned-dense-combat.png");dense=true;}
                    if(s.State.Enemies.Any(e=>e.Definition.Id.Value=="rift-warden")&&!boss)
                    {
                        var enemy=s.State.Enemies.First(e=>e.Definition.Id.Value=="rift-warden");
                        Require(b.GetComponent<EnemyInspectorController>().Select(enemy.Id),"boss Inspect");
                        Call(b,"LateUpdate");Pixels(camera,"earned-boss-inspect.png");
                        File.WriteAllText(Output+"reports/boss-inspector.txt",b.GetComponent<EnemyInspectorController>().Details+"\n");
                        b.GetComponent<EnemyInspectorController>().Close();boss=true;
                    }
                    yield return null;
                }
                Require(guard<20000,"bounded replay");
                if(s.State.Phase!=BattlePhase.WaveComplete)break;
                if(s.State.ElementProgression.PendingChoiceId!="none")
                    Require(b.ChooseElement(new ChooseElementCommand(s.State.ElementProgression.PendingChoiceId,wave==3?script.after3:script.after6)).Success,"unlock");
                Require(b.ContinueToNextWave()==WaveTransitionResult.Continued,"NextWave");
            }
            Require(s.State.Phase==BattlePhase.Victory&&AbilityBossTrace.Wire(s)==AbilityBossTrace.Wire(control.sim),"exact production final state");
            Require(boss&&castBoss,"boss and public ability evidence");
            Pixels(camera,"earned-victory8.png");
            File.WriteAllText(Output+"reports/earned-combat.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{testClock=true,productionUpdate=false,liveResourceMutation=false,maxEnemies,dense,boss,castBoss,exactFinalWire=AbilityBossTrace.Wire(s),rows},Newtonsoft.Json.Formatting.Indented)+"\n");
            success=true;
        }
        finally
        {
            File.WriteAllText(Output+"reports/earned-combat-completion.json", "{\"success\":"+success.ToString().ToLowerInvariant()+",\"testClock\":true,\"fixedCaptureFramerate\":30}\n");
            UnityEngine.Object.DestroyImmediate(label);ShaderUtil.allowAsyncCompilation=shaderAsync;Time.captureFramerate=captureRate;
        }
    }
    private static void Pixels(Camera camera,string file)
    {
        Directory.CreateDirectory(Output+"playmode");var old=camera.targetTexture;var active=RenderTexture.active;
        var rt=RenderTexture.GetTemporary(1600,900,24);Texture2D texture=null;
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture=new Texture2D(1600,900,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1600,900),0,0);texture.Apply();File.WriteAllBytes(Output+"playmode/"+file,texture.EncodeToPNG());}
        finally{camera.targetTexture=old;RenderTexture.active=active;if(texture!=null)UnityEngine.Object.DestroyImmediate(texture);RenderTexture.ReleaseTemporary(rt);}
    }
}
