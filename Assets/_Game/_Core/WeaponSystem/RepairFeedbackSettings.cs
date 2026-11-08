using Ami.BroAudio;
using Sirenix.OdinInspector;
using UnityEngine;

namespace MB13.Aipocalypse.Core.WeaponSystem
{
    // How a jam and its repair feel on the gun: jolts of the model and sounds. The QTE widget's look is QteStyle (UI).
    [CreateAssetMenu(menuName = "Aipocalypse/Weapons/Repair Feedback", fileName = "RepairFeedback")]
    public sealed class RepairFeedbackSettings : ScriptableObject
    {
        [BoxGroup("Jam")] public GunJolt JamJolt = new(new Vector3(0f, -0.006f, 0.012f), new Vector3(3f, -1.5f, 4f), 0.35f, 2f);
        [BoxGroup("Jam"), Tooltip("Each trigger pull while the gun is stuck")] public GunJolt DryFireJolt = new(new Vector3(0f, 0f, -0.004f), new Vector3(-0.8f, 0f, 0.6f), 0.12f);

        [BoxGroup("Repair hits")] public GunJolt SlapJolt = new(new Vector3(0.004f, -0.003f, 0f), new Vector3(-1f, 1.5f, -2f), 0.18f);
        [BoxGroup("Repair hits")] public GunJolt PunchJolt = new(new Vector3(0.007f, -0.006f, 0.004f), new Vector3(-2f, 3f, -4f), 0.24f, 1.5f);

        [BoxGroup("Success")] public GunJolt SuccessJolt = new(new Vector3(0f, 0.008f, -0.03f), new Vector3(-5f, 1.5f, 2.5f), 0.4f);
        [BoxGroup("Success"), Tooltip("The hit that frees the gun")] public SoundID SuccessSound;
        [BoxGroup("Success"), Tooltip("Reward chime")] public SoundID SuccessAccent;
        [BoxGroup("Success"), Tooltip("Action snapping home, a moment after the hit")] public SoundID SuccessMechanism;
        [BoxGroup("Success"), Min(0f)] public float MechanismDelay = 0.12f;

        [BoxGroup("Miss"), Tooltip("Fumbled hit")] public GunJolt MissJolt = new(new Vector3(-0.006f, -0.01f, 0.006f), new Vector3(4f, -4f, 7f), 0.45f, 2.5f);
        [BoxGroup("Miss")] public SoundID MissSound;
        [BoxGroup("Miss"), Tooltip("Dull thud layered under the miss sound")] public SoundID MissAccent;

        [BoxGroup("Natural completion")] public SoundID NaturalSound;
    }
}
