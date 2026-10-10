using System;
using UnityEngine;

namespace Rowlan.Genesis.Migration
{
    public class MigrationData : ScriptableObject
    {
        public enum Module
        {
            Clone,
            Migration,
        }

        /// <summary>
        /// Show help box for information
        /// </summary>
        public bool helpBoxVisible = false;

        public Module module = Module.Clone;

    }
}