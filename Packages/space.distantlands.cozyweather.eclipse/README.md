# COZY 4: Eclipse

Eclipse adds a solar-eclipse atmosphere module to COZY 4. It can be triggered manually or automatically when the sun and moon align.

## Setup

1. Add the Eclipse module to a COZY system with an Atmosphere module.
2. Create an Eclipse Profile from **Assets > Create > Distant Lands > Cozy > Eclipse Profile** and assign it to the module.
3. Use **Add Override** to choose generated atmosphere overrides, then configure the values they should blend toward as eclipse strength rises from 0 to 1.
4. Choose **Automatic** to calculate eclipse strength from the sun and moon directions, or choose **Manual** and set the eclipse ratio yourself.

The automatic mode only triggers while the sun is above the horizon. Its maximum alignment angle controls how close the moon must be to the sun before the effect reaches full strength.

Eclipse overrides are generated from COZY 4's atmosphere variable definitions, following the Blocks module pattern. Each enabled property blends toward its configured value according to eclipse strength; properties not selected in the profile remain unchanged.

When atmosphere variables change, regenerate package code with **Tools > COZY: Weather 4 > Generate Variants**. This runs the Eclipse override generator alongside COZY's other code generators.

The Eclipse module also masks the sun disk with a circular moon silhouette in the COZY Sky shader. Set **Eclipse Moon Size** below `1` for an annular “ring of fire”; `0.95` is the default. This affects the rendered sun disk, not the separate sun halo or scene lighting.
