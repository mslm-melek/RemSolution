# RemSolution design system

Two audiences share one codebase: agency staff who live in the back-office for
eight hours a day, and marketplace clients who see one or two screens and decide
whether to trust us with a passport scan. The system below is built so both get
what they need without forking the component library.

## What this file is

This is the source of truth for **rules and intent**. The source of truth for
**values** is the `:root` block in `src/Web/ClientApp/src/styles.scss`. Values are
deliberately not repeated here — a palette written down twice drifts, and the
copy that drifts is the one someone "corrects" the real one against.

| Question | Where the answer lives |
| --- | --- |
| What is `--warn-bg` set to? | `styles.scss`, `:root` |
| When may I use `--warn-bg`? | This file |
| What shared classes exist? | `styles.scss`, below the token block |
| Which class should this screen use? | This file |
| What does a finished screen look like? | `src/Web/ClientApp/src/app/reservation/` |

If a rule here and the code disagree, the code is a bug — fix the code, don't
edit this file to match it.

---

## The six rules

### 1. The vertical accent bar means "this needs you"

A `4px` left border marks a row or panel that is waiting on the user. Nothing
else. Amber for a deadline approaching, red for one already missed, blue for
information that must be read before acting.

Use `.needs-action` (plus `.is-overdue` / `.is-info`). Do not hand-roll it.

Two hard constraints. A single-sided border always gets `border-radius: 0` — a
rounded corner on one bordered edge is a rendering artefact, not a style. And a
row in good standing gets **no** bar, because the whole point is that scanning
the left gutter shows you the work. A bar on every row is decoration and costs
the pattern its meaning.

### 2. One primary button per view

The filled blue button is the action the screen exists for: confirm the
reservation, close the renting, record the payment. Everything else is outline.
Destructive actions are outline with red text and a red border, never filled —
a filled red button reads as the recommended path.

Two filled buttons on one screen means the screen has two purposes. Split it.

### 3. Every number is monospace

Amounts, plate numbers, dates, mileage, reference codes, counts, percentages.
Use `.num`, which sets the mono family, `font-variant-numeric: tabular-nums` and
right alignment. Tabular figures are what actually make a column of prices
scannable without reading it; the mono family alone does not.

Prose stays in the body face. `.num` is for the value, not the label.

### 4. Micro-separations, not nested cards

A card delimits a top-level block: a table, a summary panel, a form section. The
rows *inside* it are separated by `border-top`, never by being cards themselves.
A card inside a card inside a card is how a dense list becomes unreadable, and
it is the most common regression in this codebase.

### 5. Colour fills only appear on chips

State is the one thing allowed a tinted background, and it renders as `.chip`
with a semantic modifier. The text colour always comes from the same family as
the fill — amber ink on amber tint, never grey or black on a tint. Contrast
against a tint is not the same problem as contrast against a surface, and
`--ink` fails it.

Chips carry state, not category. An agency name, a branch, a document type is a
plain label or an outline pill, not a coloured chip.

### 6. Spacing comes from the scale

`--sp-1` through `--sp-6`. Vertical rhythm between blocks in `rem` steps,
component-internal gaps in the px tokens. No arbitrary `margin: 13px`. If a
spacing need doesn't fit the scale, the layout is wrong before the scale is.

---

## Tokens

Semantics only; values in `styles.scss`.

**Surfaces.** `--canvas` is the page background and nothing sits directly on it
without a card or a separator. `--surface` is a card. `--surface-2` and
`--surface-3` are quieter fills for toolbars, table headers, and inset regions.
`--line` is every hairline; `--line-strong` is a control border or an emphasised
divider.

**Text.** `--ink` for content, `--ink-2` for supporting text inside a block,
`--muted` for labels and metadata, `--muted-2` for placeholders and disabled
text. Never express de-emphasis with `opacity` — opacity multiplies against
whatever is behind it and the same "muted" text ends up four different greys
across four surfaces.

**Brand.** One ramp, blue. `--brand-500` is the filled button. `--brand-600` is
link and accent text. The 50/100/200 stops are tints for selected filter chips
and avatar backgrounds. There is no secondary brand ramp on purpose: a second
one immediately competes with rule 2.

**Semantic.** Each of `ok` / `warn` / `danger` / `info` has three tokens, and the
suffix tells you where it goes: bare (`--warn`) is a border or an icon, `-bg` is
a tint, `-ink` is text on that tint. Using `--warn` as text on `--warn-bg` is
the mistake this triple exists to prevent.

