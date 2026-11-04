Behavior Designer Pro Skills
===========================

This folder contains optional, plain-text guidance for AI coding assistants.
Each subfolder covers one Behavior Designer Pro capability and contains a
SKILL.md file. Some include a references folder with additional examples.

These files do not activate generative AI in your project or game. They do not
run code, contact an AI service, send project data, or install an assistant.
Behavior Designer Pro can be used without using these files.

Using the skills
----------------
1. Choose the capability that matches your task from the list below. Start with
   setup-and-overview if you are unsure which skill to use.
2. If your chosen assistant supports skills, follow its instructions for
   discovering or installing a skill folder. Otherwise, provide the relevant
   SKILL.md as context with your request. Keeping this folder in Unity does not
   guarantee that an assistant will discover it automatically.
3. Keep each skill's references folder and relative links intact. If you copy
   skills elsewhere, keep the sibling skill folders together so links between
   them continue to work. Refresh those copies when updating this package.
4. Review proposed changes and use the skill's Verification section to check
   the result. Generated advice or code still needs testing in your project.

Available skills
----------------
All subfolder names begin with behavior-designer-pro-.

setup-and-overview           Check installation and run a first tree.
build-behavior-trees         Create and edit tasks, connections, and flow.
configure-reactive-behaviors Configure conditional aborts and interruption.
connect-gameplay-code       Connect Shared Variables, events, and tree control.
reuse-subtrees              Extract, share, and configure reusable behaviors.
create-gameobject-tasks     Write custom managed actions and conditionals.
create-entity-tasks         Write custom ECS task data and systems.
debug-behavior-trees        Diagnose runtime behavior and performance problems.
use-atlas                  Operate the separately installed Opsive Atlas tool.

Atlas and other assistants
--------------------------
The Atlas skill is documentation; it does not install, enable, or connect Atlas.
If you choose to use Atlas or another assistant, that tool's connection,
permissions, and privacy settings determine what it can access or send.

Documentation: https://opsive.com/support/documentation/behavior-designer-pro/
Support: https://opsive.com/support/
