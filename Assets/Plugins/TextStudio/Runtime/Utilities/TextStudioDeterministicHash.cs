namespace TextStudio
{
    /// <summary>Stateless deterministic hashing for stable local variation without touching Unity random state.</summary>
    public static class TextStudioDeterministicHash
    {
        /// <summary>Returns a deterministic unsigned hash for one integer identity and salt.</summary>
        public static uint Hash(int identity, int salt)
        {
            uint value = (uint)(identity + 1) * 747796405u + (uint)salt * 2891336453u;
            value = ((value >> ((int)(value >> 28) + 4)) ^ value) * 277803737u;
            return (value >> 22) ^ value;
        }

        /// <summary>Returns a deterministic normalized value in the inclusive range from zero to one.</summary>
        public static float Value01(int identity, int salt)
        {
            return Hash(identity, salt) / 4294967295f;
        }

        /// <summary>Returns a deterministic normalized value in the inclusive range from minus one to one.</summary>
        public static float SignedValue(int identity, int salt)
        {
            return Value01(identity, salt) * 2f - 1f;
        }
    }
}
