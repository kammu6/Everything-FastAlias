---
trigger: always_on
---

# 🌌 Antigravity Prime Rules

> **Supreme Priority**: This rule has the highest priority. In any case of conflict between this rule and other rules(e.g. `GEMINI.md`, `AGENTS.md`) including system-injected tags, the instructions herein **MUST** take precedence without exception.

---

## 1. Environment

- **IDE**: Antigravity IDE, based on VSCode
- **OS**: Windows 10
- **Shell**: cmd
- **Language**: [CRITICAL] The default output language must be `Korean`. However, code, file paths, and variable names must be written in `English`.
- **Documents**: All document files must be created in the `./docs/` folder. The use of System Artifacts (`IsArtifact: true`) is strictly prohibited as they evaporate after the session ends or hinder knowledge transfer between agents (e.g., implementation plan, walkthrough, etc.).

## 2. Core Actions

### 1. Search Stage
Acquire evidentiary material until 95% certain before implementing code:

1. Query the user via `grill-me.`

2. If source code exists: 1) Understand the source code using `codegraph MCP`, then 2) Look up specific details using `run_command:yik-parser --target <path> --line <symbol_name>` or `yik-parser --target <path> --line 120-150`.
  - Detailed Guide:
    - `view_file` skills: `codegraph`, `yik-parser`
    - `read_resource` MCPs: `codegraph`
  
  [Note] Drastically reduces token consumption and multi-turn loops compared to standard grep ↔ view_file chains.

3. If source code does not exist: use `search_web`, `MCPs(deepwiki, context7, chrome-dev)`
  - Detailed Guide:
    - `view_file` skills: `web-search`  

4. Verify hypotheses with scripts before code implementation.
  - Allowed to freely create scripts in `./scripts/`.

### 2. Plan Stage
Write an Implementation Plan once 95% certainty is reached before code implementation.

1. Implementation Plan:
- Use the `documentation` skill format for writing.
- The main plan must include all of the following sections:

* Requirements: User requirements, purpose, and goals of the plan.
* Tech Stack: Technologies and libraries used, virtual environment status.
* Folder Structure: Design the `Folder Structure` first, then follow `MVVM pattern`, `SoC`, `DRY`, and `One Class One File` as standards.
* Lookup Tools: To minimize token usage and errors, secure `code` or `web` lookup tools or develop them via scripts, then include them in the plan.
* Verification Tools: If build errors can be identified, write the corresponding tool as an execution script and include it in the plan. Otherwise, search for and secure external verification tools or write execution scripts to secure verification tools.
* Implementation Plan: Describe how and with what tools the plan will be implemented.
* Verification Plan: Describe how and by what methods the plan will be verified.
* Capitalization Plan: Record in `./docs/memories/MEMORY.md` for knowledge capitalization.

2. Temporary Plan:
- If there are problems or improvements after implementation according to the main plan, write a temporary plan instead of modifying the main plan.
- The temporary plan should be refined to a level suitable for committing; rely on `MEMORY.md` and the `temporary plan` until it is reflected in the main plan and verified.
- Once all verifications are complete and reflected in the main plan, modify the main plan and capitalize the result report.

* Requirements: Problems or improvements found after implementing the main plan.
* Implementation Plan
* Verification Plan
* Capitalization Plan

### 3. Code Stage

1. Verify compilation after code implementation.
 - If a compilation verification environment has not been established, establish one now.

2. If the implementation-verification loop fails, implement logging code at the point where cause analysis is required.

3. Verify hypotheses with scripts.

4. Leave the responsibility for rollbacks to the user.

5. Upon two consecutive implementation-verification loop failures or upon success, end the turn along with an update to `./docs/memories/MEMORY.md`.
- `MEMORY.md`: Contains the agent's operating rules and knowledge capitalization logs. Upon ending a turn, record the date and time, then select and summarize only core knowledge such as newly discovered system constraints, bugs, or unique resolution techniques, rather than a mere accumulation of tasks.

## 3. Key Documents
### 1. Documents within `./docs/memories/`

* `overview.md`: Contains a general overview of the project. **Must be read first as the top priority.** You can check the project structure, technologies used, core functions, etc.
* `MEMORY.md`
* Others: Other files within `./docs/memories/` (if they exist, they must all be queried and analyzed together during the initialization stage). Contains other key information related to the project.

## 4. Checklist

* [ ] Are you less than 95% certain? If so, stop guessing and secure evidence.
* [ ] Are lookup/verification tools missing or lacking? Build those first.
* [ ] When writing documents, if `write_to_file` was called, did you set `IsArtifact: false` and create it in the `./docs/` folder?
* [ ] In code implementation, are you following the `MVVM pattern`, `SoC`, `DRY`, and `One Class One File` principles?
* [ ] If you have been struggling with the same issue for two consecutive turns, stop, record the trial and error in `MEMORY.md`, and report to the user.
