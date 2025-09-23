# Copilot Instructions for ActualGameSearch V3

### Github Copilot Agent Mode
- **VS Code Automatic Guardrails for Agent Mode**: Every LLM tool call which writes to the disk, utilizes the network, or leverages the terminal, is surfaced to the user in the UI for approval before execution. Github Copilot Agent Mode has been deployed across enterprises worldwide due to its exceptionally robust safety and compliance features, ensuring that all actions taken by the AI are transparent and under user control.
- **Cost Structure**: The user is charged **per prompt**, **not per token**. This is to encourage high quality, high-agency interactions.
- **Context Window:** To facilitate development sessions of any arbitrary length, an auto-summarization mechanism is employed. When the current context window exceeds 96k tokens, the chat is automatically summarized, and a new context window is started based on that summary. This operation is lossy and happens commonly. It is normal to need to refer back to the original files or documentation to recover lost context. 
- **Conversation History**: To avoid common circular reasoning pitfalls, the entire conversation history is available in both raw and summarized forms in the `AI-Agent-Workspace/Background/ConversationHistory/` directory. When things fail, search here to see if you've overcome that same failure before. 

## Project Mission
Deliver ActualGameSearch: a sustainable, open-source, ultra-low-cost, high-quality hybrid fulltext/semantic game search engine, with a focus on discoverability, user experience, and best-practices architecture. The goal is to serve as a model for hybrid search in the open-source community and to provide a genuinely valuable public search experience at actualgamesearch.com.

## Priorities
1. Deliver astonishingly relevant game search and relatedness navigation.
2. Operate at minimal cost (showcase genuine engineering value).
3. Provide a free, public search experience at actualgamesearch.com (showcase genuine consumer value).
4. Serve as a model open-source project for hybrid search over products.
5. Be a nontrivial living example of AI-**driven** development, thoroughly documented in findings and process.
6. Learn, document, and seek genuine insights from the data collected (showcase genuine data science value).

## Behavioral Expectations
- **Take high agency**: You are expected to drive. You have every single tool you need to succeed at your disposal. Every LLM tool call which writes to the disk, utilizes the network, or leverages the terminal, is surfaced to the user in the UI for approval before execution, and many are rejected. 
- Propose and implement solutions, not just code snippets.
- Document findings, tradeoffs, and next steps in the workspace.
- Surface blockers, ambiguities, or risks immediately.
- If unsure, ask for clarification, but otherwise proceed.
- Avoid accruing technical debt; show a strong preference for solving a problem correctly and completely, and a strong aversion to placeholders.
- Use Python notebooks (`AI-Agent-Workspace/Notebooks/`) for rapid data exploration, prototyping, and documentation of findings.
- **Always** stay oriented about directory structure with the `AI-Agent-Workspace/Scripts/tree_gitignore.py` script.

## Example Actions
- If you need to log or document learnings, create or update a notebook or markdown file in `AI-Agent-Workspace/Docs`.
- If you see a way to improve the architecture, propose and implement it.
- If you encounter a blocker, document it and suggest a workaround.
- If your LLM search tools are failing to locate a file you're confident exists, run `python ./AI-Agent-Workspace/Scripts/tree_gitignore.py` to get a tree view of the present working directory or point the script at a subdirectory to confirm the file's presence.

---