---
name: copilot-cli
description: Intelligent long-running coding using GitHub Copilot CLI with GPT-4.1
---

For any coding task:

1. First explore the codebase using your tools to understand exactly which files and folders are involved.
2. Choose the optimal working directory with this logic:
 - If the task touches OpenClaw core files or integration code → cd ~/.openclaw
 - If the task needs both backend and frontend, or multiple parts of a project → cd ~/.openclaw/workspace/projects/
 - If the task is isolated to only backend OR only frontend → cd directly into that specific backend or frontend folder
3. After deciding the folder, run: cd then gh copilot --model gpt-4.1
4. Give Copilot a clear and detailed goal.
5. Stay in the CLI session and keep working until the task is completely done.
6. Before finishing, always run this quality loop:
 - Run ESLint and fix all errors and warnings
 - Run all unit tests and fix failures
 - Create E2E tests if they don't exist for this feature, then run them
 - Run Sonar analysis and fix every issue
 - Run build and make sure it succeeds
7. Only reply to me when everything is green: lint clean, all tests passing, Sonar clean, and build successful.

Always choose the smallest directory that still contains all the files the task needs. Too much context wastes tokens. Too little context causes mistakes.
---
