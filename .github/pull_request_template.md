## What changed
<!-- One or two sentences, in plain words. -->

## How to check it
<!-- What should the reviewer open or click, and what should they see? -->

## Explain it in your own words
<!-- Short is fine, but write it yourself, not copied from a chat. The reviewer reads this before merging and may ask a question about it. -->
1. **Trace one request** through this change: which URL, which controller, which service, which query. If it touches data, paste the SQL that EF logged.
2. **What did you try that did not work**, or what surprised you?
3. **What would break** if the most important line of this change were removed?

## Checklist
- [ ] I ran or opened it myself, and it works
- [ ] No passwords, keys or personal data are in this change
- [ ] If this changed a decision, I added one line to `docs/decisions.md`
