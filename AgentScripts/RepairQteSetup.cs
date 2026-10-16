using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Aipocalypse.Core.HandSystem;
using Aipocalypse.Core.WeaponSystem;
using Aipocalypse.Core.QuickTimeSystem;
using Aipocalypse.Core.UI;
using TMPro;
using Object = UnityEngine.Object;

public static class RepairQteSetup
{
    const string Data = "Assets/_Game/Data/Weapons";
    const string Clips = "Assets/_Game/_Core/WeaponSystem/Animations/AK47";
    const string Shared = "Assets/_Game/_Core/WeaponSystem/Animations/Shared";
    const string Hud = "Assets/_Game/_Core/UI/Prefabs/HUD.prefab";
    const string Sprites = "Assets/Synty/InterfaceApocalypseHUD/Sprites/HUD/SPR_HUD_Apocalypse_";

    public static object Inspect()
    {
        var gun = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/_Core/WeaponSystem/Prefabs/Guns/AK47.prefab");
        var root = gun.GetComponentInChildren<GunAnimation>(true).transform;
        var constraints = root.GetComponentsInChildren<UnityEngine.Animations.Rigging.MultiParentConstraint>(true);
        return new {
            parts = root.GetComponentsInChildren<Transform>(true).Where(t => t.name.Contains("Contact") || t.name.Contains("Grip") || t.name.Contains("Bolt") || t.name == "LeftFree")
                .Select(t => new { path = AnimationUtility.CalculateTransformPath(t, root), p = root.Find("Motion").InverseTransformPoint(t.position).ToString("F4"), r = (Quaternion.Inverse(root.Find("Motion").rotation) * t.rotation).eulerAngles.ToString("F1") }).ToArray(),
            sources = constraints.Select(c => new { c.name, targets = c.data.sourceObjects.Select(s => AnimationUtility.CalculateTransformPath(s.transform, root)).ToArray() }).ToArray(),
            hud = AssetDatabase.LoadAssetAtPath<GameObject>(Hud).GetComponentsInChildren<Transform>(true).Where(t => t.name == "Center").Select(t => AnimationUtility.CalculateTransformPath(t, t.root)).ToArray(),
            key = AssetDatabase.LoadAssetAtPath<InteractPromptStyle>("Assets/_Game/Data/UI/InteractPromptStyle.asset")?.KeySprite?.name,
        };
    }

    public static object Build()
    {
        Folder(Data);
        var challenge = Asset<TimingQteChallenge>(Data + "/AK47_RepairQte.asset");
        var profile = Asset<GunRepairProfile>(Data + "/AK47_Repair.asset");
        profile.Qte = challenge;
        EditorUtility.SetDirty(profile);
        var definition = AssetDatabase.LoadAssetAtPath<GunDefinition>("Assets/_Game/Data/Items/AK47.asset");
        definition.Jam.Repair = profile;
        EditorUtility.SetDirty(definition);
        BuildAnimations(profile);
        BuildHud();
        AutomaticRepair();
        AssetDatabase.SaveAssets();
        return new { profile = AssetDatabase.GetAssetPath(profile), qte = AssetDatabase.GetAssetPath(challenge), animations = Clips, hud = Hud };
    }

    public static object Review()
    {
        var profile=AssetDatabase.LoadAssetAtPath<GunRepairProfile>(Data+"/AK47_Repair.asset");
        var challenge=(TimingQteChallenge)profile.Qte;
        var hud=AssetDatabase.LoadAssetAtPath<GameObject>(Hud);
        var views=hud.GetComponentsInChildren<QteView>(true);
        var style=AssetDatabase.LoadAssetAtPath<QteStyle>("Assets/_Game/Data/UI/RepairQteStyle.asset");
        if(style.Font==null || style.Panel==null || style.Ring==null || style.Bar==null || views.Length!=2)
            throw new InvalidOperationException("Missing QTE HUD style assets or views");
        Directory.CreateDirectory("Temp/RepairQte");
        foreach(string name in new[]{"RepairQte-Circle.png","RepairQte-Bar.png"})
        {
            string source="Assets/Temp/"+name;
            if(File.Exists(source)) {File.Copy(source,"Temp/RepairQte/"+name,true);AssetDatabase.DeleteAsset(source);}
        }
        return new {profile.QuickDuration,profile.StubbornDuration,profile.StubbornChance,profile.QteStartProgress,
            challenge.TotalDuration,challenge.CycleDuration,challenge.GreenZoneCenter,challenge.GreenZoneWidth,challenge.WorldTimeScale,
            challenge.Presentations, views=views.Select(v=>new{v.PresentationId,path=AnimationUtility.CalculateTransformPath(v.transform,hud.transform)}).ToArray(),
            sceneDirty=UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().isDirty};
    }

