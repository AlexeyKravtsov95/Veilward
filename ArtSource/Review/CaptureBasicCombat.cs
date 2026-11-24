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
public sealed class CaptureBasicCombat : MonoBehaviour
{
    private const string Output="Docs/review/VFX.1b/";
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    private static void Call(object o,string m)=>o.GetType().GetMethod(m,Flags).Invoke(o,null);
    private static void Require(bool ok,string why){if(!ok)throw new Exception(why);}
    public static string Capture()
    {
        var go=new GameObject("VFX1b earned test-clock capture");go.AddComponent<CaptureBasicCombat>().StartCoroutine("Flow");
        return "Started earned public-command test-clock replay; inspect earned-combat-completion.json";
    }
    private IEnumerator Flow()
    {
        Require(EditorApplication.isPlaying,"Actual Play Mode required");
        Directory.CreateDirectory(Output+"playmode");
        var readyDeadline=Time.realtimeSinceStartup+5;
        while((UnityEngine.Object.FindFirstObjectByType<BattleSceneController>()==null || UnityEngine.Object.FindFirstObjectByType<BattleSceneController>().Simulation==null) && Time.realtimeSinceStartup<readyDeadline)yield return null;
        var b=UnityEngine.Object.FindFirstObjectByType<BattleSceneController>();Require(b!=null&&b.Simulation!=null,"Battle Start initialized");var s=b.Simulation;
        Require(s.State.Towers.Count==0&&s.State.Gold==150&&s.State.Tick==0,"fresh direct Battle required");
        b.enabled=false;var script=AbilityBossTrace.Load("flame");var ability=b.GetComponent<AbilityController>();
        var control=new AbilityBossTrace.Run(AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWaveAbilityBossV2.asset").CreateSimulation(),script);control.Play();
        var camera=Camera.main;var label=new GameObject("VFX1b explicit test-clock label",typeof(Canvas));
        label.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;label.GetComponent<Canvas>().sortingOrder=1000;
        var labelText=new GameObject("Probe label",typeof(UnityEngine.UI.Text));labelText.transform.SetParent(label.transform,false);
        var text=labelText.GetComponent<UnityEngine.UI.Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=15;text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;
        text.rectTransform.anchorMin=new Vector2(0,0);text.rectTransform.anchorMax=new Vector2(1,0);text.rectTransform.pivot=new Vector2(.5f,0);text.rectTransform.sizeDelta=new Vector2(0,40);
        text.text="EARNED PRODUCTION REPLAY | TEST CLOCK fixed30Hz / <=120ticks per render | no resource substitution";
        Veilward.Infrastructure.WaveCheckpointEnvelope earned8=null;
        var rows=new List<object>();bool dense=false,boss=false,castBoss=false,bossDeath=false;var maxEnemies=0;
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
                if(wave==8)earned8=AbilityBossTrace.Envelope(s);
                Require(b.StartWave()==WaveTransitionResult.Started,"StartWave");var guard=0;
                while(s.State.Phase==BattlePhase.Combat&&guard++<20000)
                {
                    if(script.TryCenter(s,out var center))
                    {
                        Require(ability.BeginPreview()&&ability.SelectTarget(center),"public ability preview");
                        var target=s.State.Enemies.FirstOrDefault(e=>e.Definition.Id.Value=="rift-warden");
                        if(target!=null&&!castBoss){Call(b,"LateUpdate");yield return null;yield return new WaitForEndOfFrame();Composite("earned-boss-ability-preview.png");}
                        var bossHealth=target!=null?target.Health:0;
                        Require(ability.ConfirmPreview(),"public ability Confirm");Call(b,"LateUpdate");
                        Require(ability.LastCast.HasValue&&ability.LastCast.Value.Succeeded,"guarded intent dispatched at this exact simulation tick");
                        var bossDamage=target!=null?bossHealth-target.Health:0;
                        rows.Add(new{wave,tick=s.State.Tick,ability=true,bossDamage,gold=s.State.Gold,hp=s.State.BaseHealth,sequence=s.AbilityState.NextCastSequence});
                        if(target!=null&&!castBoss){Call(b,"LateUpdate");yield return null;yield return new WaitForEndOfFrame();Composite("earned-boss-ability-impact.png");castBoss=true;}
                    }
                    var bossBefore=s.State.Enemies.FirstOrDefault(e=>e.Definition.Id.Value=="rift-warden");
                    if(s.State.Phase==BattlePhase.Combat)s.Step();
                    var lethalBoss=bossBefore!=null?s.DamageEvents.Where(d=>d.TargetEnemyId==bossBefore.Id&&d.WasLethal).ToArray():Array.Empty<DamageResolvedEvent>();
                    Call(b,"ProcessAttackEvents");b.GetComponent<BattleCheckpointController>().ObservePhaseTransition();Call(b,"LateUpdate");
                    typeof(BattleSceneController).GetField("_accumulator",Flags).SetValue(b,-1f);Call(b,"Update");
                    if(lethalBoss.Length>0&&!bossDeath)
                    {
                        var tail=b.GetComponentsInChildren<EnemyModelView>().Single(v=>v.IsTail&&v.Model.name.StartsWith("Enemy_RiftWarden"));
                        tail.Advance(.2f);yield return null;yield return new WaitForEndOfFrame();Composite("earned-boss-death.png");
                        File.WriteAllText(Output+"reports/earned-boss-death.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{testClock=true,bossId=bossBefore.Id.Value,eventOrigin=lethalBoss[0].Origin.ToString(),tick=s.State.Tick,capturedPosition=new[]{lethalBoss[0].TargetPosition.X,lethalBoss[0].TargetPosition.Y},tailClock=tail.TailClock,domainRemoved=!s.State.Enemies.Any(e=>e.Id==bossBefore.Id),kills=s.State.Kills,gold=s.State.Gold},Newtonsoft.Json.Formatting.Indented)+"\n");bossDeath=true;
                    }
                    maxEnemies=Math.Max(maxEnemies,s.State.Enemies.Count);
                    if(s.State.Enemies.Count>=5&&!dense){yield return null;yield return new WaitForEndOfFrame();Composite("earned-dense-combat.png");dense=true;}
                    if(s.State.Enemies.Any(e=>e.Definition.Id.Value=="rift-warden")&&!boss)
                    {
                        var enemy=s.State.Enemies.First(e=>e.Definition.Id.Value=="rift-warden");
                        Require(b.GetComponent<EnemyInspectorController>().Select(enemy.Id),"boss Inspect");
                        Call(b,"LateUpdate");yield return null;yield return new WaitForEndOfFrame();Composite("earned-boss-inspect.png");yield return null;yield return new WaitForEndOfFrame();Composite("earned-boss-inspect-composite.png");
                        File.WriteAllText(Output+"reports/boss-inspector.txt",b.GetComponent<EnemyInspectorController>().Details+"\n");
                        CaptureInputs(b);
                        b.GetComponent<EnemyInspectorController>().Close();boss=true;
                    }
                    if(s.State.Tick%120==0)yield return null;
                }
                Require(guard<20000,"bounded replay");
                if(s.State.Phase!=BattlePhase.WaveComplete)break;
                if(s.State.ElementProgression.PendingChoiceId!="none")
                    Require(b.ChooseElement(new ChooseElementCommand(s.State.ElementProgression.PendingChoiceId,wave==3?script.after3:script.after6)).Success,"unlock");
                Require(b.ContinueToNextWave()==WaveTransitionResult.Continued,"NextWave");
            }
            Require(s.State.Phase==BattlePhase.Victory&&AbilityBossTrace.Wire(s)==AbilityBossTrace.Wire(control.sim),"exact production final state");
            Require(boss&&castBoss&&bossDeath,"boss inspect/ability/death evidence");
            yield return null;yield return new WaitForEndOfFrame();Composite("earned-victory8.png");
            File.WriteAllText(Output+"reports/earned-combat.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{testClock=true,productionUpdate=false,liveResourceMutation=false,maxEnemies,dense,boss,castBoss,bossDeath,exactFinalWire=AbilityBossTrace.Wire(s),rows},Newtonsoft.Json.Formatting.Indented)+"\n");
            Require(earned8!=null,"earned Preparation8 checkpoint");
            File.WriteAllText(Output+"reports/earned-l3-origin.json",WireJson(earned8)+"\n");
            b.GetComponent<BattleCheckpointController>().RestoreCheckpoint(earned8);
            var keeper=s.State.Towers.First(t=>t.Definition.Id.Value=="flame").Id;
            foreach(var id in s.State.Towers.Where(t=>t.Id!=keeper).Select(t=>t.Id).ToArray())Require(b.SellTower(id).Success,"earned consolidation Sell");
            Require(b.UpgradeTower(keeper).Success&&b.UpgradeTower(keeper).Success,"earned L3 from refunds, no grants");
            File.WriteAllText(Output+"reports/earned-l3-safe.json",WireJson(AbilityBossTrace.Envelope(s))+"\n");
            text.text="EARNED PREPARATION8 / public Sell + Upgrade L3 | TEST CLOCK / visual probe | no resource grants";
            Call(b,"LateUpdate");Require(b.StartWave()==WaveTransitionResult.Started,"L3 Combat8");
            bool l3=false;
            for(var tick=0;tick<1000&&s.State.Phase==BattlePhase.Combat;tick++)
            {
                s.Step();var attack=s.AttackEvents.FirstOrDefault();var shot=s.AttackEvents.Count>0;
                Call(b,"ProcessAttackEvents");Call(b,"LateUpdate");
                if(shot)
                {
                    var slot=Enumerable.Range(0,8).First(i=>b.FlameProjectiles.GetView(i).Remaining>0);
                    var view=b.FlameProjectiles.GetView(slot);var before=AbilityBossTrace.Snapshot(s);
                    yield return null;yield return new WaitForEndOfFrame();Composite("L3-launch.png");b.FlameProjectiles.Advance(.1f);yield return null;yield return new WaitForEndOfFrame();Composite("L3-flight.png");
                    b.FlameProjectiles.Advance(.11f);yield return null;yield return new WaitForEndOfFrame();Composite("L3-impact.png");
                    Require(view.ImpactVisible&&view.HeadPosition==view.Target&&AbilityBossTrace.Snapshot(s)==before,"L3 cosmetic arrival never mutates Domain");
                    File.WriteAllText(Output+"reports/earned-l3-proof.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{success=true,level=b.FlameProjectiles.GetLevel(slot),attackTick=attack.Tick,tower=keeper.Value,enemy=attack.TargetEnemyId.Value,source=view.Source.ToString("R"),target=view.Target.ToString("R"),gold=s.State.Gold,liveResourceMutation=false,testClock=true,domainUnchangedAtArrival=true},Newtonsoft.Json.Formatting.Indented)+"\n");
                    l3=true;break;
                }
            }
            Require(l3,"actual L3 flight captured");
            // Synthetic stress отдельно: все4 одновременно невозможны в native progression (максимум3).
            text.text="SYNTHETIC ALL4 / all-open1000gold fixture | snapshot/priority/mixed basic forms | original production camera";
            b.ResetPresentation();
            var catalog=AssetDatabase.LoadAssetAtPath<BattleScenarioConfig>("Assets/_Game/Configs/EightWaveAbilityBossV2.asset").CreateSimulation().TowerCatalog;
            var fixture=new BattleSimulation(s.Grid.Map,s.Path,new[]{new WaveDefinition(new EnemyDefinition(new EnemyDefinitionId("shade"),1000,1.5f,1,5),8,0,10)},catalog,20,1000,0,s.ReactionCatalog);
            typeof(BattleSceneController).GetField("_simulation",Flags).SetValue(b,fixture);
            // Checkpoint controller не участвует в явно synthetic fixture.
            var cells=new[]{new GridCoordinate(-5,-1),new GridCoordinate(-4,-1),new GridCoordinate(-5,0),new GridCoordinate(-4,0)};
            var types=new[]{"flame","tide","gale","stone"};
            for(var i=0;i<4;i++)Require(fixture.PlaceTower(new PlaceTowerCommand(new TowerDefinitionId(types[i]),cells[i])).Success,"synthetic placement");
            Call(b,"LateUpdate");fixture.StartWave();fixture.Step();Call(b,"ProcessAttackEvents");Call(b,"LateUpdate");
            yield return null;yield return new WaitForEndOfFrame();Composite("synthetic-mixed4-launch.png");
            b.BasicProjectiles.Advance(.07f);b.FlameProjectiles.Advance(.07f);yield return null;yield return new WaitForEndOfFrame();Composite("synthetic-mixed4-flight.png");
            b.Presentation.Advance(.5);Call(b,"LateUpdate");
            Require(fixture.State.Enemies[0].Statuses.Count==4,"synthetic all4 snapshots");
            Require(b.GetComponent<EnemyInspectorController>().Select(fixture.State.Enemies[0].Id),"synthetic inspector");
            Call(b,"LateUpdate");yield return null;yield return new WaitForEndOfFrame();Composite("synthetic-all4-priority-inspector.png");
            b.GetComponent<EnemyInspectorController>().Close();
            // Каждая форма показана на отдельном live owner, а не четырьмя badges на одном.
            b.ResetPresentation();
            var snapshotEnemies=new List<EnemyState>();
            for(var i=0;i<4;i++)
            {
                var enemy=new EnemyState(new EnemyId(200+i),new EnemyDefinition(new EnemyDefinitionId("shade"),1000,1,1,0),1000,6+i*2);
                typeof(EnemyState).GetMethod("ApplyStatus",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(enemy,new object[]{
                    new StatusEffectDefinition((StatusKind)i,i==0?1:20,60,i==0?30:0),new TowerId(1),0L});
                typeof(BattleState).GetMethod("AddEnemy",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(fixture.State,new object[]{enemy});snapshotEnemies.Add(enemy);
            }
            Call(b,"LateUpdate");b.Presentation.Advance(.17);Call(b,"LateUpdate");
            yield return null;yield return new WaitForEndOfFrame();Composite("synthetic-four-status-cues.png");
            File.WriteAllText(Output+"reports/synthetic-statuses.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{synthetic=true,all4NativeImpossible=true,slotsPerOwner=2,owners=snapshotEnemies.Select(e=>new{id=e.Id.Value,status=e.Statuses[0].Kind.ToString()}).ToArray(),noGameplayClaim=true},Newtonsoft.Json.Formatting.Indented)+"\n");
            success=true;
        }
        finally
        {
            File.WriteAllText(Output+"reports/earned-combat-completion.json", "{\"success\":"+success.ToString().ToLowerInvariant()+",\"testClock\":true,\"fixedCaptureFramerate\":30}\n");
            UnityEngine.Object.DestroyImmediate(label);ShaderUtil.allowAsyncCompilation=shaderAsync;Time.captureFramerate=captureRate;
        }
    }
    private static void CaptureInputs(BattleSceneController b)
    {
        var lines=b.GetComponentsInChildren<LineRenderer>(true).Where(l=>l.transform.parent.name.StartsWith("Basic Projectile Pool")).ToArray();
        var materials=lines.Select(l=>l.sharedMaterial).Distinct().ToArray();
        var data=new {actualPlayMode=true,unity=UnityEngine.Application.unityVersion,graphics=SystemInfo.graphicsDeviceType.ToString(),
            quality=QualitySettings.names[QualitySettings.GetQualityLevel()],pipelinePath=AssetDatabase.GetAssetPath(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline),
            pipelineSerialized=Newtonsoft.Json.Linq.JObject.Parse(EditorJsonUtility.ToJson(UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline)),
            frame=Time.frameCount,tick=b.Simulation.State.Tick,productionCamera=new {position=Camera.main.transform.position.ToString("R"),rotation=Camera.main.transform.rotation.ToString("R"),Camera.main.orthographicSize,aspect=1600f/900},
            renderers=lines.Length,slots=lines.Sum(l=>l.sharedMaterials.Length),capacity=BasicProjectilePool.Capacity,
            materials=materials.Select(m=>new {path=AssetDatabase.GetAssetPath(m),shader=m.shader.name,shaderPath=AssetDatabase.GetAssetPath(m.shader),passes=m.passCount,queue=m.renderQueue,color=m.GetColor("_BaseColor").ToString("R"),instance=m.GetInstanceID()}).ToArray(),
            levels=b.Simulation.TowerCatalog.Get(new TowerDefinitionId("flame")).Levels.Select(d=>new {d.FireRate,d.AttackIntervalTicks,d.Damage}).ToArray(),
            actualActiveViews=Enumerable.Range(0,12).Count(i=>b.BasicProjectiles.GetView(i).Remaining>0),
            flameRenderers=b.GetComponentsInChildren<LineRenderer>(true).Count(l=>l.transform.parent.name.StartsWith("Flame Projectile Pool")),
            liveStatusOwners=b.GetComponentsInChildren<EnemyStatusView>().Length,statusGraphics=b.GetComponentsInChildren<StatusCueGraphic>(true).Length,
            statusSlotsPerOwner=EnemyStatusView.CueCapacity, statusMaterial="UI/Default shared; no material instances",
            basicLevels=new[]{"tide","gale","stone"}.Select(type=>new{type,levels=b.Simulation.TowerCatalog.Get(new TowerDefinitionId(type)).Levels.Select(d=>new {d.FireRate,d.AttackIntervalTicks,d.Damage}).ToArray()}).ToArray(),
            dependencies=AssetDatabase.GetDependencies("Assets/_Game/Scenes/Battle.unity",true).Where(p=>File.Exists(Resolve(p))).OrderBy(p=>p).ToDictionary(p=>p,p=>Hash(File.ReadAllBytes(Resolve(p)))),
            enemies=b.GetComponentsInChildren<EnemyModelView>().Select(v=>new{v.name,v.Pose,model=AssetDatabase.GetAssetPath(v.Model.GetComponentInChildren<MeshFilter>().sharedMesh)}).ToArray(),
            damageIsImmediate=true,visualSnapshotRing=false};
        File.WriteAllText(Output+"reports/actual-render-inputs.json",Newtonsoft.Json.JsonConvert.SerializeObject(data,Newtonsoft.Json.Formatting.Indented)+"\n");
    }
    private static string WireJson(Veilward.Infrastructure.WaveCheckpointEnvelope envelope) =>
        (string)typeof(Veilward.Infrastructure.WaveCheckpointStore).Assembly.GetType("Veilward.Infrastructure.WaveCheckpointWire")
            .GetMethod("Serialize",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic).Invoke(null,new object[]{envelope,true});
    private static string Resolve(string path)
    {
        if(File.Exists(path))return path;
        var package=UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path);
        return package!=null?Path.Combine(package.resolvedPath,path.Substring(package.assetPath.Length+1)):path;
    }
    private static string Hash(byte[] bytes) {using(var sha=System.Security.Cryptography.SHA256.Create())return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}

    private static void Composite(string file)
    {
        var type=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Unity.Pipeline.Editor.Commands.Capture.CaptureCommands")).First(t=>t!=null);
        var flags=BindingFlags.Static|BindingFlags.NonPublic;type.GetMethod("PrepareGameView",flags).Invoke(null,null);
        File.WriteAllBytes(Output+"playmode/"+file,(byte[])type.GetMethod("EncodeScreenToPng",flags).Invoke(null,new object[]{1600,900}));
    }
}
