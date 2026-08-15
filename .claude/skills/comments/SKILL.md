---
name: comments
description: How to comment code in this repo — short, human, and only where the code is genuinely hard to follow. Load before writing or editing any source file.
triggers:
  - write code
  - edit code
  - add a comment
  - comment style
---

# Comments

Write the comment a colleague would write, not documentation.

## Rules

- **Only for complex things.** If the code says it plainly, say nothing.
- **One or two lines.** Never a paragraph, never a prose essay above a method.
- **Say why, not what.** `// dates are stored UTC, compare in UTC` beats a
  restatement of the line below it.
- **No section banners, no ceremony.** No `--- Invariants ---`, no
  `/// <summary>` on obvious members.
- Keep XML doc comments only where a public API is genuinely non-obvious, and
  keep them to a sentence.
- Match the file you are editing: if it is sparsely commented, stay sparse.

## Worth a comment

- A non-obvious ordering or locking rule.
- A workaround, and what breaks without it.
- A business rule you cannot infer from the code.
- A deliberate omission ("no retry here — the caller already retries").

## Not worth a comment

- Restating a name (`// the client id`).
- Narrating the obvious flow (`// save the changes`).
- Explaining a rule the code already enforces and reads clearly.
- Justifying design choices at length — that belongs in the commit message or
  the docs, not above the line.
