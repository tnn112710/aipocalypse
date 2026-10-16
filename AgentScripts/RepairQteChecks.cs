using System;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Aipocalypse.Core.Bootstrap;
using Aipocalypse.Core.PlayerSystem;
using Aipocalypse.Core.WeaponSystem;
using Aipocalypse.Core.QuickTimeSystem;
using Aipocalypse.Core.TimeSystem;
using VContainer;
using Cysharp.Threading.Tasks;
using Object=UnityEngine.Object;

public static class RepairQteChecks
{
    static QuickTimeService Qte => Object.FindAnyObjectByType<ProjectLifetimeScope>().Container.Resolve<QuickTimeService>();
    static TimeService Clock => Object.FindAnyObjectByType<ProjectLifetimeScope>().Container.Resolve<TimeService>();
    static Player Player => Object.FindAnyObjectByType<Player>();
    static HeldGun Held => Player.HeldItem as HeldGun;

    public static object Status() => new { playing=Application.isPlaying,paused=EditorApplication.isPaused,scale=Time.timeScale,active=Qte.IsActive,
        progress=(Qte.Session as TimingQteSession)?.Progress,misses=(Qte.Session as TimingQteSession)?.Misses,
        jammed=Held.Gun.Item.IsJammed, action=Held.Gun.Action.ToString(), rounds=Held.Gun.Item.Rounds, inputEnabled=Object.FindAnyObjectByType<ProjectLifetimeScope>().Container.Resolve<Aipocalypse.Input.InputService>().GameplayEnabled,
        focused=Application.isFocused,keyboardEnabled=Keyboard.current.enabled};

    public static Task<object> Stage(string presentation="Circle") => StageAsync(presentation).AsTask();
    static async UniTask<object> StageAsync(string presentation)
    {
        if(!Application.isPlaying || Held?.Gun?.Item.Gun.Jam.Repair==null)throw new InvalidOperationException("Play with a configured gun equipped first.");
        EditorApplication.isPaused=false;
        Application.runInBackground=true;
        var held=Held;
        held.Gun.Interrupt();
        var challenge=(TimingQteChallenge)held.Gun.Item.Gun.Jam.Repair.Qte;
        string[] saved=challenge.Presentations;
        challenge.Presentations=new[]{presentation};
        try
        {
            held.Gun.Item.SetJammed(true);
            held.Gun.Item.SetRounds(Mathf.Max(1, held.Gun.Item.Gun.Magazine.Size - 3));
            held.Gun.Tick(new GunInput{IsReady=true,CanFire=true,CanAim=true,Reload=true},0f);
            float start=Time.realtimeSinceStartup;
            float maxHandError=0f;
            while(!Qte.IsActive || ((TimingQteSession)Qte.Session).Progress < challenge.GreenZoneCenter)
            {
                await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
                if(Time.realtimeSinceStartup-start>12f)throw new TimeoutException("QTE did not reach the green zone.");
                if(Qte.IsActive && held.Gun.ActionProgress<.73f)
                {
                    var root=held.GetComponentInChildren<GunAnimation>().transform;
                    maxHandError=Mathf.Max(maxHandError,Vector3.Distance(root.Find("Motion/HandTargets/Left").position,root.Find("Motion/Controls/LeftFree").position));
                }
            }
            EditorApplication.isPaused=true;
            return new{presentation=Qte.Presentation, progress=((TimingQteSession)Qte.Session).Progress, repairProgress=held.Gun.ActionProgress, worldScale=Time.timeScale,
                stubborn=held.Gun.RepairIsStubborn, maxHandError, hud=Object.FindAnyObjectByType<Aipocalypse.Core.UI.RepairQteHud>()!=null};
        }
        finally{challenge.Presentations=saved;}
    }

