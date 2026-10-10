using System;
using System.Diagnostics;
using UnityEngine;

namespace TextStudio
{
    internal enum TextStudioPerformanceStage
    {
        Pipeline,
        BackendPublication,
        Layout,
        Evaluation,
        Dispatch
    }

    internal readonly struct TextStudioPerformanceSnapshot
    {
        public readonly double PipelineMilliseconds;
        public readonly double BackendPublicationMilliseconds;
        public readonly double LayoutMilliseconds;
        public readonly double EvaluationMilliseconds;
        public readonly double DispatchMilliseconds;
        public readonly long PipelineAllocatedBytes;
        public readonly long BackendPublicationAllocatedBytes;
        public readonly long LayoutAllocatedBytes;
        public readonly long EvaluationAllocatedBytes;
        public readonly long DispatchAllocatedBytes;
        public readonly bool AllocationMeasurementsAvailable;

        public TextStudioPerformanceSnapshot(
            double pipelineMilliseconds,
            double backendPublicationMilliseconds,
            double layoutMilliseconds,
            double evaluationMilliseconds,
            double dispatchMilliseconds,
            long pipelineAllocatedBytes,
            long backendPublicationAllocatedBytes,
            long layoutAllocatedBytes,
            long evaluationAllocatedBytes,
            long dispatchAllocatedBytes,
            bool allocationMeasurementsAvailable)
        {
            PipelineMilliseconds = pipelineMilliseconds;
            BackendPublicationMilliseconds = backendPublicationMilliseconds;
            LayoutMilliseconds = layoutMilliseconds;
            EvaluationMilliseconds = evaluationMilliseconds;
            DispatchMilliseconds = dispatchMilliseconds;
            PipelineAllocatedBytes = pipelineAllocatedBytes;
            BackendPublicationAllocatedBytes = backendPublicationAllocatedBytes;
            LayoutAllocatedBytes = layoutAllocatedBytes;
            EvaluationAllocatedBytes = evaluationAllocatedBytes;
            DispatchAllocatedBytes = dispatchAllocatedBytes;
            AllocationMeasurementsAvailable = allocationMeasurementsAvailable;
        }
    }

    /// <summary>Receives the cost of every scheduled hub tick while a scene performance probe is attached.</summary>
    internal interface ITextStudioHubTickObserver
    {
        void OnHubTicked(TextStudioText hub, long elapsedTicks, long allocatedBytes);
    }

#if UNITY_6000_7_OR_NEWER
    [Unity.Scripting.LifecycleManagement.AutoStaticsCleanup]
#endif
    internal static partial class TextStudioPerformanceDiagnostics
    {
        static bool _enabled = false;
        static ITextStudioHubTickObserver _hubTickObserver = null;
        static bool _hubAllocationMeasurementsAvailable = false;
        static int _measurementFrame = -1;
        static long _pipelineTicks = 0L;
        static long _backendPublicationTicks = 0L;
        static long _layoutTicks = 0L;
        static long _evaluationTicks = 0L;
        static long _dispatchTicks = 0L;
        static long _pipelineAllocatedBytes = 0L;
        static long _backendPublicationAllocatedBytes = 0L;
        static long _layoutAllocatedBytes = 0L;
        static long _evaluationAllocatedBytes = 0L;
        static long _dispatchAllocatedBytes = 0L;
        static bool _allocationMeasurementsAvailable = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetForFastEnterPlayMode()
        {
            _enabled = false;
            _measurementFrame = -1;
            _pipelineTicks = 0L;
            _backendPublicationTicks = 0L;
            _layoutTicks = 0L;
            _evaluationTicks = 0L;
            _dispatchTicks = 0L;
            _pipelineAllocatedBytes = 0L;
            _backendPublicationAllocatedBytes = 0L;
            _layoutAllocatedBytes = 0L;
            _evaluationAllocatedBytes = 0L;
            _dispatchAllocatedBytes = 0L;
            _allocationMeasurementsAvailable = false;
            _hubTickObserver = null;
            _hubAllocationMeasurementsAvailable = false;
        }

        /// <summary>
        /// Per-hub attribution for scene profiling. Null keeps the scheduler on its unmeasured path; an observer makes
        /// the scheduler time each hub's whole tick, including dispatch to its renderers.
        /// </summary>
        internal static ITextStudioHubTickObserver HubTickObserver
        {
            get => _hubTickObserver;
            set
            {
                _hubTickObserver = value;
                _hubAllocationMeasurementsAvailable = value != null && DetectAllocationMeasurementSupport();
            }
        }

        internal static bool HubAllocationMeasurementsAvailable => _hubAllocationMeasurementsAvailable;

