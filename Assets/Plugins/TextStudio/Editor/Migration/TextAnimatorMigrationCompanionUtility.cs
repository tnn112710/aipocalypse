using System;
using System.Collections.Generic;
using System.Reflection;
using ImpossibleRobert.Common;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace TextStudio.Editor
{
    internal readonly struct TextAnimatorSoundWriterSnapshot
    {
        public readonly AudioClip[] Sounds;
        public readonly bool RandomSequence;
        public readonly bool InterruptPreviousSound;
        public readonly float MinSoundDelay;
        public readonly AudioSource Source;

        public TextAnimatorSoundWriterSnapshot(
            AudioClip[] sounds,
            bool randomSequence,
            bool interruptPreviousSound,
            float minSoundDelay,
            AudioSource source)
        {
            Sounds = sounds ?? Array.Empty<AudioClip>();
            RandomSequence = randomSequence;
            InterruptPreviousSound = interruptPreviousSound;
            MinSoundDelay = Mathf.Max(0f, minSoundDelay);
            Source = source;
        }
    }

    internal readonly struct TextAnimatorSoundWriterMapping
    {
        public readonly AudioClip[] GlyphClips;
        public readonly TextStudioAudioPlayerClipSequenceMode ClipSequenceMode;
        public readonly TextStudioAudioPlayerPlaybackMode PlaybackMode;
        public readonly int SourcePoolSize;
        public readonly float CooldownSeconds;
        public readonly AudioSource SourceTemplate;
        public readonly bool IgnoreWhitespaceAndPunctuation;

        public TextAnimatorSoundWriterMapping(
            AudioClip[] glyphClips,
            TextStudioAudioPlayerClipSequenceMode clipSequenceMode,
            TextStudioAudioPlayerPlaybackMode playbackMode,
            int sourcePoolSize,
            float cooldownSeconds,
            AudioSource sourceTemplate,
            bool ignoreWhitespaceAndPunctuation)
        {
            GlyphClips = glyphClips ?? Array.Empty<AudioClip>();
            ClipSequenceMode = clipSequenceMode;
            PlaybackMode = playbackMode;
            SourcePoolSize = Mathf.Max(1, sourcePoolSize);
            CooldownSeconds = Mathf.Max(0f, cooldownSeconds);
            SourceTemplate = sourceTemplate;
            IgnoreWhitespaceAndPunctuation = ignoreWhitespaceAndPunctuation;
        }
    }

    internal readonly struct TextAnimatorEventCopyReport
    {
        public readonly int CopiedListenerCount;
        public readonly int ManualRewireListenerCount;

        public TextAnimatorEventCopyReport(int copiedListenerCount, int manualRewireListenerCount)
        {
            CopiedListenerCount = Mathf.Max(0, copiedListenerCount);
            ManualRewireListenerCount = Mathf.Max(0, manualRewireListenerCount);
        }
    }

#if UNITY_6000_7_OR_NEWER
    // Event rules are immutable migration metadata and remain valid for the code-load lifetime.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class TextAnimatorMigrationCompanionUtility
    {
        internal const string TAnimSoundWriterGuid = "2d68c28ce7b16334a80fe39389408715";
        internal const string TAnimSoundWriterTypeName = "Febucci.UI.Examples.TAnimSoundWriter";

        const int PersistentListenerModeVoid = (int)PersistentListenerMode.Void;

        static readonly TextAnimatorEventRule[] TypewriterEventRules =
        {
            new TextAnimatorEventRule("onTextShowed", "_onTextShown", false, "OnTextShown"),
            new TextAnimatorEventRule("onTypewriterStart", "_onTypewriterStarted", false, "OnTypewriterStarted"),
            new TextAnimatorEventRule("onTextDisappeared", "_onTextDisappeared", false, "OnTextDisappeared"),
            new TextAnimatorEventRule("onCharacterVisible", "_onCharacterVisible", true, "OnCharacterVisible"),
            new TextAnimatorEventRule("onCharacterWaitStarted", "_onWaitStarted", true, "OnWaitStarted"),
            new TextAnimatorEventRule("onCharacterWaitFinished", "_onWaitFinished", true, "OnWaitFinished"),
            new TextAnimatorEventRule("onMessage", "_onMessage", true, "OnMessage")
        };

        public static bool IsTAnimSoundWriter(string typeName, string scriptGuid)
        {
            return string.Equals(scriptGuid, TAnimSoundWriterGuid, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(typeName, TAnimSoundWriterTypeName, StringComparison.Ordinal);
        }

        public static bool IsCompanionArtifact(TextAnimatorMigrationArtifactKind kind)
        {
            return kind == TextAnimatorMigrationArtifactKind.SoundWriterComponent ||
                   kind == TextAnimatorMigrationArtifactKind.TypewriterEventComponent;
        }

        public static TextAnimatorSoundWriterSnapshot ReadSoundWriterSnapshot(SerializedObject serializedObject)
        {
            if (serializedObject == null)
                return new TextAnimatorSoundWriterSnapshot(Array.Empty<AudioClip>(), false, true, 0f, null);

            return new TextAnimatorSoundWriterSnapshot(
                ReadAudioClipArray(serializedObject.FindProperty("sounds")),
                ReadBool(serializedObject.FindProperty("randomSequence")),
                ReadBool(serializedObject.FindProperty("interruptPreviousSound"), true),
                ReadFloat(serializedObject.FindProperty("minSoundDelay")),
                ReadObject<AudioSource>(serializedObject.FindProperty("source")));
        }

        public static TextAnimatorSoundWriterMapping MapSoundWriterSnapshot(TextAnimatorSoundWriterSnapshot snapshot)
        {
            bool interruptPrevious = snapshot.InterruptPreviousSound;
            return new TextAnimatorSoundWriterMapping(
                snapshot.Sounds,
                snapshot.RandomSequence
                    ? TextStudioAudioPlayerClipSequenceMode.Random
                    : TextStudioAudioPlayerClipSequenceMode.Sequence,
                interruptPrevious
                    ? TextStudioAudioPlayerPlaybackMode.InterruptPrevious
                    : TextStudioAudioPlayerPlaybackMode.Overlap,
                interruptPrevious ? 1 : 4,
                snapshot.MinSoundDelay,
                snapshot.Source,
                false);
        }

        public static void ApplySoundWriterMapping(
            TextStudioAudioPlayer writer,
            TextAnimatorSoundWriterMapping mapping)
        {
            if (writer == null)
                return;

            writer.SettingsSource = TextStudioAudioPlayerSettingsSource.Inline;
            writer.AudioSet = null;
            writer.SetClips(mapping.GlyphClips);
            writer.ClipSequenceMode = mapping.ClipSequenceMode;
            writer.PlaybackMode = mapping.PlaybackMode;
            writer.SourcePoolSize = mapping.SourcePoolSize;
            writer.CooldownSeconds = mapping.CooldownSeconds;
            writer.SourceTemplate = mapping.SourceTemplate;
            writer.IgnoreWhitespaceAndPunctuation = mapping.IgnoreWhitespaceAndPunctuation;
        }

        public static bool HasTypewriterEventListeners(SerializedObject serializedObject)
        {
            return CountTypewriterEventListeners(serializedObject) > 0;
        }

        public static int CountTypewriterEventListeners(SerializedObject serializedObject)
        {
            if (serializedObject == null)
                return 0;

            int count = 0;
            for (int i = 0; i < TypewriterEventRules.Length; i++)
                count += CountPersistentListeners(serializedObject.FindProperty(TypewriterEventRules[i].SourceProperty));
            return count;
        }

        public static int CountManualTypewriterListeners(SerializedObject serializedObject)
        {
            if (serializedObject == null)
                return 0;

            int count = 0;
            for (int i = 0; i < TypewriterEventRules.Length; i++)
            {
                if (!TypewriterEventRules[i].IsTyped)
                    continue;

                count += CountNonVoidPersistentListeners(serializedObject.FindProperty(TypewriterEventRules[i].SourceProperty));
            }

            return count;
        }

        public static string BuildTypewriterEventNote(SerializedObject serializedObject)
        {
            int total = CountTypewriterEventListeners(serializedObject);
            int manual = CountManualTypewriterListeners(serializedObject);
            if (total == 0)
                return "Text Animator typewriter events are available for Text Studio relays.";

            string copyText = (total - manual) == 1
                ? "1 parameterless listener can be copied"
                : (total - manual) + " parameterless listeners can be copied";
            string manualText = manual == 1
                ? "1 typed dynamic listener needs manual rewiring"
                : manual + " typed dynamic listeners need manual rewiring";
            return copyText + "; " + manualText + " on the Text Studio Typewriter Event Relay.";
        }

        public static TextAnimatorEventCopyReport CopyCompatibleTypewriterListeners(
            SerializedObject source,
            SerializedObject destination)
        {
            if (source == null || destination == null)
                return new TextAnimatorEventCopyReport(0, 0);

            int copied = 0;
            int manual = 0;
            for (int i = 0; i < TypewriterEventRules.Length; i++)
            {
                TextAnimatorEventRule rule = TypewriterEventRules[i];
                SerializedProperty sourceEvent = source.FindProperty(rule.SourceProperty);
                SerializedProperty destinationEvent = destination.FindProperty(rule.DestinationProperty);
                SerializedProperty sourceCalls = GetPersistentCalls(sourceEvent);
                SerializedProperty destinationCalls = GetPersistentCalls(destinationEvent);
                if (sourceCalls == null || destinationCalls == null || !sourceCalls.isArray || !destinationCalls.isArray)
                    continue;

                for (int callIndex = 0; callIndex < sourceCalls.arraySize; callIndex++)
                {
                    SerializedProperty sourceCall = sourceCalls.GetArrayElementAtIndex(callIndex);
                    if (IsParameterlessPersistentCall(sourceCall))
                    {
                        // Repeated migration runs keep one copy of each listener.
                        if (!ContainsPersistentCall(destinationCalls, sourceCall))
                            AppendPersistentCall(destinationCalls, sourceCall, forceVoidMode: true);
                        copied++;
                    }
                    else if (rule.IsTyped)
                    {
                        manual++;
                    }
                }
            }

            destination.ApplyModifiedProperties();
            return new TextAnimatorEventCopyReport(copied, manual);
        }

        internal static bool TryCreateSoundWriter(Component sourceComponent)
        {
            TextStudioAudioPlayer writer = FindOrAddComponent<TextStudioAudioPlayer>(sourceComponent.gameObject);
            if (writer == null)
                return false;

            TextStudioText target = sourceComponent.GetComponent<TextStudioText>();
            Undo.RecordObject(writer, "Create Text Studio Companions");
            writer.Target = target;
            ApplySoundWriterMapping(
                writer,
                MapSoundWriterSnapshot(ReadSoundWriterSnapshot(new SerializedObject(sourceComponent))));
            MarkChanged(writer);
            return true;
        }

        internal static bool TryCreateEventRelay(Component sourceComponent, out TextAnimatorEventCopyReport report)
        {
            TextStudioTypewriterEventRelay relay = FindOrAddComponent<TextStudioTypewriterEventRelay>(sourceComponent.gameObject);
            if (relay == null)
            {
                report = new TextAnimatorEventCopyReport(0, 0);
                return false;
            }

            Undo.RecordObject(relay, "Create Text Studio Companions");
            report = CopyCompatibleTypewriterListeners(
                new SerializedObject(sourceComponent),
                new SerializedObject(relay));
            MarkChanged(relay);
            return true;
        }

        static T FindOrAddComponent<T>(GameObject gameObject) where T : Component
        {
            if (gameObject == null)
                return null;

            T existing = gameObject.GetComponent<T>();
            if (existing != null)
                return existing;

            return Undo.AddComponent<T>(gameObject);
        }

        static void MarkChanged(Component component)
        {
            if (component == null)
                return;

            EditorUtility.SetDirty(component);
            EditorUtility.SetDirty(component.gameObject);
            Scene scene = component.gameObject.scene;
            if (scene.IsValid() && !string.IsNullOrEmpty(scene.path))
                EditorSceneManager.MarkSceneDirty(scene);
        }

        static AudioClip[] ReadAudioClipArray(SerializedProperty property)
        {
            if (property == null || !property.isArray)
                return Array.Empty<AudioClip>();

            List<AudioClip> clips = new List<AudioClip>(property.arraySize);
            for (int i = 0; i < property.arraySize; i++)
            {
                AudioClip clip = property.GetArrayElementAtIndex(i).objectReferenceValue as AudioClip;
                if (clip != null)
                    clips.Add(clip);
            }

            return clips.ToArray();
        }

        static bool ReadBool(SerializedProperty property, bool defaultValue = false)
        {
            return property != null && property.propertyType == SerializedPropertyType.Boolean
                ? property.boolValue
                : defaultValue;
        }

        static float ReadFloat(SerializedProperty property)
        {
            return property != null && property.propertyType == SerializedPropertyType.Float
                ? property.floatValue
                : 0f;
        }

        static T ReadObject<T>(SerializedProperty property) where T : UnityEngine.Object
        {
            return property != null && property.propertyType == SerializedPropertyType.ObjectReference
                ? property.objectReferenceValue as T
                : null;
        }

        static int CountPersistentListeners(SerializedProperty unityEvent)
        {
            SerializedProperty calls = GetPersistentCalls(unityEvent);
            return calls != null && calls.isArray ? calls.arraySize : 0;
        }

        static int CountNonVoidPersistentListeners(SerializedProperty unityEvent)
        {
            SerializedProperty calls = GetPersistentCalls(unityEvent);
            if (calls == null || !calls.isArray)
                return 0;

            int count = 0;
            for (int i = 0; i < calls.arraySize; i++)
            {
                if (ReadListenerMode(calls.GetArrayElementAtIndex(i)) != PersistentListenerModeVoid)
                    count++;
            }

            return count;
        }

        static SerializedProperty GetPersistentCalls(SerializedProperty unityEvent)
        {
            return unityEvent?.FindPropertyRelative("m_PersistentCalls.m_Calls");
        }

        static int ReadListenerMode(SerializedProperty call)
        {
            SerializedProperty mode = call?.FindPropertyRelative("m_Mode");
            if (mode == null)
                return -1;

            if (mode.propertyType == SerializedPropertyType.Enum)
                return mode.enumValueIndex;

            int enumValueIndex = mode.enumValueIndex;
            return enumValueIndex >= 0 ? enumValueIndex : mode.intValue;
        }

        static bool IsParameterlessPersistentCall(SerializedProperty call)
        {
            if (ReadListenerMode(call) == PersistentListenerModeVoid)
                return true;

            SerializedProperty targetProperty = call?.FindPropertyRelative("m_Target");
            SerializedProperty methodProperty = call?.FindPropertyRelative("m_MethodName");
            UnityEngine.Object target = targetProperty != null ? targetProperty.objectReferenceValue : null;
            string methodName = methodProperty != null ? methodProperty.stringValue : string.Empty;
            if (target == null || string.IsNullOrEmpty(methodName))
                return false;

            MethodInfo[] methods = target.GetType().GetMethods(
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                MethodInfo method = methods[i];
                if (method != null &&
                    string.Equals(method.Name, methodName, StringComparison.Ordinal) &&
                    method.GetParameters().Length == 0)
                {
                    return true;
                }
            }

            return false;
        }

        static bool ContainsPersistentCall(SerializedProperty destinationCalls, SerializedProperty sourceCall)
        {
            SerializedProperty sourceTarget = sourceCall.FindPropertyRelative("m_Target");
            SerializedProperty sourceMethod = sourceCall.FindPropertyRelative("m_MethodName");
            if (sourceTarget == null || sourceMethod == null)
                return false;

            for (int i = 0; i < destinationCalls.arraySize; i++)
            {
                SerializedProperty call = destinationCalls.GetArrayElementAtIndex(i);
                SerializedProperty target = call.FindPropertyRelative("m_Target");
                SerializedProperty method = call.FindPropertyRelative("m_MethodName");
                if (target != null && method != null &&
                    target.objectReferenceValue == sourceTarget.objectReferenceValue &&
                    string.Equals(method.stringValue, sourceMethod.stringValue, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        static void AppendPersistentCall(
            SerializedProperty destinationCalls,
            SerializedProperty sourceCall,
            bool forceVoidMode)
        {
            int index = destinationCalls.arraySize;
            destinationCalls.InsertArrayElementAtIndex(index);
            SerializedProperty destinationCall = destinationCalls.GetArrayElementAtIndex(index);
            CopyPersistentCall(sourceCall, destinationCall, forceVoidMode);
        }

        static void CopyPersistentCall(
            SerializedProperty sourceCall,
            SerializedProperty destinationCall,
            bool forceVoidMode)
        {
            CopyObjectReference(sourceCall, destinationCall, "m_Target");
            CopyString(sourceCall, destinationCall, "m_TargetAssemblyTypeName");
            CopyString(sourceCall, destinationCall, "m_MethodName");
            if (forceVoidMode)
                SetEnum(destinationCall, "m_Mode", PersistentListenerModeVoid);
            else
                CopyEnum(sourceCall, destinationCall, "m_Mode");
            CopyObjectReference(sourceCall, destinationCall, "m_Arguments.m_ObjectArgument");
            CopyString(sourceCall, destinationCall, "m_Arguments.m_ObjectArgumentAssemblyTypeName");
            CopyInt(sourceCall, destinationCall, "m_Arguments.m_IntArgument");
            CopyFloat(sourceCall, destinationCall, "m_Arguments.m_FloatArgument");
            CopyString(sourceCall, destinationCall, "m_Arguments.m_StringArgument");
            CopyBool(sourceCall, destinationCall, "m_Arguments.m_BoolArgument");
            CopyEnum(sourceCall, destinationCall, "m_CallState");
        }

        static void SetEnum(SerializedProperty root, string path, int value)
        {
            SerializedProperty property = root.FindPropertyRelative(path);
            if (property == null)
                return;

            if (property.propertyType == SerializedPropertyType.Enum)
                property.enumValueIndex = value;
            else
                property.intValue = value;
        }

        static void CopyObjectReference(SerializedProperty sourceRoot, SerializedProperty destinationRoot, string path)
        {
            SerializedProperty source = sourceRoot.FindPropertyRelative(path);
            SerializedProperty destination = destinationRoot.FindPropertyRelative(path);
            if (source != null && destination != null)
                destination.objectReferenceValue = source.objectReferenceValue;
        }

        static void CopyString(SerializedProperty sourceRoot, SerializedProperty destinationRoot, string path)
        {
            SerializedProperty source = sourceRoot.FindPropertyRelative(path);
            SerializedProperty destination = destinationRoot.FindPropertyRelative(path);
            if (source != null && destination != null)
                destination.stringValue = source.stringValue;
        }

        static void CopyInt(SerializedProperty sourceRoot, SerializedProperty destinationRoot, string path)
        {
            SerializedProperty source = sourceRoot.FindPropertyRelative(path);
            SerializedProperty destination = destinationRoot.FindPropertyRelative(path);
            if (source != null && destination != null)
                destination.intValue = source.intValue;
        }

        static void CopyFloat(SerializedProperty sourceRoot, SerializedProperty destinationRoot, string path)
        {
            SerializedProperty source = sourceRoot.FindPropertyRelative(path);
            SerializedProperty destination = destinationRoot.FindPropertyRelative(path);
            if (source != null && destination != null)
                destination.floatValue = source.floatValue;
        }

        static void CopyBool(SerializedProperty sourceRoot, SerializedProperty destinationRoot, string path)
        {
            SerializedProperty source = sourceRoot.FindPropertyRelative(path);
            SerializedProperty destination = destinationRoot.FindPropertyRelative(path);
            if (source != null && destination != null)
                destination.boolValue = source.boolValue;
        }

        static void CopyEnum(SerializedProperty sourceRoot, SerializedProperty destinationRoot, string path)
        {
            SerializedProperty source = sourceRoot.FindPropertyRelative(path);
            SerializedProperty destination = destinationRoot.FindPropertyRelative(path);
            if (source == null || destination == null)
                return;

            if (source.propertyType == SerializedPropertyType.Enum && destination.propertyType == SerializedPropertyType.Enum)
                destination.enumValueIndex = source.enumValueIndex;
            else
                destination.intValue = source.propertyType == SerializedPropertyType.Enum ? source.enumValueIndex : source.intValue;
        }

        readonly struct TextAnimatorEventRule
        {
            public readonly string SourceProperty;
            public readonly string DestinationProperty;
            public readonly bool IsTyped;
            public readonly string RelayEventName;

            public TextAnimatorEventRule(
                string sourceProperty,
                string destinationProperty,
                bool isTyped,
                string relayEventName)
            {
                SourceProperty = sourceProperty;
                DestinationProperty = destinationProperty;
                IsTyped = isTyped;
                RelayEventName = relayEventName;
            }
        }
    }
}