    public static object AutomaticRepair()
    {
        var profile=AssetDatabase.LoadAssetAtPath<GunRepairProfile>(Data+"/AK47_Repair.asset");
        var challenge=(TimingQteChallenge)profile.Qte;
        profile.RequireReloadPress=false;
        profile.QuickDuration=4f;
        profile.StubbornDuration=5f;
        profile.QteStartProgress=.5f;
        profile.StubbornChance=.5f;
        challenge.TotalDuration=4f;
        challenge.WorldTimeScale=1f;
        EditorUtility.SetDirty(profile);
        EditorUtility.SetDirty(challenge);
        foreach(var pair in new[]{(state:"Repairing",duration:profile.QuickDuration),(state:"RepairingStubborn",duration:profile.StubbornDuration)})
        {
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Clips+"/AK47_"+pair.state+".anim");
            Rescale(clip,pair.duration);
            EditorUtility.SetDirty(clip);
        }
        var hud=PrefabUtility.LoadPrefabContents(Hud);
        try
        {
            var root=(RectTransform)hud.transform.Find("SafeFrame/Center/RepairQte");
            if(root==null)throw new InvalidOperationException("Existing RepairQte widget missing");
            root.anchorMin=root.anchorMax=new Vector2(.5f,.5f);
            root.anchoredPosition=new Vector2(-80f,-60f);
            root.sizeDelta=new Vector2(230f,180f);
            var content=(RectTransform)root.Find("Content");
            content.sizeDelta=root.sizeDelta;
            foreach(string name in new[]{"Panel","Title","Hint","Remaining"})
                if(content.Find(name)!=null)content.Find(name).gameObject.SetActive(false);
            var circle=(RectTransform)content.Find("Circle");
            circle.anchoredPosition=Vector2.zero;
            circle.sizeDelta=new Vector2(112f,112f);
            foreach(string name in new[]{"Track","Fill","GreenZone"})
                ((RectTransform)circle.Find(name)).sizeDelta=circle.sizeDelta;
            var bar=(RectTransform)content.Find("Bar");
            bar.anchoredPosition=Vector2.zero;
            bar.sizeDelta=new Vector2(210f,20f);
            foreach(string name in new[]{"Track","Fill"})
                ((RectTransform)bar.Find(name)).sizeDelta=bar.sizeDelta;
            var marker=(RectTransform)bar.Find("Marker");
            marker.sizeDelta=new Vector2(3f,32f);
            var key=(RectTransform)content.Find("KeyCap");
            key.anchoredPosition=new Vector2(0f,-78f);
            key.sizeDelta=new Vector2(30f,30f);
            ((RectTransform)key.Find("Key")).sizeDelta=key.sizeDelta;
            key.gameObject.SetActive(true);
            content.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(hud,Hud);
        }
        finally {PrefabUtility.UnloadPrefabContents(hud);}
        AssetDatabase.SaveAssets();
        return new{profile.RequireReloadPress,profile.QuickDuration,profile.StubbornDuration,profile.QteStartProgress,challenge.WorldTimeScale};
    }

    public static object SlapSounds()
    {
        var definition=AssetDatabase.LoadAssetAtPath<GunDefinition>("Assets/_Game/Data/Items/AK47.asset");
        string folder="Assets/_StoreAssets/Animals/Malbers Animations/Common/Audio/";
        var clips=new[]{AssetDatabase.LoadAssetAtPath<AudioClip>(folder+"Hits/Hit2.wav"),AssetDatabase.LoadAssetAtPath<AudioClip>(folder+"Weapons/Bow/Wood Impact.wav")};
        if(clips.Any(c=>c==null))throw new InvalidOperationException("Slap sound source missing");
        Undo.RecordObject(definition,"AK47 repair impact sounds");
        definition.Sounds.RepairSlaps=clips;
        definition.Sounds.RepairSlapVolume=.55f;
        EditorUtility.SetDirty(definition);
        AssetDatabase.SaveAssetIfDirty(definition);
        return new{clips=clips.Select(c=>new{c.name,c.length}).ToArray(),definition.Sounds.RepairSlapVolume};
    }

    public static object OtherGuns()
    {
        var source=AssetDatabase.LoadAssetAtPath<GunDefinition>("Assets/_Game/Data/Items/AK47.asset");
        var results=new List<object>();
        foreach(string name in new[]{"MP5","Pistol","PumpShotgun","DoubleBarrel"})
        {
            string profilePath=Data+"/"+name+"_Repair.asset";
            string qtePath=Data+"/"+name+"_RepairQte.asset";
            if(AssetDatabase.LoadAssetAtPath<GunRepairProfile>(profilePath)==null)
                if(!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source.Jam.Repair),profilePath))throw new InvalidOperationException("Could not create "+profilePath);
            if(AssetDatabase.LoadAssetAtPath<QteChallenge>(qtePath)==null)
                if(!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(source.Jam.Repair.Qte),qtePath))throw new InvalidOperationException("Could not create "+qtePath);
            var profile=AssetDatabase.LoadAssetAtPath<GunRepairProfile>(profilePath);
            profile.Qte=AssetDatabase.LoadAssetAtPath<QteChallenge>(qtePath);
            var gun=AssetDatabase.LoadAssetAtPath<GunDefinition>("Assets/_Game/Data/Items/"+name+".asset");
            gun.Jam.Repair=profile;
            gun.Sounds.RepairSlaps=source.Sounds.RepairSlaps.ToArray();
            gun.Sounds.RepairSlapVolume=source.Sounds.RepairSlapVolume;
            EditorUtility.SetDirty(profile);EditorUtility.SetDirty(gun);
            AssetDatabase.SaveAssetIfDirty(profile);AssetDatabase.SaveAssetIfDirty(gun);
            results.Add(new{name,profilePath,qtePath,profile.QuickDuration,profile.StubbornDuration,profile.QteStartProgress});
        }
        return results;
    }

    public static object IsolateFont()
    {
        const string path="Assets/_Game/Data/UI/RepairQteFont.asset";
        var style=AssetDatabase.LoadAssetAtPath<QteStyle>("Assets/_Game/Data/UI/RepairQteStyle.asset");
        if(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path)==null)
            if(!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(style.Font),path))throw new InvalidOperationException("Font copy failed");
        style.Font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        EditorUtility.SetDirty(style);
        var hud=PrefabUtility.LoadPrefabContents(Hud);
        try
        {
            foreach(var label in hud.transform.Find("SafeFrame/Center/RepairQte").GetComponentsInChildren<TMP_Text>(true))
            {
                label.font=style.Font;
                label.fontSharedMaterial=style.Font.material;
            }
            PrefabUtility.SaveAsPrefabAsset(hud,Hud);
        }
        finally {PrefabUtility.UnloadPrefabContents(hud);}
        AssetDatabase.SaveAssets();
        return new{font=path,material=AssetDatabase.GetAssetPath(style.Font.material),texture=AssetDatabase.GetAssetPath(style.Font.atlasTexture)};
    }


    static T Asset<T>(string path) where T : ScriptableObject
    {
        var asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    static void Folder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        int slash = path.LastIndexOf('/');
        Folder(path.Substring(0, slash));
        AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
    }

    static void Set(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void BuildAnimations(GunRepairProfile profile)
    {
        var gun = PrefabUtility.LoadPrefabContents("Assets/_Game/_Core/WeaponSystem/Prefabs/Guns/AK47.prefab");
        try
        {
            var root = gun.GetComponentInChildren<GunAnimation>(true).transform;
            var motion = root.Find("Motion");
            var bolt = motion.Find("Weapon/Bolt");
            var forend = motion.Find("Weapon/Handguard/LeftGrip");
            var boltGrip = bolt.Find("LeftGrip");
            var free = motion.Find("Controls/LeftFree");
            Vector3 start = motion.InverseTransformPoint(forend.position);
            Vector3 grasp = motion.InverseTransformPoint(boltGrip.position);
            Quaternion startR = Quaternion.Inverse(motion.rotation) * forend.rotation;
            Quaternion graspR = Quaternion.Inverse(motion.rotation) * boltGrip.rotation;
            Vector3 held = new(-0.035f, 0.065f, 0.025f);
            Vector3 tilt = new(-10f, 25f, -20f);
            Vector3 boltRest = bolt.localPosition;
            var template = AssetDatabase.LoadAssetAtPath<AnimationClip>(Clips + "/AK47_Repairing.anim");
            var reload = AssetDatabase.LoadAssetAtPath<AnimationClip>(Clips + "/AK47_ReloadingEmpty.anim");
            reload.SampleAnimation(root.gameObject,0f);
            var forendShape = motion.Find("HandTargets/Left").GetComponent<HandGrip>().Shape;
            reload.SampleAnimation(root.gameObject,reload.length*.81f);
            var boltShape = motion.Find("HandTargets/Left").GetComponent<HandGrip>().Shape;
            reload.SampleAnimation(root.gameObject,0f);
            var quick = Object.Instantiate(template);
            quick.name = "AK47_Repairing";
            Rescale(quick, profile.QuickDuration);
            var stubborn = Object.Instantiate(quick);
            stubborn.name = "AK47_RepairingStubborn";
            foreach (var pair in new[] { (clip: quick, duration: profile.QuickDuration, stubborn: false), (clip: stubborn, duration: profile.StubbornDuration, stubborn: true) })
            {
                AnimationClip clip = pair.clip;
                float d = pair.duration;
                Rescale(clip, d);
                Position(clip, "Motion", d, (0f, Vector3.zero), (0.16f, held), (0.5f, held), (0.68f, held), (0.72f, held + new Vector3(0f,-0.005f,0.008f)), (0.8f,held), (1f,Vector3.zero));
                Rotation(clip, "Motion", d, (0f, Quaternion.identity), (0.16f, Quaternion.Euler(tilt)), (0.68f,Quaternion.Euler(tilt)), (0.72f,Quaternion.Euler(tilt + new Vector3(-1f,2f,-2f))), (0.8f,Quaternion.Euler(tilt)), (1f,Quaternion.identity));
                Position(clip, "Motion/Controls/LeftFree", d, (0f,start),(0.04f,start),(0.12f,start + new Vector3(-0.10f,0.10f,-0.04f)),(0.23f,grasp),(0.74f,grasp), (0.81f,grasp + new Vector3(-0.06f,0.05f,0.02f)), (0.94f,start),(1f,start));
                Rotation(clip, "Motion/Controls/LeftFree", d, (0f,startR),(0.04f,startR),(0.23f,graspR),(0.74f,graspR),(0.94f,startR),(1f,startR));
                var strokes = pair.stubborn
                    ? new[] { (0f,0f),(.23f,0f),(.30f,.018f),(.36f,0f),(.44f,.02f),(.5f,.02f),(.60f,.035f),(.65f,.078f),(.70f,0f),(1f,0f) }
                    : new[] { (0f,0f),(.23f,0f),(.44f,.02f),(.5f,.02f),(.64f,.075f),(.70f,0f),(1f,0f) };
                Position(clip,"Motion/Weapon/Bolt",d,strokes.Select(s => (s.Item1,boltRest + Vector3.back*s.Item2)).ToArray());
                Weights(clip,d,(0f,0),(.04f,4),(.23f,3),(.74f,3),(.76f,4),(.94f,0),(1f,0));
                Shape(clip,d,(0f,forendShape),(.13f,default(HandShape)),(.23f,boltShape),(.74f,boltShape),(.82f,default(HandShape)),(.94f,forendShape),(1f,forendShape));
            }
            var focus = Frozen(quick, 0.5f, "AK47_RepairFocus", 0.8f);
            Position(focus,"Motion/Weapon/Bolt",.8f,(0f,boltRest+Vector3.back*.02f),(.5f,boltRest+Vector3.back*.024f),(1f,boltRest+Vector3.back*.02f));
            var recovery = Frozen(quick,.5f,"AK47_RepairEnd",.28f);
            Position(recovery,"Motion",.28f,(0f,held),(1f,Vector3.zero));
            Rotation(recovery,"Motion",.28f,(0f,Quaternion.Euler(tilt)),(1f,Quaternion.identity));
            Position(recovery,"Motion/Weapon/Bolt",.28f,(0f,boltRest+Vector3.back*.02f),(.24f,boltRest),(1f,boltRest));
            Position(recovery,"Motion/Controls/LeftFree",.28f,(0f,grasp+Vector3.back*.02f),(.25f,grasp),(.55f,grasp+new Vector3(-.07f,.04f,.02f)),(1f,start));
            Rotation(recovery,"Motion/Controls/LeftFree",.28f,(0f,graspR),(.25f,graspR),(1f,startR));
            Weights(recovery,.28f,(0f,4),(1f,0));
            Shape(recovery,.28f,(0f,boltShape),(.5f,default(HandShape)),(1f,forendShape));

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(Shared + "/Gun.controller");
            var overrides = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(Clips + "/AK47.overrideController");
            var mappings = new List<KeyValuePair<AnimationClip,AnimationClip>>();
            overrides.GetOverrides(mappings);
            var saved = new List<(string state,AnimationClip placeholder,AnimationClip clip)>();
            foreach (AnimationClip clip in new[] {quick,stubborn,focus,recovery})
            {
                string state = clip.name.Substring("AK47_".Length);
                string path = Clips + "/" + clip.name + ".anim";
                var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (existing == null) { AssetDatabase.CreateAsset(clip,path); existing=clip; }
                else { EditorUtility.CopySerialized(clip,existing); Object.DestroyImmediate(clip); EditorUtility.SetDirty(existing); }
                var stateObject = controller.layers[0].stateMachine.states.Select(s=>s.state).FirstOrDefault(s=>s.name==state);
                if (stateObject == null)
                {
                    var placeholder = new AnimationClip {name=state};
                    placeholder.SetCurve("Motion",typeof(Transform),"m_LocalPosition.x",AnimationCurve.Linear(0f,0f,1f,0f));
                    AssetDatabase.CreateAsset(placeholder,Shared+"/"+state+".anim");
                    stateObject=controller.layers[0].stateMachine.AddState(state);
                    stateObject.motion=placeholder;
                    stateObject.writeDefaultValues=false;
                }
                saved.Add((state,(AnimationClip)stateObject.motion,existing));
            }
            overrides.GetOverrides(mappings);
            foreach(var item in saved)
            {
                int index=mappings.FindIndex(p=>p.Key==item.placeholder);
                if(index<0)throw new InvalidOperationException("Missing override slot "+item.state);
                mappings[index]=new KeyValuePair<AnimationClip,AnimationClip>(item.placeholder,item.clip);
            }
            overrides.ApplyOverrides(mappings);
            EditorUtility.SetDirty(controller);
            EditorUtility.SetDirty(overrides);
        }
        finally { PrefabUtility.UnloadPrefabContents(gun); }
    }

    static AnimationClip Frozen(AnimationClip source,float t,string name,float duration)
    {
        var clip = Object.Instantiate(source); clip.name=name;
        foreach(var binding in AnimationUtility.GetCurveBindings(clip))
        {
            float value=AnimationUtility.GetEditorCurve(source,binding).Evaluate(source.length*t);
            AnimationUtility.SetEditorCurve(clip,binding,AnimationCurve.Constant(0f,duration,value));
        }
        return clip;
    }

    static void Rescale(AnimationClip clip,float duration)
    {
        float factor=duration/Mathf.Max(.001f,clip.length);
        foreach(var binding in AnimationUtility.GetCurveBindings(clip))
        {
            var curve=AnimationUtility.GetEditorCurve(clip,binding);
            var keys=curve.keys;
            for(int i=0;i<keys.Length;i++) {keys[i].time*=factor; keys[i].inTangent/=factor; keys[i].outTangent/=factor;}
            curve.keys=keys;
            AnimationUtility.SetEditorCurve(clip,binding,curve);
        }
    }

    static void Curve(AnimationClip clip,string path,Type type,string property,float duration,(float t,float v)[] values,bool stepped=false)
    {
        var curve=new AnimationCurve(values.Select(k=>new Keyframe(k.t*duration,k.v)).ToArray());
        for(int i=0;i<curve.length;i++)
        {
            var mode=stepped?AnimationUtility.TangentMode.Constant:AnimationUtility.TangentMode.ClampedAuto;
            AnimationUtility.SetKeyLeftTangentMode(curve,i,mode); AnimationUtility.SetKeyRightTangentMode(curve,i,mode);
        }
        AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,type,property),curve);
    }
    static void Position(AnimationClip clip,string path,float d,params (float t,Vector3 v)[] keys)
    {
        for(int i=0;i<3;i++) {int axis=i;Curve(clip,path,typeof(Transform),"m_LocalPosition."+"xyz"[i],d,keys.Select(k=>(k.t,k.v[axis])).ToArray());}
    }
    static void Rotation(AnimationClip clip,string path,float d,params (float t,Quaternion v)[] keys)
    {
        for(int i=0;i<4;i++) {int axis=i;Curve(clip,path,typeof(Transform),"m_LocalRotation."+"xyzw"[i],d,keys.Select(k=>(k.t,k.v[axis])).ToArray());}
        clip.EnsureQuaternionContinuity();
    }
    static void Weights(AnimationClip clip,float d,params (float t,int source)[] keys)
    {
        for(int i=0;i<5;i++) {int index=i;Curve(clip,"Rig/LeftHand",typeof(UnityEngine.Animations.Rigging.MultiParentConstraint),"m_Data.m_SourceObjects.m_Item"+i+".weight",d,keys.Select(k=>(k.t,k.source==index?1f:0f)).ToArray(),true);}
    }
    static void Shape(AnimationClip clip,float d,params (float t,HandShape shape)[] keys)
    {
        string[] fingers={"Thumb","Index","Middle","Ring","Little"};
        for(int i=0;i<5;i++)
        {
            int f=i;
            foreach(string part in new[]{"Curl","Spread","Tip"})
                Curve(clip,"Motion/HandTargets/Left",typeof(HandGrip),"_animatedShape."+fingers[i]+"."+part,d,
                    keys.Select(k=>(k.t,part=="Curl"?k.shape[f].Curl:part=="Spread"?k.shape[f].Spread:k.shape[f].Tip)).ToArray());
        }
    }

    static void BuildHud()
    {
        var style=Asset<QteStyle>("Assets/_Game/Data/UI/RepairQteStyle.asset");
        style.Panel=AssetDatabase.LoadAssetAtPath<Sprite>(Sprites+"Box_Large_01.png");
        style.Ring=AssetDatabase.LoadAssetAtPath<Sprite>(Sprites+"Ring_Medium_01_Clean.png");
        style.Bar=AssetDatabase.LoadAssetAtPath<Sprite>(Sprites+"Bar_Horizontal_01.png");
        var promptStyle=AssetDatabase.LoadAssetAtPath<InteractPromptStyle>("Assets/_Game/Data/UI/InteractPromptStyle.asset");
        const string fontPath="Assets/_Game/Data/UI/RepairQteFont.asset";
        if(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath)==null)
            if(!AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(promptStyle.Font),fontPath))throw new InvalidOperationException("Font copy failed");
        style.KeyCap=promptStyle.KeySprite; style.Font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(fontPath);
        EditorUtility.SetDirty(style);
        var hud=PrefabUtility.LoadPrefabContents(Hud);
        try
        {
            var center=hud.transform.Find("SafeFrame/Center");
            if(center==null)throw new InvalidOperationException("HUD Center missing");
            var existing=center.Find("RepairQte");
            if(existing!=null)throw new InvalidOperationException("QTE HUD already exists; edit it in place");
            var root=Rect("RepairQte",center,new Vector2(450,300),new Vector2(0,180));
            root.gameObject.AddComponent<Canvas>();
            var presenter=root.gameObject.AddComponent<RepairQteHud>();
            var content=Rect("Content",root,new Vector2(450,300));
            var panel=Image("Panel",content,new Vector2(450,300),Vector2.zero,style.Panel,style.Background);
            panel.type=UnityEngine.UI.Image.Type.Sliced; panel.gameObject.SetActive(false);
            var title=Label("Title",content,new Vector2(420,40),new Vector2(0,119),style,"CLEAR THE JAM",30);
            var hint=Label("Hint",content,new Vector2(420,34),new Vector2(0,-94),style,"PRESS IN THE GREEN ZONE",23);
            title.gameObject.SetActive(false); hint.gameObject.SetActive(false);
            var cap=Image("KeyCap",content,new Vector2(42,42),new Vector2(0,-132),style.KeyCap,Color.white);
            var key=Label("Key",cap.transform,new Vector2(42,42),Vector2.zero,style,"R",25); key.color=Color.black;
            var lifetime=Image("Remaining",content,new Vector2(360,3),new Vector2(0,-64),style.Bar,style.Fill);
            lifetime.type=UnityEngine.UI.Image.Type.Filled; lifetime.fillMethod=UnityEngine.UI.Image.FillMethod.Horizontal; lifetime.fillOrigin=0; lifetime.gameObject.SetActive(false);
            var views=new List<TimingQteView>();
            foreach(bool radial in new[]{true,false})
            {
                string id=radial?"Circle":"Bar";
                Vector2 size=radial?new Vector2(132,132):new Vector2(344,28);
                var container=Rect(id,content,size,new Vector2(0,18));
                var view=container.gameObject.AddComponent<TimingQteView>();
                var track=Image("Track",container,size,Vector2.zero,radial?style.Ring:style.Bar,style.Track);
                var fill=Image("Fill",container,size,Vector2.zero,track.sprite,style.Fill);
                fill.type=UnityEngine.UI.Image.Type.Filled;
                fill.fillMethod=radial?UnityEngine.UI.Image.FillMethod.Radial360:UnityEngine.UI.Image.FillMethod.Horizontal;
                fill.fillOrigin=radial?2:0; fill.fillClockwise=true;
                var zone=Image("GreenZone",container,size,Vector2.zero,track.sprite,style.GreenZone);
                if(radial) {zone.type=UnityEngine.UI.Image.Type.Filled;zone.fillMethod=UnityEngine.UI.Image.FillMethod.Radial360;zone.fillOrigin=2;zone.fillClockwise=true;}
                var marker=Image("Marker",container,new Vector2(4,radial?19:46),Vector2.zero,null,style.Text);
                Set(view,"_style",style);Set(view,"_track",track);Set(view,"_fill",fill);Set(view,"_zone",zone);Set(view,"_marker",marker);
                var so=new SerializedObject(view);so.FindProperty("_radial").boolValue=radial;so.FindProperty("_presentationId").stringValue=id;so.ApplyModifiedPropertiesWithoutUndo();
                views.Add(view);
                view.Display(new TimingQteSession(AssetDatabase.LoadAssetAtPath<TimingQteChallenge>(Data+"/AK47_RepairQte.asset")));
                container.gameObject.SetActive(radial);
            }
            Set(presenter,"_style",style);Set(presenter,"_content",content.gameObject);Set(presenter,"_keyCap",cap);Set(presenter,"_key",key);
            var ps=new SerializedObject(presenter);var array=ps.FindProperty("_views");array.arraySize=views.Count;for(int i=0;i<views.Count;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=views[i];ps.ApplyModifiedPropertiesWithoutUndo();
            content.gameObject.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(hud,Hud);
        }
        finally {PrefabUtility.UnloadPrefabContents(hud);}
    }

    static RectTransform Rect(string name,Transform parent,Vector2 size,Vector2 position=default)
    {
        var rt=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();rt.SetParent(parent,false);
        rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(.5f,.5f);rt.sizeDelta=size;rt.anchoredPosition=position;return rt;
    }
    static UnityEngine.UI.Image Image(string name,Transform parent,Vector2 size,Vector2 pos,Sprite sprite,Color color)
    {
        var rt=Rect(name,parent,size,pos);var image=rt.gameObject.AddComponent<UnityEngine.UI.Image>();image.sprite=sprite;image.color=color;image.raycastTarget=false;return image;
    }
    static TextMeshProUGUI Label(string name,Transform parent,Vector2 size,Vector2 pos,QteStyle style,string text,float fontSize)
    {
        var label=Rect(name,parent,size,pos).gameObject.AddComponent<TextMeshProUGUI>();label.font=style.Font;label.fontSize=fontSize;label.color=style.Text;label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;label.SetText(text);return label;
    }
}
