using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Aipocalypse.Core.HandSystem;

public static class HandFistReview
{
    const string Left = "Assets/_Game/Prefabs/Hands/FPHand_Left.prefab";
    public static object Geometry()
    {
        var rig=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Left)).GetComponent<HandRig>();
        try
        {
            rig.Apply(new HandShape{Thumb=new FingerShape(1.8f),Index=new FingerShape(1.8f),Middle=new FingerShape(1.8f),Ring=new FingerShape(1.8f),Little=new FingerShape(1.8f)});
            var fingers=new SerializedObject(rig).FindProperty("_fingers");
            return Enumerable.Range(0,5).Select(f=>new{finger=(Finger)f,points=Enumerable.Range(0,fingers.GetArrayElementAtIndex(f).FindPropertyRelative("Joints").arraySize).Select(j=>{
                var bone=(Transform)fingers.GetArrayElementAtIndex(f).FindPropertyRelative("Joints").GetArrayElementAtIndex(j).objectReferenceValue;
                return new{bone.name,position=rig.GripPoint.InverseTransformPoint(bone.position).ToString("F4"),direction=rig.GripPoint.InverseTransformDirection(bone.up).ToString("F3")};}).ToArray()}).ToArray();
        }
        finally{UnityEngine.Object.DestroyImmediate(rig.gameObject);}
    }
    public static object Verify(string fixture="HandRigTests")
    {
        var type=AppDomain.CurrentDomain.GetAssemblies().Single(a=>a.GetName().Name=="Aipocalypse.Tests.EditMode").GetType("Aipocalypse.Tests."+fixture,true);
        var failures=new System.Collections.Generic.List<string>();int passed=0;
        foreach(var method in type.GetMethods())
        foreach(var attribute in method.GetCustomAttributes(false))
        {
            string name=attribute.GetType().FullName;
            if(name!="NUnit.Framework.TestAttribute"&&name!="NUnit.Framework.TestCaseAttribute")continue;
            var args=name.EndsWith("TestCaseAttribute")?(object[])attribute.GetType().GetProperty("Arguments").GetValue(attribute):Array.Empty<object>();
            var suite=Activator.CreateInstance(type);
            try {type.GetMethod("SetUp")?.Invoke(suite,null);method.Invoke(suite,args);passed++;}
            catch(Exception e){failures.Add(method.Name+"("+string.Join(",",args)+"): "+(e.InnerException??e).Message);}
            finally {type.GetMethod("TearDown")?.Invoke(suite,null);}
        }
        return new{fixture,passed,failed=failures.Count,failures};
    }
    public static object Weapon(string weapon="AK47",float time=1.65f)
    {
        var scene=EditorSceneManager.NewPreviewScene();
        Aipocalypse.Core.WeaponSystem.GunAnimation animation=null;
        try
        {
            var set=AssetDatabase.LoadAssetAtPath<HandSet>("Assets/_Game/Data/Hands/HandSet.asset");
            var gun=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/_Core/WeaponSystem/Prefabs/Guns/"+weapon+".prefab"));
            SceneManager.MoveGameObjectToScene(gun,scene);gun.transform.position=set.PreviewItemPosition;
            var held=new SerializedObject(gun.GetComponent<Aipocalypse.Core.WeaponSystem.HeldGun>());
            var pivot=(Transform)held.FindProperty("_posePivot").objectReferenceValue;
            var hip=(Transform)held.FindProperty("_hipPose").objectReferenceValue;
            pivot.localPosition=hip.localPosition;pivot.localRotation=hip.localRotation;
            animation=gun.GetComponentInChildren<Aipocalypse.Core.WeaponSystem.GunAnimation>();
            animation.StartAction(Aipocalypse.Core.WeaponSystem.GunAction.ClearingJam,false);
            animation.Tick(.016f,time/4f);
            var hands=gun.GetComponent<ItemHands>();
            foreach(HandSide side in new[]{HandSide.Left,HandSide.Right})
            {
                var rig=UnityEngine.Object.Instantiate(set.Get(side));
                SceneManager.MoveGameObjectToScene(rig.gameObject,scene);
                rig.transform.localScale*=hands.HandScale;
                var grip=hands.GetDefault(side);
                grip.Resolve(rig,set.Elbow(side),out var position,out var rotation);
                rig.Place(position,rotation);rig.Apply(grip.Shape);
            }
            var lamp=new GameObject("Light",typeof(Light));SceneManager.MoveGameObjectToScene(lamp,scene);
            lamp.GetComponent<Light>().type=LightType.Directional;lamp.GetComponent<Light>().intensity=1.3f;lamp.transform.rotation=Quaternion.Euler(35,-25,0);
            var go=new GameObject("Camera",typeof(Camera));SceneManager.MoveGameObjectToScene(go,scene);
            var camera=go.GetComponent<Camera>();camera.scene=scene;camera.fieldOfView=70;camera.nearClipPlane=.01f;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.13f,.16f,.2f);
            var rt=new RenderTexture(960,720,24);var texture=new Texture2D(960,720,TextureFormat.RGB24,false);
            var previous=RenderTexture.active;
            try
            {
                camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
                texture.ReadPixels(new Rect(0,0,960,720),0,0);texture.Apply();
                Directory.CreateDirectory("Temp/HandFist");File.WriteAllBytes("Temp/HandFist/"+weapon+"_"+time.ToString("F2",System.Globalization.CultureInfo.InvariantCulture)+".png",texture.EncodeToPNG());
            }
            finally {camera.targetTexture=null;RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(texture);}
            return new{weapon,time};
        }
        finally {if(animation!=null)animation.Stop();EditorSceneManager.ClosePreviewScene(scene);}
    }
    public static object Apply()
    {
        foreach(string side in new[]{"Left","Right"})
        {
            string path="Assets/_Game/Prefabs/Hands/FPHand_"+side+".prefab";
            var prefab=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var rig=prefab.GetComponent<HandRig>();
                var transforms=rig.GetComponentsInChildren<Transform>(true);
                var rotations=transforms.Select(t=>t.localRotation).ToArray();
                Calibrate(rig);
                for(int i=0;i<transforms.Length;i++)transforms[i].localRotation=rotations[i];
                PrefabUtility.SaveAsPrefabAsset(prefab,path);
            }
            finally {PrefabUtility.UnloadPrefabContents(prefab);}
        }
        int clips=0;
        foreach(string name in new[]{"AK47","MP5","Pistol","PumpShotgun","DoubleBarrel"})
        foreach(string state in new[]{"Repairing","RepairingStubborn","RepairFocus","RepairEnd"})
        {
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Game/_Core/WeaponSystem/Animations/"+name+"/"+name+"_"+state+".anim");
            var bindings=AnimationUtility.GetCurveBindings(clip);
            var thumbBinding=bindings.Single(b=>b.type==typeof(HandGrip)&&b.path.EndsWith("/Left")&&b.propertyName=="_animatedShape.Thumb.Curl");
            var originalThumb=AnimationUtility.GetEditorCurve(clip,thumbBinding);
            foreach(var binding in bindings.Where(b=>b.type==typeof(HandGrip)&&b.path.EndsWith("/Left")&&b.propertyName.StartsWith("_animatedShape.")))
            {
                var curve=AnimationUtility.GetEditorCurve(clip,binding);
                var keys=curve.keys;
                bool changed=false;
                for(int i=0;i<keys.Length;i++)
                {
                    var key=keys[i];
                    if(binding.propertyName.EndsWith(".Curl")&&key.value>1.01f)
                    {
                        key.value=FingerShape.MaxCurl;
                        if(i>0&&keys[i-1].value<=1.01f&&state.StartsWith("Repairing"))
                            key.time=keys[i-1].time+.22f+(binding.propertyName.Contains("Thumb")?.06f:binding.propertyName.Contains("Little")?-.03f:binding.propertyName.Contains("Ring")?-.015f:binding.propertyName.Contains("Middle")?-.005f:0f);
                        keys[i]=key; changed=true;
                    }
                    if(binding.propertyName=="_animatedShape.Thumb.Spread"&&originalThumb.Evaluate(key.time)>1.01f)
                    {key.value=0f;keys[i]=key;changed=true;}
                }
                if(!changed)continue;
                curve.keys=keys;
                for(int i=0;i<keys.Length;i++)
                {
                    AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);
                    AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.ClampedAuto);
                }
                AnimationUtility.SetEditorCurve(clip,binding,curve);
            }
            EditorUtility.SetDirty(clip);AssetDatabase.SaveAssetIfDirty(clip);clips++;
        }
        return new{calibratedHands=2,repairClips=clips};
    }

    static void Calibrate(HandRig rig)
    {
        rig.Apply(default);
        var serialized=new SerializedObject(rig);
        var fingers=serialized.FindProperty("_fingers");
        var middle=(Transform)fingers.GetArrayElementAtIndex(2).FindPropertyRelative("Joints").GetArrayElementAtIndex(1).objectReferenceValue;
        Vector3 normal=rig.GripPoint.up;
        Vector3 forward=Vector3.ProjectOnPlane(middle.up,normal).normalized;
        for(int f=0;f<fingers.arraySize;f++)
        {
            var finger=fingers.GetArrayElementAtIndex(f);
            var joints=finger.FindPropertyRelative("Joints");
            var mcp=(Transform)joints.GetArrayElementAtIndex(f==0?0:1).objectReferenceValue;
            var axis=Vector3.Cross(mcp.up,-normal).normalized;
            var localAxes=Enumerable.Range(0,joints.arraySize).Select(j=>((Transform)joints.GetArrayElementAtIndex(j).objectReferenceValue).InverseTransformDirection(axis)).ToArray();
            float adduct=Vector3.SignedAngle(Vector3.ProjectOnPlane(mcp.up,normal),forward,normal)*.85f;
            var fist=finger.FindPropertyRelative("Fist");fist.arraySize=joints.arraySize;
            for(int j=0;j<joints.arraySize;j++)
            {
                var joint=(Transform)joints.GetArrayElementAtIndex(j).objectReferenceValue;
                var open=finger.FindPropertyRelative("Open").GetArrayElementAtIndex(j).quaternionValue;
                var closed=finger.FindPropertyRelative("Closed").GetArrayElementAtIndex(j).quaternionValue;
                if(f==0)joint.localRotation=Quaternion.SlerpUnclamped(open,closed,new[]{1.3f,1.45f,1.4f}[j]);
                else if(j==0)joint.localRotation=closed;
                else
                {
                    joint.localRotation=open;
                    if(j==1)joint.rotation=Quaternion.AngleAxis(adduct,normal)*joint.rotation;
                    joint.localRotation*=Quaternion.AngleAxis(new[]{0f,65f,85f,15f}[j],localAxes[j]);
                }
                fist.GetArrayElementAtIndex(j).quaternionValue=joint.localRotation;
            }
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
    public static object Inspect()
    {
        var rig = AssetDatabase.LoadAssetAtPath<GameObject>(Left).GetComponent<HandRig>();
        var fingers = new SerializedObject(rig).FindProperty("_fingers");
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Game/_Core/WeaponSystem/Animations/AK47/AK47_Repairing.anim");
        return new {
            fingers = Enumerable.Range(0, fingers.arraySize).Select(f => {
                var finger = fingers.GetArrayElementAtIndex(f);
                var joints = finger.FindPropertyRelative("Joints");
                return new { finger = (Finger)f, joints = Enumerable.Range(0, joints.arraySize).Select(j => new {
                    name = joints.GetArrayElementAtIndex(j).objectReferenceValue.name,
                    localPosition = ((Transform)joints.GetArrayElementAtIndex(j).objectReferenceValue).localPosition.ToString("F4"),
                    open = finger.FindPropertyRelative("Open").GetArrayElementAtIndex(j).quaternionValue.eulerAngles.ToString("F1"),
                    closed = finger.FindPropertyRelative("Closed").GetArrayElementAtIndex(j).quaternionValue.eulerAngles.ToString("F1"),
                    delta = (Quaternion.Inverse(finger.FindPropertyRelative("Open").GetArrayElementAtIndex(j).quaternionValue) * finger.FindPropertyRelative("Closed").GetArrayElementAtIndex(j).quaternionValue).eulerAngles.ToString("F1")
                }).ToArray() };
            }).ToArray(),
            curves = AnimationUtility.GetCurveBindings(clip).Where(b => b.type == typeof(HandGrip) && b.path.EndsWith("/Left")).Select(b => new {b.propertyName, keys = AnimationUtility.GetEditorCurve(clip,b).keys.Select(k => new {k.time,k.value}).ToArray()}).ToArray()
        };
    }

    public static object Render(string label = "Fist", float time = 1.7f)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var rig = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Left)).GetComponent<HandRig>();
            SceneManager.MoveGameObjectToScene(rig.gameObject, scene);
            var gun = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/_Core/WeaponSystem/Prefabs/Guns/AK47.prefab"));
            SceneManager.MoveGameObjectToScene(gun,scene);
            var animation = gun.GetComponentInChildren<Aipocalypse.Core.WeaponSystem.GunAnimation>();
            animation.StartAction(Aipocalypse.Core.WeaponSystem.GunAction.ClearingJam,false);
            animation.Tick(.016f,time / 4f);
            var shape = animation.transform.Find("Motion/HandTargets/Left").GetComponent<HandGrip>().Shape;
            rig.Apply(shape);
            rig.Place(Vector3.zero,Quaternion.identity);
            gun.SetActive(false);
            var renderer = rig.GetComponentInChildren<SkinnedMeshRenderer>();
            var mesh = new Mesh(); renderer.BakeMesh(mesh);
            var bounds = new Bounds(renderer.transform.position + renderer.transform.rotation * mesh.vertices[0],Vector3.zero);
            foreach(var vertex in mesh.vertices) bounds.Encapsulate(renderer.transform.position + renderer.transform.rotation * vertex);
            var light = new GameObject("Light",typeof(Light));
            SceneManager.MoveGameObjectToScene(light,scene);
            light.GetComponent<Light>().type = LightType.Directional;
            light.GetComponent<Light>().intensity = 1.3f;
            light.transform.rotation = Quaternion.Euler(35,-25,0);
            var cameraGo = new GameObject("Camera",typeof(Camera));
            SceneManager.MoveGameObjectToScene(cameraGo,scene);
            var camera = cameraGo.GetComponent<Camera>();
            camera.scene = scene; camera.orthographic = true; camera.orthographicSize = bounds.size.magnitude * .48f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.13f,.16f,.2f);
            camera.nearClipPlane=.01f; camera.farClipPlane=3f;
            Directory.CreateDirectory("Temp/HandFist");
            var directions = new[]{new Vector3(0,1,-.3f),new Vector3(0,-1,-.3f),new Vector3(-1,.25f,-.25f)};
            for(int i=0;i<directions.Length;i++)
            {
                camera.transform.position = bounds.center + directions[i].normalized * .6f;
                camera.transform.LookAt(bounds.center,Vector3.forward);
                var rt = new RenderTexture(512,512,24);
                var texture = new Texture2D(512,512,TextureFormat.RGB24,false);
                camera.targetTexture=rt; camera.Render();
                var previous=RenderTexture.active; RenderTexture.active=rt;
                texture.ReadPixels(new Rect(0,0,512,512),0,0); texture.Apply();
                File.WriteAllBytes("Temp/HandFist/"+label+"_"+i+".png",texture.EncodeToPNG());
                camera.targetTexture=null; RenderTexture.active=previous;
                UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(rt);
            }
            animation.Stop(); UnityEngine.Object.DestroyImmediate(mesh);
            return new{label,time,shape,bounds=bounds.ToString(),scale=rig.transform.lossyScale.ToString(),rendererScale=renderer.transform.lossyScale.ToString()};
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
