// This script is not being used fully. Some code in it may serve purpose in the future, 
//just ignore it for now if you are wondering why this code here....


using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace AshTools.ControlRigPro
{
    public enum CreatureType
    {
        Humanoid,
        Quadruped,
        Bird,
        Insect,
        Spider,
        Serpent,
        Octopus,
        Custom
    }

    public enum LimbType
    {
        Arm,
        Leg,
        Wing,
        Tail,
        Tentacle,
        Spine,
        Neck,
        Finger,
        Toe,
        Antenna,
        Mandible
    }

    public class LimbChain
    {
        public string Name { get; set; }
        public LimbType Type { get; set; }
        public List<Transform> Bones { get; set; } = new List<Transform>();
        public bool IsLeft { get; set; }
        public bool IsRight { get; set; }
        public int ChainIndex { get; set; }

        public string GetSidePrefix()
        {
            if (IsLeft) return "Left";
            if (IsRight) return "Right";
            if (ChainIndex > 0) return $"Chain{ChainIndex}";
            return "";
        }

        public string GetFullName()
        {
            string prefix = GetSidePrefix();
            return prefix != "" ? $"{prefix} {Name}" : Name;
        }
    }

    public static class CreatureRigDetector
    {
        private static readonly Dictionary<string, LimbType> limbKeywords = new Dictionary<string, LimbType>
        {
            { "arm", LimbType.Arm },
            { "hand", LimbType.Arm },
            { "wrist", LimbType.Arm },
            { "elbow", LimbType.Arm },
            { "shoulder", LimbType.Arm },
            { "leg", LimbType.Leg },
            { "foot", LimbType.Leg },
            { "ankle", LimbType.Leg },
            { "knee", LimbType.Leg },
            { "thigh", LimbType.Leg },
            { "hip", LimbType.Leg },
            { "wing", LimbType.Wing },
            { "tail", LimbType.Tail },
            { "tentacle", LimbType.Tentacle },
            { "spine", LimbType.Spine },
            { "chest", LimbType.Spine },
            { "neck", LimbType.Neck },
            { "head", LimbType.Neck },
            { "finger", LimbType.Finger },
            { "thumb", LimbType.Finger },
            { "index", LimbType.Finger },
            { "middle", LimbType.Finger },
            { "ring", LimbType.Finger },
            { "pinky", LimbType.Finger },
            { "toe", LimbType.Toe },
            { "antenna", LimbType.Antenna },
            { "mandible", LimbType.Mandible },
            { "claw", LimbType.Arm },
            { "pincer", LimbType.Arm }
        };

        public static CreatureType DetectCreatureType(GameObject root)
        {
            var allBones = root.GetComponentsInChildren<Transform>().ToList();
            var limbChains = DetectAllLimbChains(root);

            int legCount = limbChains.Count(l => l.Type == LimbType.Leg);
            int armCount = limbChains.Count(l => l.Type == LimbType.Arm);
            int wingCount = limbChains.Count(l => l.Type == LimbType.Wing);
            int tentacleCount = limbChains.Count(l => l.Type == LimbType.Tentacle);

            // Detection logic
            if (tentacleCount >= 4) return CreatureType.Octopus;
            if (legCount >= 6) return CreatureType.Spider;
            if (legCount >= 6 && wingCount > 0) return CreatureType.Insect;
            if (wingCount >= 2 && legCount == 2) return CreatureType.Bird;
            if (legCount == 4 && armCount == 0) return CreatureType.Quadruped;
            if (legCount == 2 && armCount >= 2) return CreatureType.Humanoid;
            if (limbChains.Any(l => l.Type == LimbType.Tail) && legCount == 0) return CreatureType.Serpent;

            return CreatureType.Custom;
        }

        public static List<LimbChain> DetectAllLimbChains(GameObject root)
        {
            var limbChains = new List<LimbChain>();
            var processedBones = new HashSet<Transform>();
            var allBones = root.GetComponentsInChildren<Transform>().ToList();

            // Find all potential limb endpoints (bones with no children or only one child)
            var endpoints = allBones.Where(b => b.childCount == 0 ||
                                                 (b.childCount == 1 && IsLikelyEndpoint(b))).ToList();

            foreach (var endpoint in endpoints)
            {
                if (processedBones.Contains(endpoint)) continue;

                var chain = TraceLimbChain(endpoint, processedBones);
                if (chain != null && chain.Bones.Count >= 2)
                {
                    limbChains.Add(chain);
                }
            }

            // Group similar chains and assign indices
            GroupSimilarChains(limbChains);

            return limbChains;
        }

        private static bool IsLikelyEndpoint(Transform bone)
        {
            string name = bone.name.ToLower();
            return name.Contains("hand") || name.Contains("foot") ||
                   name.Contains("tip") || name.Contains("end") ||
                   name.Contains("toe") || name.Contains("finger") ||
                   name.Contains("claw") || name.Contains("head");
        }

        private static LimbChain TraceLimbChain(Transform endpoint, HashSet<Transform> processedBones)
        {
            var chain = new LimbChain();
            var bones = new List<Transform>();

            Transform current = endpoint;
            while (current != null && !processedBones.Contains(current))
            {
                bones.Add(current);
                processedBones.Add(current);

                // Stop at major junction points
                if (IsJunctionPoint(current) && bones.Count > 1)
                    break;

                current = current.parent;

                // Stop after reasonable chain length
                if (bones.Count >= 5)
                    break;
            }

            if (bones.Count < 2) return null;

            // Reverse to get root->tip order
            bones.Reverse();
            chain.Bones = bones;

            // Detect limb type
            chain.Type = DetectLimbType(bones);
            chain.Name = GetLimbName(chain.Type);

            // Detect side
            DetectSide(bones, chain);

            return chain;
        }

        private static bool IsJunctionPoint(Transform bone)
        {
            if (bone.childCount > 2) return true;

            string name = bone.name.ToLower();
            return name.Contains("hip") || name.Contains("pelvis") ||
                   name.Contains("chest") || name.Contains("spine") ||
                   name.Contains("shoulder") || name.Contains("root");
        }

        private static LimbType DetectLimbType(List<Transform> bones)
        {
            // Check all bone names for keywords
            foreach (var bone in bones)
            {
                string boneName = bone.name.ToLower();
                foreach (var kvp in limbKeywords)
                {
                    if (boneName.Contains(kvp.Key))
                        return kvp.Value;
                }
            }

            // Default based on bone count and position
            if (bones.Count == 3)
            {
                // Likely arm or leg based on position
                float avgY = bones.Average(b => b.position.y);
                float rootY = bones[0].position.y;
                return avgY > rootY ? LimbType.Arm : LimbType.Leg;
            }

            return LimbType.Tentacle; // Default for unknown chains
        }

        private static string GetLimbName(LimbType type)
        {
            switch (type)
            {
                case LimbType.Arm: return "Arm";
                case LimbType.Leg: return "Leg";
                case LimbType.Wing: return "Wing";
                case LimbType.Tail: return "Tail";
                case LimbType.Tentacle: return "Tentacle";
                case LimbType.Spine: return "Spine";
                case LimbType.Neck: return "Neck";
                case LimbType.Finger: return "Finger";
                case LimbType.Toe: return "Toe";
                case LimbType.Antenna: return "Antenna";
                case LimbType.Mandible: return "Mandible";
                default: return "Limb";
            }
        }

        private static void DetectSide(List<Transform> bones, LimbChain chain)
        {
            foreach (var bone in bones)
            {
                string name = bone.name.ToLower();
                if (name.Contains("left") || name.StartsWith("l_") || name.StartsWith("l "))
                {
                    chain.IsLeft = true;
                    return;
                }
                if (name.Contains("right") || name.StartsWith("r_") || name.StartsWith("r "))
                {
                    chain.IsRight = true;
                    return;
                }
            }

            // Use position if no naming convention found
            float avgX = bones.Average(b => b.position.x);
            if (avgX < -0.01f) chain.IsLeft = true;
            else if (avgX > 0.01f) chain.IsRight = true;
        }

        private static void GroupSimilarChains(List<LimbChain> chains)
        {
            var groups = chains.GroupBy(c => c.Type).ToList();

            foreach (var group in groups)
            {
                var groupList = group.ToList();
                if (groupList.Count > 2)
                {
                    // Multiple similar limbs (e.g., spider legs)
                    int index = 1;
                    foreach (var chain in groupList.Where(c => !c.IsLeft && !c.IsRight))
                    {
                        chain.ChainIndex = index++;
                    }
                }
            }
        }

        public static List<LimbChain> DetectChainsFromSelection(Transform bone)
        {
            // Try to detect what type of chain the selected bone belongs to
            var chain = new LimbChain();
            var bones = new List<Transform>();

            // First, try to find the chain by going up and down
            Transform current = bone;

            // Go down to find tip
            while (current.childCount == 1)
            {
                current = current.GetChild(0);
            }
            Transform tip = current;

            // Go up from tip to build chain
            current = tip;
            while (current != null && bones.Count < 5)
            {
                bones.Insert(0, current);
                if (current == bone.parent?.parent) break; // Stop at grandparent of selection
                current = current.parent;
            }

            if (bones.Count < 2)
            {
                // Try alternate detection
                bones.Clear();
                bones.Add(bone);
                if (bone.parent != null) bones.Insert(0, bone.parent);
                if (bone.parent?.parent != null) bones.Insert(0, bone.parent.parent);
            }

            if (bones.Count >= 2)
            {
                chain.Bones = bones;
                chain.Type = DetectLimbType(bones);
                chain.Name = GetLimbName(chain.Type);
                DetectSide(bones, chain);
                return new List<LimbChain> { chain };
            }

            return new List<LimbChain>();
        }
    }
}