        internal static bool Enabled
        {
            get => _enabled;
            set
            {
                _enabled = value;
                if (value)
                    _allocationMeasurementsAvailable =
                        DetectAllocationMeasurementSupport();
                else
                {
                    _measurementFrame = -1;
                    _allocationMeasurementsAvailable = false;
                }
            }
        }

        internal static void PrepareFrame()
        {
            if (!_enabled)
                return;

            _measurementFrame = Time.frameCount;
            _pipelineTicks = 0L;
            _backendPublicationTicks = 0L;
            _layoutTicks = 0L;
            _evaluationTicks = 0L;
            _dispatchTicks = 0L;
            _pipelineAllocatedBytes = 0L;
            _backendPublicationAllocatedBytes = 0L;
            _layoutAllocatedBytes = 0L;
            _evaluationAllocatedBytes = 0L;
            _dispatchAllocatedBytes = 0L;
        }

        internal static MeasurementScope Measure(TextStudioPerformanceStage stage)
        {
            if (!_enabled || _measurementFrame != Time.frameCount)
                return default;

            long allocatedBytes = 0L;
            if (_allocationMeasurementsAvailable)
                allocatedBytes = GC.GetAllocatedBytesForCurrentThread();
            return new MeasurementScope(
                stage,
                Stopwatch.GetTimestamp(),
                allocatedBytes);
        }

        internal static TextStudioPerformanceSnapshot Capture()
        {
            double millisecondsPerTick = 1000d / Stopwatch.Frequency;
            return new TextStudioPerformanceSnapshot(
                _pipelineTicks * millisecondsPerTick,
                _backendPublicationTicks * millisecondsPerTick,
                _layoutTicks * millisecondsPerTick,
                _evaluationTicks * millisecondsPerTick,
                _dispatchTicks * millisecondsPerTick,
                _pipelineAllocatedBytes,
                _backendPublicationAllocatedBytes,
                _layoutAllocatedBytes,
                _evaluationAllocatedBytes,
                _dispatchAllocatedBytes,
                _allocationMeasurementsAvailable);
        }

        static bool DetectAllocationMeasurementSupport()
        {
#if !UNITY_ENABLE_CHECKS && !UNITY_EDITOR
            if (!UnityEngine.Debug.isDebugBuild)
                return false;
#endif
            long before = GC.GetAllocatedBytesForCurrentThread();
            object probe = new object();
            long after = GC.GetAllocatedBytesForCurrentThread();
            GC.KeepAlive(probe);
            return after > before;
        }

        static void Record(
            TextStudioPerformanceStage stage,
            long elapsedTicks,
            long allocatedBytes)
        {
            if (!_enabled || elapsedTicks <= 0L)
                return;

            allocatedBytes = Math.Max(0L, allocatedBytes);
            switch (stage)
            {
                case TextStudioPerformanceStage.Pipeline:
                    _pipelineTicks += elapsedTicks;
                    _pipelineAllocatedBytes += allocatedBytes;
                    break;
                case TextStudioPerformanceStage.BackendPublication:
                    _backendPublicationTicks += elapsedTicks;
                    _backendPublicationAllocatedBytes += allocatedBytes;
                    break;
                case TextStudioPerformanceStage.Layout:
                    _layoutTicks += elapsedTicks;
                    _layoutAllocatedBytes += allocatedBytes;
                    break;
                case TextStudioPerformanceStage.Evaluation:
                    _evaluationTicks += elapsedTicks;
                    _evaluationAllocatedBytes += allocatedBytes;
                    break;
                case TextStudioPerformanceStage.Dispatch:
                    _dispatchTicks += elapsedTicks;
                    _dispatchAllocatedBytes += allocatedBytes;
                    break;
            }
        }

        internal readonly struct MeasurementScope : IDisposable
        {
            readonly TextStudioPerformanceStage _stage;
            readonly long _startedTicks;
            readonly long _startedAllocatedBytes;

            internal MeasurementScope(
                TextStudioPerformanceStage stage,
                long startedTicks,
                long startedAllocatedBytes)
            {
                _stage = stage;
                _startedTicks = startedTicks;
                _startedAllocatedBytes = startedAllocatedBytes;
            }

            public void Dispose()
            {
                // Measure already matched the frame, and a synchronous scope cannot outlive it.
                if (_startedTicks == 0L)
                    return;

                long allocatedBytes = 0L;
                if (_allocationMeasurementsAvailable)
                {
                    allocatedBytes =
                        GC.GetAllocatedBytesForCurrentThread() - _startedAllocatedBytes;
                }

                Record(
                    _stage,
                    Stopwatch.GetTimestamp() - _startedTicks,
                    allocatedBytes);
            }
        }
    }
}
