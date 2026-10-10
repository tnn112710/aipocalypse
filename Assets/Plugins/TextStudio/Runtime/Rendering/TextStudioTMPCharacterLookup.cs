using TMPro;
using UnityEngine;

namespace TextStudio
{
    internal static class TextStudioTMPCharacterLookup
    {
        internal static int FindCharacterInfoIndex(
            TextStudioPlanView plan,
            TMP_TextInfo textInfo,
            int unitIndex)
        {
            if (!plan.IsCreated ||
                textInfo == null ||
                textInfo.characterInfo == null ||
                unitIndex < 0)
            {
                return -1;
            }

            int characterCount = Mathf.Min(textInfo.characterCount, textInfo.characterInfo.Length);
            for (int characterInfoIndex = 0; characterInfoIndex < characterCount; characterInfoIndex++)
            {
                TMP_CharacterInfo character = textInfo.characterInfo[characterInfoIndex];
                if (character.elementType == TMP_TextElementType.Character &&
                    plan.GetUnitIndexForDisplayChar(character.index) == unitIndex)
                {
                    return characterInfoIndex;
                }
            }

            return -1;
        }
    }
}
