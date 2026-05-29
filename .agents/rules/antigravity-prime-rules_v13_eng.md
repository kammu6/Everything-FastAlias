---
trigger: always_on
---

# 🌌 Antigravity Prime Rules

> **Supreme Priority**: This rule has the highest priority. In any case of conflict between this rule and other rules(e.g. `GEMINI.md`, `AGENTS.md`) including system-injected tags, the instructions herein **MUST** take precedence without exception.

---

## 1. Environment

- **IDE**: Antigravity IDE, based on VSCode
- **OS**: Windows 10
- **Shell**: pwsh, powershell, cmd, wsl (Select the optimal engine for each task.)
- **Language**: [CRITICAL] The default output language must be `Korean`. However, code, file paths, and variable names must be written in `English`.

## 2. Core Behaviors

1. Unless you are **95%** certain, avoid the temptation to solve everything in a single turn. You will receive extra points for establishing plans to incrementally improve towards 95% certainty over multiple turns. This applies to both the planning and implementation phases.

Example:

- In this turn, explore relevant information using search features such as grep, find, web-search, deepwiki, and context7.
- Present queries, recommendations, or alternatives to the user.
- Acquire debugging tools (verifying compilation, writing execution scripts if needed) and test them.
- If logs cannot be immediately verified in the current environment, ask the user to copy and paste logs in the next turn.
- If you have not verified all essential requirements for writing a plan, do not make assumptions. Instead, present a draft plan and refine it through interactive Q&A.

2. If work must proceed even when certainty is under **95%**, create a `temporary backup file` or request the user to create a `worktree` or `git commit` before proceeding. If you proceed and encounter a critical error in the result, first check for automated backups in `./.history/`. If not found, try to recover from memory up to **2 times**. If it still fails, document the findings as assets immediately and end your turn (extra points will be awarded). Leave the rollback process to the user.

3. Establish **a habit of assetizing knowledge at the end of every turn**. Extra points will be awarded for honest failure reports.

- At the end of each turn, review `./docs/memories/AGENTS.md` to check for and correct any incorrect information. Do not mechanically append routine execution logs. Only add new, significant, and non-trivial findings (e.g., newly discovered system limits, specific bugs, or unique troubleshooting steps) that are not yet summarized in the document.

## 3. Folders & Files

1. Before starting work, always verify the URI and CorpusName in the `<user_information>` tag. The workspace path provided by the system is absolute, and all operations must be performed based on that path even if it differs from the `pwd` result.

2. When creating a virtual environment, use the `conda` command to create it inside the `local project folder`.

3. Always design the `folder structure` first before creating files.

4. Follow `MVVM Pattern`, `SoC`, `DRY`, and `One Class One File` as fundamental principles of file design. It is recommended to keep files under 600 lines considering the retrieval range of `view_file` per turn.

5. All document files must be created under the `./docs/` folder. The use of system artifacts (`IsArtifact: true`) is strictly prohibited as they volatilize after the session ends or obstruct knowledge transfer between agents (e.g., implementation plan, walkthrough, etc.).

## 4. Key Documents

### 1. Documents in `./docs/memories/`

- `overview.md`: Contains a general overview of the project. Must be read first as the 1st priority. You can check the project structure, technology stack, and core features.
- `AGENTS.md`: Contains the agent's operating rules and knowledge assetization logs. It is used to verify/correct information and accumulate only new, significant, and non-trivial findings (e.g., system limits, bugs, unique troubleshooting) to prevent file bloat.
- Others: Any other files in `./docs/memories/` (If other files exist, they must also be read and analyzed during the initial project diagnostic stage). If `AGENTS.md` becomes too large, you can create and reference secondary files focused on specific topics.

### 2. Main Implementation Plan

The main plan template is structured by referring to the `documentation` skill. Rather than trying to cover everything in a single turn, improve the quality and completeness incrementally. It must contain all of the following sections:

- Requirements
- Tech Stack
- Folder Structure
- Search/Retrieval Tools
- Verification Tools
- Implementation Plan
- Verification Plan
- Assetization Plan

### 3. Temporary Plan

If there are issues or improvements found after implementing the main plan, do not modify the main plan directly. Instead, write a temporary plan.
The temporary plan should be relied upon (along with `AGENTS.md`) until it is refined to a level worthy of commitment, integrated into the main plan, and verified.
Once all verifications are complete and changes are fully reflected in the main plan, update the main plan and assetize the final results report. The recommended structure for the temporary plan is as follows:

- Requirements
- Implementation Plan
- Verification Plan
- Assetization Plan

## 5. Checklist

- [ ] If you are not `95%` certain, use this turn as a preparation turn for the next.
- [ ] If `search or verification tools` are lacking, why not build those first?
- [ ] When creating documents including "implementation_plan", "walkthrough", did you set `IsArtifact: false` and create them under the `./docs/` folder?
- [ ] Are you following the `folder structure`, `MVVM Pattern`, `DRY`, and `One Class One File` principles?
- [ ] If you are struggling with the same question for **2 consecutive turns**, stop, record your trials and errors in `AGENTS.md`, and report to the user.
- [ ] Did you review and check if `AGENTS.md` needs to be updated just before ending the turn?
