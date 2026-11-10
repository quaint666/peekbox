# How to contribute

You do not need to install anything to contribute. Everything below works in the browser on github.com.

## The rule

Nobody edits `main` directly. Every change goes through a **pull request** (PR), and the project owner reviews it before it merges. This keeps `main` working at all times.

## Make a change, step by step

1. Open the file on GitHub and click the pencil icon (**Edit this file**).
2. Make your change.
3. Click **Commit changes**. Choose **Create a new branch for this commit and start a pull request**. GitHub fills in a branch name; keep it.
4. Click **Propose changes**, then **Create pull request**.
5. Fill in the three short sections of the template: what changed, how to check it, and the checklist.
6. Wait for the owner's review. If they ask for changes, edit the same branch (repeat steps 1 to 3, choosing the existing branch) and the PR updates by itself.
7. When the owner approves, the PR is merged as one commit and its branch is deleted.

## Good habits

- One PR, one change. Small is better: a PR the owner can review in five minutes gets merged faster.
- Describe the change in plain words. "Add the budget bar to the builder page" is better than "update".
- Never put passwords, keys, or other people's personal data in a file. If you did by mistake, tell the owner right away; do not just delete it, because it stays in the history.
- If a check fails or something looks wrong, ask. Asking early is fine.

## Decisions

When the team decides something (a name, a rule, a page we are not building), add one line to `docs/decisions.md` in the same PR.

## Where things are

- `docs/project-pipeline.md`: how we build, step by step.
- `docs/decisions.md`: what we decided and why.
