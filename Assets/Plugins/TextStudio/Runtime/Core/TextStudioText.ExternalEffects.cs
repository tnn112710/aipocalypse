using System.Collections.Generic;

namespace TextStudio
{
    internal interface ITextStudioExternalEffectSample
    {
        bool ApplySample(TextStudioText target);
    }

    public sealed partial class TextStudioText
    {
        List<ITextStudioExternalEffectSample> _externalEffectSamples;

        internal void RegisterExternalEffectSample(ITextStudioExternalEffectSample sample)
        {
            if (sample == null) return;
            _externalEffectSamples ??= new List<ITextStudioExternalEffectSample>();
            if (!_externalEffectSamples.Contains(sample)) _externalEffectSamples.Add(sample);
        }

        internal void ReleaseExternalEffectSample(ITextStudioExternalEffectSample sample)
        {
            _externalEffectSamples?.Remove(sample);
        }

        void ApplyExternalEffectSamples()
        {
            if (_externalEffectSamples == null) return;
            for (int i = 0; i < _externalEffectSamples.Count;)
            {
                if (_externalEffectSamples[i].ApplySample(this)) i++;
                else _externalEffectSamples.RemoveAt(i);
            }
        }
    }
}
