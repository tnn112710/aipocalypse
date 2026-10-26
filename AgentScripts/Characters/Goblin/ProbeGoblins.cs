// Play-mode probe for the test goblins: samples position, Animator state and contract parameters over time.
// Enter Play first (runInBackground on), then: script run --file AgentScripts/Characters/Goblin/ProbeGoblins.cs
// --entry ProbeGoblins.Sample --args '[20, 1]'.
using System.Text;
using System.Threading.Tasks;
using Aipocalypse.Core.EnemySystem;
using UnityEngine;

public static class ProbeGoblins
{
    public static async Task<string> Sample(float seconds, float every)
    {
        var sb = new StringBuilder();
        var brains = Object.FindObjectsByType<EnemyBrain>(FindObjectsInactive.Exclude);
        sb.AppendLine($"enemies={brains.Length}");
        float end = Time.realtimeSinceStartup + seconds;
        while (Time.realtimeSinceStartup < end)
        {
            foreach (EnemyBrain brain in brains)
            {
                EnemyAnimator anim = brain.Animation;
                if (anim == null)
                    continue;
                Animator a = anim.Animator;
                var state = a.GetCurrentAnimatorStateInfo(0);
                string stateName = state.IsName("Locomotion") ? "Locomotion" : a.GetCurrentAnimatorClipInfoCount(0) > 0 ? a.GetCurrentAnimatorClipInfo(0)[0].clip.name : "?";
                sb.AppendLine($"t={Time.time:F1} {brain.name} pos={brain.transform.position.ToString("F1")} v={brain.GetComponent<EnemyMover>().Velocity.magnitude:F2} Speed={a.GetFloat("Speed"):F2} LookX={a.GetFloat("LookX"):F2} LookY={a.GetFloat("LookY"):F2} Lean={a.GetFloat("Lean"):F2} state={stateName} visible={a.GetComponentInChildren<SkinnedMeshRenderer>().isVisible}");
            }
            await Task.Delay((int)(every * 1000));
        }
        return sb.ToString();
    }

    // A temporary camera framing one goblin, for capture game_view --camera GoblinCam (play mode only; gone on stop).
    public static string Frame(string goblin, float distance, float height)
    {
        var target = GameObject.Find(goblin);
        if (target == null)
            return "no " + goblin;
        var cam = GameObject.Find("GoblinCam");
        if (cam == null)
        {
            cam = new GameObject("GoblinCam");
            cam.AddComponent<Camera>().enabled = false;
        }
        Vector3 focus = target.transform.position + Vector3.up * 0.9f;
        cam.transform.position = focus + target.transform.forward * distance + Vector3.up * height;
        cam.transform.LookAt(focus);
        return "framed " + goblin;
    }
}
