using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using UnityEngine;
using Veilward.Domain;
namespace Veilward.Tests.Shared
{
    [Serializable] public sealed class BossMissionCommand { public int wave,x,y,id;public string op,type; }
    [Serializable] public sealed class BossMissionCast { public int wave;public long relativeTick;public float x,y; }
    [Serializable] public sealed class BossMissionScript
    {
        public string start,after3,after6;
        public BossMissionCommand[] commands;
        public int threshold=200,bossDelay;
        public bool earlyWave8,holdWave7;
        public BossMissionCast[] casts;
        public static BossMissionScript Load(string start,bool leak=false)
        {
            var path="Docs/review/P2.4e/revision-2/"+start+(leak?"-leak":"")+".commands.json";
            if(!File.Exists(path))return Original(start,leak);
            var script=JsonUtility.FromJson<BossMissionScript>(File.ReadAllText(path));
            if(script.threshold<=0)script.threshold=200;
            return script;
        }
        [Serializable] private sealed class OldReceipt {public int wave;public long tick;public string command;}
        [Serializable] private sealed class OldLog {public OldReceipt[] receipts;}
        public static BossMissionScript Original(string start,bool leak=false)
        {
            var script=JsonUtility.FromJson<BossMissionScript>(File.ReadAllText("Docs/review/P2.4e/"+start+".commands.json"));
            var log=JsonUtility.FromJson<OldLog>(File.ReadAllText("Docs/review/P2.4e/"+start+(leak?"-leak":"-victory")+".json"));
            var starts=new Dictionary<int,long>();var casts=new List<BossMissionCast>();
            foreach(var r in log.receipts)
            {
                if(r.command=="Start")starts[r.wave]=r.tick;
                if(r.command.StartsWith("Cast center(",StringComparison.Ordinal))
                {
                    var text=r.command.Substring(12);text=text.Substring(0,text.IndexOf(')'));var xy=text.Split(',');
                    casts.Add(new BossMissionCast{wave=r.wave,relativeTick=r.tick-starts[r.wave],x=float.Parse(xy[0],CultureInfo.InvariantCulture),y=float.Parse(xy[1],CultureInfo.InvariantCulture)});
                }
            }
            script.casts=casts.ToArray();script.threshold=200;return script;
        }
        public static IEnumerable<string[]> Choices(string start)
        {
            var all=new[]{"flame","tide","gale","stone"};
            foreach(var a in all)foreach(var b in all)if(a!=start&&b!=start&&b!=a)yield return new[]{a,b};
        }
        public bool TryCenter(BattleSimulation s,out PathPosition center)
        {
            center=default;
            if(s.State.Phase!=BattlePhase.Combat||s.AbilityState.CooldownRemainingTicks!=0)return false;
            if(holdWave7&&s.State.WaveIndex==6)return false;
            if(casts!=null)
            {
                foreach(var c in casts)if(c.wave==s.State.WaveIndex+1&&c.relativeTick==s.WaveElapsedTicks){center=new PathPosition(c.x,c.y);return true;}
                return false;
            }
            EnemyState boss=null;
            for(var i=0;i<s.State.Enemies.Count;i++)if(s.State.Enemies[i].Definition.Id.Value=="rift-warden")boss=s.State.Enemies[i];
            if(s.State.WaveIndex==7)
            {
                if(boss!=null){if(s.WaveElapsedTicks<330+bossDelay)return false;center=s.Path.GetPosition(boss.DistanceAlongPath);return true;}
                if(!earlyWave8)return false;
            }
            var best=0;var urgent=false;
            for(var i=0;i<s.State.Enemies.Count;i++)
            {
                var p=s.Path.GetPosition(s.State.Enemies[i].DistanceAlongPath);
                Score(s,p,ref best,ref center);
                if(s.State.Enemies[i].DistanceAlongPath>=s.Path.TotalLength-1)urgent=true;
                for(var j=i+1;j<s.State.Enemies.Count;j++)
                {var q=s.Path.GetPosition(s.State.Enemies[j].DistanceAlongPath);Score(s,new PathPosition((p.X+q.X)*.5f,(p.Y+q.Y)*.5f),ref best,ref center);}
            }
            return best>0&&(best>=threshold||urgent);
        }
        private static void Score(BattleSimulation s,PathPosition p,ref int best,ref PathPosition center)
        {
            var score=0;
            foreach(var e in s.State.Enemies)
            {
                var q=s.Path.GetPosition(e.DistanceAlongPath);var dx=(double)p.X-q.X;var dy=(double)p.Y-q.Y;
                if(dx*dx+dy*dy<=2.25){var armor=Math.Max(0,e.Definition.Armor-(e.Crack?.Power??0));score+=Math.Min(e.Health,Math.Max(1,120-armor));}
            }
            if(score>best){best=score;center=p;}
        }
    }
}
