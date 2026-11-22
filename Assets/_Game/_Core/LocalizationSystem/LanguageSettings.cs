using System;
using System.Collections.Generic;
using UnityEngine;

namespace MB13.Aipocalypse.Core.LocalizationSystem
{
    // Which languages players can pick (on the root scope). The others keep their tables and CSV columns but are hidden,
    // and their missing text isn't tested: Vietnamese and French wait there until the text settles (user, 2026-11-22).
    [Serializable]
    public sealed class LanguageSettings
    {
        [Tooltip("Locale codes in menu order; the first is used when the saved or command-line language isn't offered. Empty = every locale")]
        public List<string> Offered = new() { "en" };
    }
}
