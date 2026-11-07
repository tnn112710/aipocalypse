# COZY 4: Blocks

Blocks layers time-based atmosphere states over the COZY Atmosphere module. It does not replace the atmosphere profile: each Blocks profile contains only the generated overrides it needs, so all unspecified values remain controlled by the active COZY atmosphere and biome profiles.

Create a Blocks Collection profile and add time blocks in `blockTimes`. Each block has a start time, a transition duration leading into that start time, and weighted Blocks profiles. A weighted profile is selected once per day for each time block, then reused for that day. Transitions blend complete atmosphere parameter snapshots, so overrides present in only one of the two profiles also fade correctly.

The generated override classes in `Runtime/Data/Overrides` are built from COZY's VariableStack layer data. Regenerate them with the COZY VariableStack generator after changing atmosphere variables; the runtime module itself does not declare atmosphere properties.

Each generated atmosphere variable has its own override toggle. Disabled variables are left untouched by Blocks, while enabled variables blend toward their stored constant value during the time block transition.
