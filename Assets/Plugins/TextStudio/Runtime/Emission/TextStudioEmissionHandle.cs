namespace TextStudio
{
    /// <summary>
    /// Generation-safe reference to one active temporary emission.
    /// </summary>
    public readonly struct TextStudioEmissionHandle
    {
        readonly TextStudioEmitter _emitter;
        readonly int _id;
        readonly uint _generation;

        internal TextStudioEmissionHandle(TextStudioEmitter emitter, int id, uint generation)
        {
            _emitter = emitter;
            _id = id;
            _generation = generation;
        }

        public static TextStudioEmissionHandle Invalid => default;
        public bool IsValid => _emitter != null && _emitter.IsHandleValid(_id, _generation);

        /// <summary>Replaces active emission text and restarts effects according to its preset.</summary>
        /// <returns><see langword="false"/> after release or pool reuse.</returns>
        public bool SetText(string text)
        {
            return _emitter != null &&
                   _emitter.TrySetText(_id, _generation, text);
        }

        /// <summary>Reformats and replaces an active numeric emission.</summary>
        /// <returns><see langword="false"/> after release or pool reuse.</returns>
        public bool SetValue(double value)
        {
            return _emitter != null &&
                   _emitter.TrySetValue(_id, _generation, value);
        }

        /// <summary>Starts the configured hide phase, or releases immediately when no transition applies.</summary>
        /// <returns><see langword="false"/> after release or pool reuse.</returns>
        public bool Dismiss()
        {
            return _emitter != null &&
                   _emitter.TryDismiss(_id, _generation);
        }
    }
}