**Row tints.** `--row-attention` and `--row-overdue` are the barely-there table
row backgrounds that pair with rule 1. They are not general-purpose fills.

---

## Component vocabulary

These classes live in `styles.scss`. A screen that uses them inherits the whole
look and needs almost no local SCSS.

`.page` / `.page-head` — page shell and its header. The header carries an
uppercase eyebrow (context: agency, branch, date), the page title in the display
face, and at most one primary action on the right.

`.toolbar` — search field plus filters, inside the card and above the table,
separated by a `border-bottom`. An active filter renders as a brand-tinted chip
with a clear affordance to remove it, so the user can always see why a list
looks short.

`.data-table` — header row in uppercase micro type, `border-top` between rows,
numeric columns right-aligned via `.num`, a `4px` gutter column first for the
accent bar. Fixed layout with explicit widths on the narrow columns; content
columns take the remainder.

`.metric-card` / `.tile` — a label and one number, in grids of two to four.
Distinct from `.card`, which has a border and holds structure. The number is
`--font-mono` at 22px; a unit or currency beside it goes in a `.unit` span, which
drops back to the body face because it is supporting text, not part of the figure.

`.action-banner` — the blocking panel: an accent bar, an icon, one sentence
saying what is happening, a second saying what happens if you ignore it, and the
answers on the end side. For a screen that is waiting on a decision. A banner
that merely *reports* something is `.info-banner` and friends instead.

`.chip` — see rule 5.

`.needs-action` — see rule 1.

`.num` — see rule 3.

`.money-lines` — the definition list used for amount breakdowns. Labels left,
values right in `.num`, a `border-bottom` above the total, and the total one
step larger with its currency code in muted type beside it. The balance is the
only number a person decides on, so it is the only one allowed extra size.

---

## Typography

Three families, each with one job. `--font-display` (Poppins 500) for page and
section titles only. `--font-body` (Inter 400/500) for everything else.
`--font-mono` (Roboto Mono 400) for values, per rule 3.

Load only those three weights, with `font-display: swap`, and preload the body
regular. Three families unrestrained is roughly 180 kB and a flash of unstyled
text on every first visit — which lands on the public marketplace, where it
costs conversion.

The scale is `--fs-page` / `--fs-section` / `--fs-body` / `--fs-sm` /
`--fs-label` / `--fs-micro`. Six sizes is the whole vocabulary. Weights are 400
and 500; there is no 600 or 700, because at these sizes the extra weight reads
as shouting rather than emphasis.

Sentence case everywhere — titles, buttons, labels, table headers, menu items.
Uppercase is reserved for the eyebrow and table headers, both at `--fs-label` or
smaller with letter-spacing.

---

## Dark mode

Dark is the intended default for the back-office. The token block under
`[data-theme='dark']` remaps surfaces, ink, and the semantic triples; component
CSS never branches on theme, because every colour it touches is already a
variable.

Three things do not come for free:

`--brand-500` deliberately does **not** lighten in dark mode. It is a filled
button with white text, and lightening it drops contrast below AA. Only
`--brand-600` — link and accent *text* — lightens. The same applies to
`--danger-solid`, the filled alert pip: the light red that reads well as dark-mode
*ink* fails as a *fill* under white text.

The brand ramp does not invert as a whole. The tint stops (50/100/200) flip to
dark tints, because their job is to be a quiet fill. The deep stops (700/800/900)
stay put, because they carry the hero gradients, which are correct dark-on-dark.
This is why **text never comes from 700 and up** — only from `--brand-600`. A
`color: var(--brand-700)` disappears in dark mode.

Angular Material is compiled from source in `src/_material-theme.scss`, which
`styles.scss` imports at the top. The light pass is emitted on `:root` and a
colour-only pass under `:root[data-theme='dark']`, so `mat-select` panels,
`mat-dialog`, `mat-datepicker` and `mat-table` chrome follow the theme — overlay
panels live in `.cdk-overlay-container` under `<body>`, which inherits from
`<html>`. The prebuilt `azure-blue.css` that used to be listed in `angular.json`
is gone and must not be added back: a prebuilt theme is one hard-coded colour
pass and cannot follow `data-theme`, which is what previously blocked the toggle.

Material's neutral/structural palette is still Material's `azure`, whose tonal
ramp is contrast-checked. Only the tokens that actually carry brand colour are
re-pointed at our ramp, in the `_brand-overrides` mixin. That mixin is emitted
under **both** selectors on purpose: `:root[data-theme='dark']` outranks `:root`,
so a single `:root` block would be beaten by the dark colour pass on exactly the
tokens it means to pin.