    public static Task<object> PressSuccess() => PressSuccessAsync().AsTask();
    static async UniTask<object> PressSuccessAsync()
    {
        var held=Held;
        if(!Qte.IsActive)throw new InvalidOperationException("Stage a QTE first.");
        var timing=(TimingQteSession)Qte.Session;
        if(timing.Progress<timing.ZoneStart || timing.Progress>timing.ZoneStart+timing.ZoneWidth)throw new InvalidOperationException("Input is outside green zone.");
        int rounds=held.Gun.Item.Rounds;
        Clock.SetPaused(true);
        EditorApplication.isPaused=false;
        for(int i=0;i<3;i++)await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
        Clock.SetPaused(false);
        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.R));
        try
        {
            for(int i=0;i<3;i++)await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
        }
        finally {InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());}
        for(int i=0;i<3;i++)await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
        if(held.Gun.Item.IsJammed || Qte.IsActive || Mathf.Abs(Time.timeScale-1f)>.001f || held.Gun.Item.Rounds!=rounds || held.Gun.Action!=GunAction.None)
            throw new InvalidOperationException("R input did not clear the jam and restore time without changing ammo. Result="+timing.Result+" "+UnityEngine.JsonUtility.ToJson(new DebugState{jammed=held.Gun.Item.IsJammed,active=Qte.IsActive,scale=Time.timeScale,rounds=held.Gun.Item.Rounds,misses=timing.Misses}));
        return new{success=true,jammed=held.Gun.Item.IsJammed,action=held.Gun.Action.ToString(),worldScale=Time.timeScale,rounds};
    }
    [Serializable] class DebugState {public bool jammed,active;public float scale;public int rounds,misses;}

    public static Task<object> NaturalFinish(float trigger=.5f,bool miss=false,bool stubborn=false) => NaturalFinishAsync(trigger,miss,stubborn).AsTask();
    static async UniTask<object> NaturalFinishAsync(float trigger,bool miss,bool stubborn)
    {
        Application.runInBackground=true;
        EditorApplication.isPaused=false;
        await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
        var held=Held;
        held.Gun.Interrupt();
        var profile=held.Gun.Item.Gun.Jam.Repair;
        var challenge=(TimingQteChallenge)profile.Qte;
        float savedTrigger=profile.QteStartProgress,savedChance=profile.StubbornChance,savedDuration=challenge.TotalDuration;
        profile.QteStartProgress=trigger;
        profile.StubbornChance=stubborn?1f:0f;
        challenge.TotalDuration=.1f;
        float expectedDuration=stubborn?profile.StubbornDuration:profile.QuickDuration;
        float start=Time.realtimeSinceStartup;
        double gameStart=Time.timeAsDouble;
        float firstQteProgress=-1f;
        bool sawLateQte=false,pressed=false;
        int releaseAfter=0;
        TimingQteSession session=null;
        var animation=held.GetComponentInChildren<GunAnimation>();
        var source=held.GetComponent<AudioSource>();
        int impactCount=0,soundStarts=0;
        Action impact=()=>{impactCount++;if(source!=null && source.isPlaying)soundStarts++;};
        animation.RepairSlapped+=impact;
        try
        {
            held.Gun.Item.SetJammed(true);
            held.Gun.Tick(new GunInput{IsReady=true,CanFire=true,CanAim=true},0f);
            if(held.Gun.Action!=GunAction.ClearingJam)throw new InvalidOperationException("Automatic repair did not start.");
            while(held.Gun.Action==GunAction.ClearingJam)
            {
                await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
                if(releaseAfter>0 && --releaseAfter==0)InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());
                if(Qte.IsActive)
                {
                    session=(TimingQteSession)Qte.Session;
                    if(firstQteProgress<0f)firstQteProgress=held.Gun.ActionProgress;
                    if(held.Gun.ActionProgress>.95f)sawLateQte=true;
                    if(miss && !pressed && session.Progress>.06f && session.Progress<.3f)
                    {
                        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.R));
                        pressed=true;releaseAfter=2;
                    }
                    if(Time.timeScale!=1f)throw new InvalidOperationException("Repair QTE slowed time.");
                    if(held.GetComponentInChildren<GunAnimation>().State==GunAnimation.RepairFocus)throw new InvalidOperationException("Repair animation entered its old hold loop.");
                }
                else if(firstQteProgress>=0f && held.Gun.Action==GunAction.ClearingJam)
                    throw new InvalidOperationException("QTE disappeared before repair completion.");
                if(Time.realtimeSinceStartup-start>8f)throw new TimeoutException("Repair did not complete within its duration.");
            }
            float elapsed=Time.realtimeSinceStartup-start;
            double gameElapsed=Time.timeAsDouble-gameStart;
            if(held.Gun.Item.IsJammed || Qte.IsActive || !sawLateQte || firstQteProgress<trigger || firstQteProgress-trigger>.03f || Math.Abs(gameElapsed-expectedDuration)>.15f || (miss && session.Misses!=1))
                throw new InvalidOperationException($"Natural completion check: jam={held.Gun.Item.IsJammed}, active={Qte.IsActive}, late={sawLateQte}, onset={firstQteProgress}, trigger={trigger}, elapsed={elapsed}, expected={expectedDuration}, misses={session?.Misses}.");
            int expectedImpacts=stubborn?4:3;
            if(impactCount!=expectedImpacts || soundStarts!=expectedImpacts)throw new InvalidOperationException($"Repair impact/audio mismatch: {impactCount} impacts, {soundStarts} sound starts, expected {expectedImpacts}.");
            return new{automatic=true,stubborn,expectedDuration,elapsed,gameElapsed,trigger,firstQteProgress,qteVisiblePast95Percent=sawLateQte,standaloneTimeoutIgnored=true,misses=session.Misses,scale=Time.timeScale,impactCount,soundStarts};
        }
        finally
        {
            animation.RepairSlapped-=impact;
            InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());
            profile.QteStartProgress=savedTrigger;profile.StubbornChance=savedChance;challenge.TotalDuration=savedDuration;
        }
    }

    public static Task<object> CancelAndPause() => CancelAndPauseAsync().AsTask();
    static async UniTask<object> CancelAndPauseAsync()
    {
        await StageAsync("Bar");
        var held=Held;
        float remaining=Qte.Session.Remaining;
        Clock.SetPaused(true);
        Qte.Advance(10f,true);
        if(Qte.Session.Remaining!=remaining || Time.timeScale!=0f)throw new InvalidOperationException("Pause advanced the QTE.");
        Clock.SetPaused(false);
        object other=new object();
        Clock.Request(other,.1f);
        held.Gun.Interrupt();
        if(Qte.IsActive || !held.Gun.Item.IsJammed || Mathf.Abs(Time.timeScale-.1f)>.001f)throw new InvalidOperationException("Cancel cleared jam or removed another slow-motion request.");
        Clock.Release(other);
        EditorApplication.isPaused=false;
        await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
        return new{cancelRetainedJam=held.Gun.Item.IsJammed,scale=Time.timeScale,pausedQteRetainedTime=true,otherSlowMotionRetained=true};
    }

    public static Task<object> MissThenSuccess() => MissThenSuccessAsync().AsTask();
    static async UniTask<object> MissThenSuccessAsync()
    {
        await StageAsync("Bar");
        var session=(TimingQteSession)Qte.Session;
        Clock.SetPaused(true);
        EditorApplication.isPaused=false;
        for(int i=0;i<3;i++)await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
        Clock.SetPaused(false);
        float start=Time.realtimeSinceStartup;
        while(session.Progress>.15f)
        {
            await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
            if(Time.realtimeSinceStartup-start>2f)throw new TimeoutException("No next fill.");
        }
        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.R));
        for(int i=0;i<2;i++)await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());
        await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
        if(session.Misses!=1 || !Qte.IsActive || !Held.Gun.Item.IsJammed)throw new InvalidOperationException("Miss did not retain the jam and event.");
        while(!session.AttemptAvailable || session.Progress<.70f)
        {
            await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
            if(Time.realtimeSinceStartup-start>3f)throw new TimeoutException("No retry opportunity.");
        }
        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState(Key.R));
        for(int i=0;i<2;i++)await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
        InputSystem.QueueStateEvent(Keyboard.current,new KeyboardState());
        await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
        if(session.Result!=QteResult.Success || Held.Gun.Item.IsJammed || Held.Gun.Action!=GunAction.None || Time.timeScale!=1f)
            throw new InvalidOperationException("Retry did not clear the jam or the QTE input also started a reload.");
        return new{misses=session.Misses,retrySucceeded=true,scale=Time.timeScale,rounds=Held.Gun.Item.Rounds,action=Held.Gun.Action.ToString()};
    }

    public static Task<object> SwitchCancel() => SwitchCancelAsync().AsTask();
    static async UniTask<object> SwitchCancelAsync()
    {
        var equipment=Player.Equipment;
        var current=Held.Item;
        equipment.Select(equipment.Hotbar.IndexOf(current));
        equipment.Switcher.EquipImmediately(current);
        await StageAsync("Circle");
        var held=Held;
        var session=Qte.Session;
        int next=-1;
        for(int i=0;i<equipment.Hotbar.Count;i++)
            if(equipment.Hotbar[i]!=null && equipment.Hotbar[i]!=held.Item) {next=i;break;}
        if(next<0)throw new InvalidOperationException("Need another equipped item to test switching.");
        Clock.SetPaused(true);
        EditorApplication.isPaused=false;
        for(int i=0;i<3;i++)await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
        Clock.SetPaused(false);
        equipment.Select(next);
        for(int i=0;i<3;i++)await UniTask.NextFrame(PlayerLoopTiming.LastPostLateUpdate);
        if(Qte.IsActive || session.Result!=QteResult.Cancelled || !held.Gun.Item.IsJammed || Time.timeScale!=1f)
            throw new InvalidOperationException("Switch did not cancel QTE, retain jam and restore time.");
        return new{switchCancelled=true,scale=Time.timeScale,jamRetained=held.Gun.Item.IsJammed,sessionResult=session.Result.ToString()};
    }
}
