using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Rowlan.Genesis
{
    public interface ITemplateProcessor
    {
        public void Process(ITemplateSettings template, Object[] objects);
    }
}