The switch is shipped: `shared/theme.ts` resolves the choice (stored, else the OS
preference), `main.ts` applies it before bootstrap so the first paint is correct,
and `ThemeService` flips it live — no reload, because every colour is a custom
property. The choice is per-device and deliberately not stored on the account:
which theme suits you depends on the screen you are sitting at.

---

## Writing the words

Copy is design material. It is also the part most likely to be generated
carelessly.

Name things as the user thinks of them, not as the schema does. "Reservation
expires in 2 h", not "PendingConfirmation TTL". A person manages bookings and
vehicles, never aggregates or feature flags.

Buttons are verb-first and one to three words, and the verb survives the whole
flow: the button that says "Confirm reservation" produces a toast that says
"Reservation confirmed". Not "Submit", not "OK".

Errors say what happened and what to do, in one sentence, without an apology and
without a raw exception string. A concurrency conflict is "Someone else updated
this reservation. Reload to see their changes." — never "409 CONCURRENT_UPDATE".
The machine-readable code goes in `ProblemDetails` for the client to localise;
the user reads the sentence.

Empty states are an invitation, not a report. Name the space and offer the
action: "No reservations yet — create one to hold a car for a client." Skip
"Nothing here."

Both languages ship. Every string goes through a transloco key; none are
hardcoded, in either language. Agency-entered data — brands, model names, extra
service names — is never translated. Those are proper nouns and the agency's own
words.

---

## Rules for anyone — human or agent — editing the frontend

No colour, size, or spacing literal in a component. Only `var(--*)`. A component
that needs a value the tokens don't provide needs a new token, decided
deliberately, not a one-off hex. This is enforceable by grep, and the component
sheets are currently clean:

```
grep -rn "#[0-9a-fA-F]\{3,8\}\b\|rgba\?([0-9]" src/Web/ClientApp/src/app \
  --include=*.css --include=*.scss | grep -v "var(--"
```

Two traps that grep does not catch. A token whose light value is dark (`--ok-ink`,
`--danger-ink`, `--muted`) inverts to a *light* value in dark mode, so using one as
a **fill** under white text produces light-on-light the moment the theme flips —
fills come from `-bg` with matching `-ink`, or from a deliberately fixed token like
`--danger-solid`. And Material's typography hierarchy emits `.mat-typography p` at
`[0,1,1]`, which beats any bare class: a shared class that may land on a `<p>`
needs the `.mat-typography` qualifier, as `.section-title`, `.tile-label` and
`.tile-value` do.

There is a compatibility block at the end of `:root` holding the previous token
names (`--gap-*`, `--r-*`, `--shadow-*`, `--font`, `--accent*`, `--success`). It
exists so all ~50 component sheets kept working through the restyle and inherit
dark mode for free. New CSS uses the canonical names; the aliases get retired a
folder at a time.

New shared look goes in `styles.scss` as a class, not into a component's local
SCSS. If two screens would need the same rule, it belongs in the global file by
definition. Read the specificity note at the top of that file before adding
anything that has to beat a component rule — component styles are
emulated-encapsulated, which buys them one class' worth of specificity, and the
global rules that override them are written that way on purpose.

Never reorder the stylesheet imports in `angular.json`. The prebuilt Material
theme loads before `styles.scss` and the override strategy depends on it.

Never rename or remove a transloco key while restyling. Templates and
`src/assets/i18n/*` move together or not at all; a renamed key fails silently in
the language nobody on the team is currently reading.

`src/Web/ClientApp/src/app/web-api-client.ts` is generated by NSwag. It is never
edited by hand. If a screen needs a field the client doesn't expose, the change
belongs in the endpoint and the DTO, then regenerate.

Ship to the accessibility floor without announcing it: visible keyboard focus on
every interactive element, an `aria-label` on every icon-only button,
`aria-hidden` on decorative icons, and a layout that survives down to mobile
width. The public marketplace is the half of the product where this is a legal
question and not only a courtesy.

---

## Reference implementation

`src/Web/ClientApp/src/app/reservation/` is the worked example: list, detail, and
the confirm/reject dialog. It uses the shared classes and nothing else, and it is
the screen to copy from rather than a description to interpret.

When restyling a feature, do one folder per pull request. A change that touches
every component folder at once cannot be reviewed, and the regressions it
introduces surface in the language and the theme nobody checked.
