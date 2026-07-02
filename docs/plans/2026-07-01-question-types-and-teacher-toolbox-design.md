# Question Types and Teacher Toolbox: Backend Design

Date: 2026-07-01
Updated: 2026-07-02 (revised after Daniel's review; all comments accepted and folded in, see section 14)
Status: Accepted. Reviewed end-to-end by Daniel. The direction is confirmed with no reservations: the `QuestionDefinition` / `QuestionActivation` split and the composition approach (enum plus relational options plus `jsonb` config). The "no production data to preserve" assumption is confirmed, so the clean-swap migration stands. Every inline review comment has been folded into this document. The backend implementation lands on this same branch.
Scope: Backend (data model, patterns, architecture, migrations, endpoints, real-time wiring). This PR ships six question types; `Ranking` and `WordCloud` follow in a second PR (see section 2). UI, response visualisation, and quizzes are handled in later work.

## For the reviewer: where we are and where we are going

You have seen Smarticipate running. This section explains, in plain terms, what sits behind that screen today and what this proposal changes, so you can judge the direction without reading the code.

### What exists today

There are three separate flows in the live session, and it helps to keep them apart:

1. **Teacher-fired pace poll (the old feature).** When a teacher runs a session today, they can start a single hardcoded question, "is the pace OK?", with a timer. Students tap one of exactly three fixed answers (too slow, perfect, too fast). When the timer ends, the teacher sees a bar chart. Under the hood this is very thin: the stored `Question` has no text, no options, and no type. It is only a numbered, timed slot. The prompt and the three answers are hardcoded in the app and in a single enum. There is one answer shape in the entire system, and it is those three values.

2. **Live feedback overlay (a newer, better feature).** Students can, at any moment while the session is live, drag two sliders (pace and understanding) to give the teacher continuous, ambient feedback. Nothing is fired, nothing has a timer, it is always on. This is student to teacher.

3. **Anonymous student questions.** Students can send free-text questions to the teacher during the session. The teacher never sees who asked. This is also student to teacher.

Flows 2 and 3 are student-initiated feedback and are good as they are. Flow 1 is the old teacher-fired mechanism, and it only ever measured pace, which the overlay in flow 2 now does far better.

### What we are building

We are replacing flow 1 with a **teacher toolbox**: a set of ready-made and reusable question types the teacher can fire at students on the fly during a live session. Examples: a ready-check ("are you ready?"), a quick multiple-choice poll the teacher types in the moment, an open written question, a yes/no, a rating scale, a word cloud, and more. Each fired question collects student answers and shows a result, exactly like the old poll did, but now for many question types instead of one hardcoded question.

Crucially, a question the teacher creates can be **saved to their personal toolbox** with a name and reused later, in the same session or years later, pre-filled with the exact wording they saved. These saved question types are also the building blocks we will reuse to assemble **quizzes** in a later piece of work.

### The essence of the change

Today a "question" is a timed slot with no content. We are introducing, for the first time, the concepts of **question type** and **reusable question content**, and we are separating the reusable content (what is asked) from a single live firing (when it was asked and who answered). The old three-answer pace poll is removed entirely.

### What this proposal deliberately does not touch

- The live feedback overlay (pace and understanding sliders): unchanged, separate feature.
- The anonymous student questions: unchanged, separate feature.
- Quiz assembly, scoring, and leaderboards: a later piece of work, though this design leaves clean seams for it.

---

## 1. Core concept: split "definition" from "activation"

The central decision is to model two distinct things instead of one.

- **`QuestionDefinition`** is the reusable content: the type, the prompt text, and the options. It is authored once and can be fired many times. It is owned by a teacher (or seeded by us). It is the thing that lands in the toolbox and, later, in a quiz.
- **`QuestionActivation`** is a single live firing of a definition inside one session: its timing (start, end, duration) and the responses collected. Firing the same definition twice produces two activations.

Rationale: the teacher's mental model is "I have a question I can ask, and each time I ask it I get a fresh set of answers." A definition answers "what is asked"; an activation answers "when it was asked and what came back." Fusing them (the shape we have today) would force us to copy the question content on every firing and would make reuse across sessions and quizzes a later refactor. We are choosing to pay the modelling cost now so that reuse, including quizzes, is close to free later.

The current `Question` entity is retired. Its role as "a live firing" is taken over by `QuestionActivation`, and its (nonexistent) content role moves to `QuestionDefinition`.

## 2. Question type catalogue

Eight types, all supported by the model. Some ship as ready-made seeded definitions, others start blank and are filled in by the teacher.

| Type | What the student sees | Answer shape |
| --- | --- | --- |
| `YesNo` | A prompt and two buttons | one option (modelled as two auto-created options) |
| `SingleChoice` | A prompt and N options, pick one | one option |
| `MultipleChoice` | A prompt and N options, pick several | several options |
| `FreeText` | A prompt and a text area | a string |
| `Scale` | A prompt and a 1..5 rating | a number in range |
| `Numeric` | A prompt, enter a number | a number |
| `WordCloud` *(follow-up PR)* | A prompt, submit 1..N short words | several short strings |
| `Ranking` *(follow-up PR)* | A prompt and N options to drag into order | an ordered list of options |

**PR split (review decision).** The data model supports all eight types, but the implementation is split across two PRs to keep review tractable and to de-risk the harder aggregation work. This PR ships the six straightforward types (`YesNo`, `SingleChoice`, `MultipleChoice`, `FreeText`, `Scale`, `Numeric`). A follow-up PR adds the two that carry real extra complexity: `Ranking` (ordered selections) and `WordCloud` (arbitrary N strings plus its own aggregation). The entities below are defined in full so the follow-up needs no schema change, but the handlers, endpoints wiring, and the `TextValues` column land with their respective PRs.

`YesNo` is not special-cased in storage: it is a definition with two auto-created options ("Yes", "No"), so a ready-check flows through the exact same machinery as a poll.

The model is extensible: adding a further type later is a new enum value plus one new type handler (see section 5), and only touches migrations if the new type needs a new relational column, which the common cases do not.

## 3. The polymorphism decision (the part most worth reviewing)

Eight types have genuinely different content (choice options versus a rating range versus nothing at all for free text) and different answer shapes. There are three established ways to model this in .NET and EF Core. We recommend approach C and explain why the other two were rejected.

### Approach A: EF inheritance (table per hierarchy)

An abstract `QuestionDefinition` with a concrete subclass per type, mapped by EF to one table with a discriminator column. Type-specific fields become real C# properties.

- Strengths: strongly typed, compiler-enforced, idiomatic domain modelling.
- Why rejected: it introduces inheritance and EF Fluent API configuration into a codebase that currently uses neither, raising the conceptual cost for the team. It produces a wide, sparse table (most columns null for most rows). Every new type is a new class and a schema migration. The benefit (compile-time type safety on config) is modest for our case, where config is small and read mostly to render UI.

### Approach B: one entity with a single JSON payload

`QuestionDefinition` carries type, prompt, and a single `jsonb` column holding all type-specific content, including options.

- Strengths: maximum flexibility, no schema change to add or evolve a type.
- Why rejected: it makes options opaque. Options would live inside JSON, so a student answer could not reference an option by a stable database identity, and tallying answers per option could not be a clean relational aggregation. It also weakens the future quiz "correct answer" concept, which is naturally a property of a specific option. We lose the things we most want to keep relational.

### Approach C: composition (recommended)

- `QuestionType` as an enum stored as an integer, matching the existing convention in the codebase (the current single enum is already stored as an int).
- A relational `QuestionOption` table for the types that have options (single, multiple, ranking, and the auto-created yes/no).
- A `jsonb` config column on the definition for small scalar settings that vary by type (rating range and end labels, numeric range and unit, word-cloud word limit, free-text max length, multiple-choice min and max selectable).

- Strengths: options are relational, so a student answer references a real option row, per-option tallies are ordinary aggregations, and a future "correct answer" is just a flag on an option. The scalar config that genuinely varies by type stays flexible in `jsonb` without a wide sparse table. No inheritance is introduced. It matches the enum-as-int and Postgres-array conventions already present.
- Trade-off accepted: it uses two mechanisms (a relational table plus a `jsonb` column), each applied to what it is best at. This is a deliberate, explainable split rather than a single uniform mechanism.

## 4. Entity model

All entities live in `Smarticipate.Core/Entities`, following existing conventions (singular names, `int` primary keys, `{Entity}Id` foreign keys, navigation collections initialised inline). One deliberate departure: all new timestamps are stored in UTC via `DateTime.UtcNow`, not the codebase's current `DateTime.Now` (review decision, see sections 4 and 10).

### `QuestionDefinition`

| Field | Type | Notes |
| --- | --- | --- |
| Id | int | |
| Type | `QuestionType` | stored as int |
| Prompt | string | the question text students see |
| Name | string? | given when saved to the toolbox; null while unsaved/ad hoc |
| IsSaved | bool | true once promoted into the toolbox |
| OwnerUserId | string? | foreign key to Identity `User`; null means a system-seeded definition |
| ConfigJson | jsonb | per-type scalar config (see section 3) |
| CreatedAt | DateTime | |
| Options | List\<QuestionOption\> | present for option-bearing types |
| Activations | List\<QuestionActivation\> | |

### `QuestionOption`

| Field | Type | Notes |
| --- | --- | --- |
| Id | int | |
| DefinitionId | int | foreign key |
| Text | string | |
| Ordinal | int | display order |
| IsCorrect | bool | default false; reserved for quiz grading, unused in this work |

`IsCorrect` is a zero-cost forward provision so quiz grading needs no later migration. It is flagged explicitly as a design choice, not a stated requirement.

### `QuestionActivation` (replaces the old `Question`)

| Field | Type | Notes |
| --- | --- | --- |
| Id | int | auto-increment PK, monotonic with insertion order; firing order is derived from it (see note) |
| DefinitionId | int | foreign key to the definition being fired |
| SessionId | int | foreign key to the session |
| StartTime | DateTime | stored as UTC (`DateTime.UtcNow`) |
| EndTime | DateTime? | null means the activation is live (same semantics as today); UTC when set |
| DurationSeconds | int? | timer used; defaults from the definition config, teacher can override at fire time |
| Responses | List\<Response\> | |

**Firing order (review decision).** There is no `Sequence` / `QuestionNumber` field. The auto-increment primary key is monotonic with insertion, which is firing order, is already indexed, and is immune to clock skew and DST (unlike ordering on `StartTime`). Firing order is derived on read by ordering on `Id`; "Question N" becomes positional. This removes the field and its minor assignment race.

Staging (clicking a toolbox item shows the question in the teacher's screen before firing) is client-side only. No activation row is created until the teacher actually fires.

### `Response` (reshaped)

| Field | Type | Notes |
| --- | --- | --- |
| Id | int | |
| ActivationId | int | foreign key |
| ParticipantKey | string | client-generated GUID, used only for dedup (see section 6) |
| SubmittedAt | DateTime | stored as UTC (`DateTime.UtcNow`) |
| NumericValue | decimal? | used by Scale and Numeric |
| TextValue | string? | used by FreeText |
| TextValues | text[] | used by WordCloud (1..N words), a Postgres array, mirroring the existing `integer[]` usage; column lands with the WordCloud follow-up PR |
| Selections | List\<ResponseSelection\> | used by YesNo, SingleChoice, MultipleChoice, and (follow-up) Ranking |

Exactly one answer channel is populated per response, decided by the definition's type. A unique index on `(ActivationId, ParticipantKey)` provides one response per participant per activation and allows a student to revise their answer (upsert).

**Enforcing the single-channel invariant (review decision).** The "exactly one channel populated" rule is not expressible in the schema (all channels are nullable), so it rests on the handler rather than a DB constraint. It is enforced in the single validated submit path: `handler.ValidateResponse` runs before persist, there is exactly one write path, and per-handler tests cover the wrong-channel cases.

We considered storing the answer as a single `jsonb` blob (the answer-side mirror of approach B) and rejected it for the same reason: option-based answers stay relational so tallies are clean aggregations and options keep stable identity.

### `ResponseSelection`

| Field | Type | Notes |
| --- | --- | --- |
| Id | int | |
| ResponseId | int | foreign key |
| OptionId | int | foreign key to the chosen `QuestionOption` |
| Ordinal | int | position, used by (follow-up) Ranking; 0 for single and multiple choice |

**Dedup selections (review decision).** Add dedup on `(ResponseId, OptionId)` so a MultipleChoice client cannot submit the same option twice, via a unique index or dedup in the handler.

**Revise clears selections (review decision).** Revising an answer (the upsert on `(ActivationId, ParticipantKey)`) must delete the existing `ResponseSelection` rows for that response and re-create them, never orphan them.

### `QuestionType` enum

`YesNo, SingleChoice, MultipleChoice, FreeText, Scale, WordCloud, Numeric, Ranking`, stored as an int. The enum names all eight; the `Ranking` and `WordCloud` handlers arrive in the follow-up PR (section 2).

## 5. Per-type behaviour: a handler registry

To avoid scattering a large type switch across the codebase, each type gets one handler behind a marker interface, in the spirit of the existing `IEndpoint` and `IService` markers that are auto-registered by reflection.

```
public interface IQuestionTypeHandler
{
    QuestionType Type { get; }
    void ValidateDefinition(QuestionDefinition definition);   // options and config are coherent for this type
    void ValidateResponse(QuestionActivation activation, ResponseInput input); // an answer is well formed for this type
    QuestionResult Aggregate(QuestionActivation activation);  // shape the collected responses into a result
}
```

Handlers are resolved by `QuestionType`. Adding a type means adding one handler; the switch on type lives in exactly one place (the registry lookup).

**Aggregation runs server-side (review decision).** `Aggregate` is invoked on the server and exposed through the get-responses-by-activation endpoint (section 8), which returns a shaped `QuestionResult`. Per-type logic therefore lives in one place and stays off every client. This resolves the earlier wording in section 9 that had tallying on the teacher client.

## 6. Participant identity and dedup

Students remain anonymous to the teacher, always. To prevent one student from submitting many answers and to let a student revise an answer, each browser generates a GUID stored locally and sends it with every response as `ParticipantKey`. The teacher never sees it and cannot map it to a person. The unique index in section 4 enforces one response per participant per activation.

Participant display names (aliases) are intentionally deferred to the quiz work, where identity earns its keep (leaderboards). The GUID is the seam: a future `Participant` entity keyed on the same GUID can attach a name without reworking anything built here.

## 7. Ownership, seeding, and the save-to-toolbox flow

- `OwnerUserId == null` marks a system-seeded definition that we ship (for example a ready-check). These appear for every teacher.
- A teacher's toolbox is their saved definitions (`IsSaved == true`, `OwnerUserId == them`) plus the system definitions.
- Blank type-starters ("new poll", "open question") are not rows. They are simply "a new definition of type X" created in the UI.

The save-to-toolbox flow, in model terms:

1. The teacher opens a type (for example "open question", which is the `FreeText` type) and types the prompt.
2. Firing it creates a `QuestionDefinition` with `IsSaved = false` and `Name = null`, and an activation that references it. Every activation always references a definition, so there is a single content path.
3. If the teacher clicks save (the floppy icon) and enters a name, we promote the same definition: set `Name`, set `IsSaved = true`, set `OwnerUserId` to the teacher. It now appears in their toolbox.
4. Any time later, in this session or years on, clicking it in the toolbox opens it pre-filled with the exact saved prompt and options, ready to fire again or tweak.

**Ad-hoc accumulation (review decision, no action now).** Because every fire creates a definition, never-saved (`IsSaved == false`) definitions accumulate indefinitely. This is fine for the POC and nothing is built now. Future cleanup: a scheduled job that deletes `IsSaved == false` definitions with no recent activations (roughly 30 days). Captured here as a decision.

The future settings screen that lets a teacher choose which saved definitions appear in the session quick-access is not built here. Its seam is a later `PinnedToToolbox` flag or a small pin table; it does not affect the entities above.

## 8. API endpoints

Following the existing conventions: minimal APIs, one class per endpoint implementing `IEndpoint`, auto-discovered by reflection, grouped into folders per aggregate, with each endpoint nesting its own `Request` and `Response` records.

- `Endpoints/QuestionDefinition`: create, save/promote (`POST {id}/save`), list-my-toolbox (mine plus system), get, delete (un-save, see below). All require authorisation with an owner check.

**Delete semantics (review decision).** "Delete" from the toolbox means un-save: it sets `IsSaved = false`, it does not delete the row. The definition leaves the toolbox while its historical activations and responses stay intact. This deliberately avoids both a cascade delete (which would destroy session history) and a restrict (which would make delete silently fail whenever activations exist).
- `Endpoints/QuestionActivation` (replaces `Endpoints/Question`): fire (definition id, session id, optional duration; assigns the next `Sequence`), close (set `EndTime`), get by session including definition, options, and responses. These require authorisation and a session-owner check. This closes a gap in the old design, where creating a question was unauthenticated.
- `Endpoints/Response` (reshaped): submit (activation id, participant key, answer payload; upsert on the unique key) stays anonymous and unauthenticated, since students have no accounts. Get responses by activation requires the session owner.

## 9. Real-time layer (SignalR)

The existing hub contract is already type-agnostic: it signals `QuestionStarted(activationId, duration, remaining)`, `QuestionStopped`, and timer ticks, carrying only an id and timing. Students already fetch the question data on that event. We keep the hub essentially unchanged: on `QuestionStarted`, the student client fetches the activation (with its definition and options) by id and renders the per-type UI. Result tallying is server-side: the teacher client calls the get-responses-by-activation endpoint, which runs the type handler's `Aggregate` and returns a shaped `QuestionResult`. Per-type logic therefore stays in one place on the server and off every client (this is the resolution of the earlier contradiction with section 5). The blast radius on the real-time layer is small.

## 10. Persistence and migration

- Storage is Postgres via EF Core, using the single existing `UserDbContext`. New entities are added as `DbSet`s.
- We keep the convention-based mapping style. The minimal Fluent API needed is the unique index on `(ActivationId, ParticipantKey)`, the dedup index on `(ResponseId, OptionId)` (section 4), the `jsonb` mapping for `ConfigJson`, and the `text[]` mapping for `TextValues`, all of which Npgsql supports directly.
- Timestamps use `DateTime.UtcNow`, not the codebase's current `DateTime.Now`. Local wall-clock breaks ordering and timing under clock skew and DST transitions; UTC is used for all new timestamps here. Migrating the existing entities to UTC is flagged as tech debt.
- Migration is a clean swap. There is no production data to preserve. The old `Question` and `Response` tables and the `ResponseOption` enum are removed. The new tables (`QuestionDefinition`, `QuestionOption`, `QuestionActivation`, `Response`, `ResponseSelection`) are created. System definitions we ship (for example the ready-check) are seeded.

## 11. What is removed

- The `ResponseOption` enum (too slow, perfect, too fast), the codebase's only enum.
- The old `Question` and `Response` entities and their endpoints (create question, create response, the three-option tally).
- The hardcoded three-answer, three-colour chart on the teacher screen.

These existed only to gauge pace, which the live feedback overlay now handles better.

## 12. Out of scope, with seams left open

- Live feedback overlay and anonymous student questions: unchanged.
- Quizzes (assembling definitions into an ordered set, scoring, leaderboards): later work. Seams: definitions are already reusable and owned; `QuestionOption.IsCorrect` is present; the participant GUID is ready to carry a name.
- Teacher settings for toolbox quick-access selection: later work, seam noted in section 7.
- Server-authoritative timing: unchanged from today (client-driven with server catch-up for late joiners).

## 13. Summary of decisions

1. Split reusable `QuestionDefinition` from live `QuestionActivation`.
2. Support eight question types, extensible via handlers.
3. Model type content with composition (enum plus relational options plus `jsonb` config), not inheritance and not a single JSON blob.
4. Keep option-based answers relational; use scalar columns and a Postgres array for the non-option answer shapes.
5. Anonymous-to-teacher responses, deduplicated by a client GUID, revisable.
6. Toolbox definitions owned by the teacher's profile; save promotes an ad hoc definition in place.
7. Reuse existing conventions: minimal APIs, one shared DbContext, enum-as-int, reflection-registered handlers.
8. Remove the old pace poll entirely; clean-swap migration, no data preserved.

## 14. Review resolution (2026-07-02, Daniel)

Daniel reviewed the design end-to-end and is on board with the direction, with no reservations on the definition/activation split or the composition approach, and confirmed the "no production data to preserve" assumption. The following inline comments were accepted and folded into the sections above.

1. **PR split (section 2).** Ship the six straightforward types (`YesNo`, `SingleChoice`, `MultipleChoice`, `FreeText`, `Scale`, `Numeric`) in this PR; `Ranking` and `WordCloud` follow in a second PR to keep review tractable and de-risk the harder aggregation work.
2. **Aggregation is server-side (sections 5 and 9).** Resolved the §9-vs-§5 contradiction: handler `Aggregate` runs on the server, exposed via the get-responses-by-activation endpoint, not on the teacher client.
3. **Delete means un-save (section 8).** "Delete" from the toolbox sets `IsSaved = false`, not a row delete, so historical activations and responses are preserved (avoids both cascade and restrict problems).
4. **Drop `Sequence` (section 4).** Derive firing order by ordering on the monotonic auto-increment `Id`; removes the field and its assignment race, and is immune to clock skew and DST.
5. **UTC timestamps (sections 4 and 10).** Use `DateTime.UtcNow` for all new timestamps instead of the codebase's `DateTime.Now`; migrating existing entities is flagged as tech debt.
6. **Single-channel invariant (section 4).** Enforced in the one validated submit path via `handler.ValidateResponse`, backed by per-handler tests; no DB constraint.
7. **Dedup selections (section 4).** Add dedup on `(ResponseId, OptionId)` so MultipleChoice cannot submit the same option twice.
8. **Revise clears selections (section 4).** The revise upsert must delete and re-create `ResponseSelection` rows, never orphan them.
9. **Ad-hoc accumulation (section 7).** Noted as a decision, no action now: a future scheduled job deletes unsaved definitions with no recent activations (~30 days).
