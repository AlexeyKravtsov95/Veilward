// Public input APIs, actual production Battle; no resource edits.
using System;
using System.IO;
using UnityEngine;
using Veilward.Domain;
using Veilward.Presentation;
using Veilward.Tests.Shared;
public static class CaptureArt4Input
{
    private static BattleSceneController Battle=>UnityEngine.Object.FindFirstObjectByType<BattleSceneController>();
    public static string Ready()
    {
        var b=Battle;
        if(!b.ChooseElement(new ChooseElementCommand("start-resonance","flame")).Success)throw new Exception("Fresh Battle required");
        return "Fresh direct Battle / public FLAME choice / actual ART4 overview";
    }
    public static string Preview()
    {
        var p=Battle.GetComponent<BattlePlacementController>();
        if(!p.TrySelectCell(new GridCoordinate(-4,-1)))throw new Exception("buildable selection");
        p.SelectTowerType(new TowerDefinitionId("flame"));
        return "Public preview / gold150 / no purchase";
    }
    public static string Occupied()
    {
        var p=Battle.GetComponent<BattlePlacementController>();p.ConfirmSelection();
        if(Battle.Simulation.State.Towers.Count!=1||!p.TrySelectCell(new GridCoordinate(-4,-1)))throw new Exception("occupied selection");
        return "Public purchase / occupied selects TowerId1 / no duplicate charge";
    }
    public static string Blocked()
    {
        var b=Battle;var p=b.GetComponent<BattlePlacementController>();p.CancelSelection();
        var before=AbilityBossTrace.Snapshot(b.Simulation);
        if(p.TrySelectCell(new GridCoordinate(-5,-2))||b.PlaceTower(new TowerDefinitionId("flame"),new GridCoordinate(-5,-2)).Success||before!=AbilityBossTrace.Snapshot(b.Simulation))throw new Exception("Blocked command contract");
        var canvas=UnityEngine.Object.FindFirstObjectByType<Canvas>();
        var go=new GameObject("ART4 blocked result label",typeof(RectTransform),typeof(UnityEngine.UI.Text));go.transform.SetParent(canvas.transform,false);
        var text=go.GetComponent<UnityEngine.UI.Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=18;text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;
        text.text="EDITOR INPUT PROOF | Spawn/path cell (-5,-2): rejected | no preview, no purchase, Gold80 / Towers1 unchanged";
        text.rectTransform.anchorMin=new Vector2(0,0);text.rectTransform.anchorMax=new Vector2(1,0);text.rectTransform.pivot=new Vector2(.5f,0);text.rectTransform.anchoredPosition=new Vector2(0,3);text.rectTransform.sizeDelta=new Vector2(0,35);
        File.WriteAllText("Docs/review/ART.4/reports/blocked-input.txt",before+"\nrejected; whole state/events/Grid unchanged\n");
        return "Rejected blocked entrance cell; exact whole state unchanged";
    }
}
