//  Distant Lands 2026
//  COZY: Stylized Weather 4
//  All code included in this file is protected under the Unity Asset Store Eula

using System;

namespace DistantLands.Cozy
{
    public interface IStateMachineModule<T> : ICozyModule where T : IStateMachineProfile
    {
        public MeridiemTimeDelta TimeTillProfileChange { get; }
        public float TransitionTime { get; }
        public Action<T, T> OnProfileChange { get; }
        public T CurrentProfile { get; }
        public bool ChangeProfile();
        public T GetNextProfile(T lastProfile, ConditionSnapshot conditionSnapshot);
        public bool SetProfile(T profile, MeridiemTimeDelta duration, bool resetTimer = true, bool transition = true);
        public bool SetProfile(T profile, bool resetTimer = true, bool transition = true);
    }
}