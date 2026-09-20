// Stylized Water 3 by Staggart Creations (http://staggart.xyz)
// COPYRIGHT PROTECTED UNDER THE UNITY ASSET STORE EULA (https://unity.com/legal/as-terms)
//    • Copying or referencing source code for the production of new asset store, or public, content is strictly prohibited!
//    • Uploading this file to a public repository will subject it to an automated DMCA takedown request.

namespace StylizedWater3.DynamicEffects
{
    public class DynamicEffects : Extension
    {
        public DynamicEffects()
        {
            this.id = ID.DynamicEffects;
            this.name = "Dynamic Effects";
            this.description = "Enables advanced effects to be projected onto the water surface. Such as boat wakes, ripples and shoreline waves.";
            this.assetStoreID = 299321;
            this.docUrl = "https://staggart.xyz/unity/stylized-water-3/sw3-dynamic-effects-docs/";
            
            this.version = "3.3.0";
            this.minBaseVersion = "3.3.0";
        }

        public static DynamicEffects extension;

        public override void Load()
        {
            extension = IsInstalled(ID.DynamicEffects) as DynamicEffects;
        }
    }
}