// Stylized Water 3 by Staggart Creations (http://staggart.xyz)
// COPYRIGHT PROTECTED UNDER THE UNITY ASSET STORE EULA (https://unity.com/legal/as-terms)
//    • Copying or referencing source code for the production of new asset store, or public, content is strictly prohibited!
//    • Uploading this file to a public repository will subject it to an automated DMCA takedown request.

#if !URP
#error Underwater Rendering extension is imported without either the "Stylized Water 3" asset or the minimum "Universal Render Pipeline" version installed. Will not be functional until these are both installed and set up.
#endif

namespace StylizedWater3.UnderwaterRendering
{
    public class UnderwaterRendering : Extension
    {
        public UnderwaterRendering()
        {
            id = ID.UnderwaterRendering;
            this.name = "Underwater Rendering";
            this.description = "Extends the Stylized Water 3 shader asset with underwater rendering, by seamlessly blending the water with post processing effects.";
            this.assetStoreID = 322081;
            this.docUrl = "https://staggart.xyz/unity/stylized-water-3/sw3-underwater-rendering-docs/";
            
            this.version = "3.3.0";
            this.minBaseVersion = "3.3.0";

            demoScenes = new[]
            {
                new DemoScene("41ca62770db784a488d3e7da1fc199f3", "Separate areas", "Two different underwater areas in a box"),
            };
        }

        public static UnderwaterRendering extension;
        
        public override void Load()
        {
            extension = IsInstalled(ID.UnderwaterRendering) as UnderwaterRendering;
        }
    }
}