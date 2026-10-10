using System.Collections.Generic;

namespace Rowlan.Genesis
{
    public interface ITemplateCollection
    {
        public string GetPresetHelpText();

        public ITemplateSettings[] GetTemplates();
    }
}