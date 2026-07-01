# Question Types and Teacher Toolbox: Backend Design

Date: 2026-07-01
Status: Proposed (for review)
Scope: Backend only (data model, patterns, architecture). UI, response visualisation, and quizzes are handled in later work.

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
| `WordCloud` | A prompt, submit 1..N short words | several short strings |
| `Numeric` | A prompt, enter a number | a number |
| `Ranking` | A prompt and N options to drag into order | an ordered list of options |

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

All entities live in `Smarticipate.Core/Entities`, following existing conventions (singular names, `int` primary keys, `{Entity}Id` foreign keys, navigation collections initialised inline).

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
| Id | int | |
| DefinitionId | int | foreign key to the definition being fired |
| SessionId | int | foreign key to the session |
| Sequence | int | order of firing within the session (replaces the old `QuestionNumber`) |
| StartTime | DateTime | |
| EndTime | DateTime? | null means the activation is live (same semantics as today) |
| DurationSeconds | int? | timer used; defaults from the definition config, teacher can override at fire time |
| Responses | List\<Response\> | |

Staging (clicking a toolbox item shows the question in the teacher's screen before firing) is client-side only. No activation row is created until the teacher actually fires.

### `Response` (reshaped)

| Field | Type | Notes |
| --- | --- | --- |
| Id | int | |
| ActivationId | int | foreign key |
| ParticipantKey | string | client-generated GUID, used only for dedup (see section 6) |
| SubmittedAt | DateTime | |
| NumericValue | decimal? | used by Scale and Numeric |
| TextValue | string? | used by FreeText |
| TextValues | text[] | used by WordCloud (1..N words), a Postgres array, mirroring the existing `integer[]` usage |
| Selections | List\<ResponseSelection\> | used by YesNo, SingleChoice, MultipleChoice, Ranking |

Exactly one answer channel is populated per response, decided by the definition's type. A unique index on `(ActivationId, ParticipantKey)` provides one response per participant per activation and allows a student to revise their answer (upsert).

We considered storing the answer as a single `jsonb` blob (the answer-side mirror of approach B) and rejected it for the same reason: option-based answers stay relational so tallies are clean aggregations and options keep stable identity.

### `ResponseSelection`

| Field | Type | Notes |
| --- | --- | --- |
| Id | int | |
| ResponseId | int | foreign key |
| OptionId | int | foreign key to the chosen `QuestionOption` |
| Ordinal | int | position, used by Ranking; 0 for single and multiple choice |

### `QuestionType` enum

`YesNo, SingleChoice, MultipleChoice, FreeText, Scale, WordCloud, Numeric, Ranking`, stored as an int.

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

The future settings screen that lets a teacher choose which saved definitions appear in the session quick-access is not built here. Its seam is a later `PinnedToToolbox` flag or a small pin table; it does not affect the entities above.

## 8. API endpoints

Following the existing conventions: minimal APIs, one class per endpoint implementing `IEndpoint`, auto-discovered by reflection, grouped into folders per aggregate, with each endpoint nesting its own `Request` and `Response` records.

- `Endpoints/QuestionDefinition`: create, save/promote (`POST {id}/save`), list-my-toolbox (mine plus system), get, delete. All require authorisation with an owner check.
- `Endpoints/QuestionActivation` (replaces `Endpoints/Question`): fire (definition id, session id, optional duration; assigns the next `Sequence`), close (set `EndTime`), get by session including definition, options, and responses. These require authorisation and a session-owner check. This closes a gap in the old design, where creating a question was unauthenticated.
- `Endpoints/Response` (reshaped): submit (activation id, participant key, answer payload; upsert on the unique key) stays anonymous and unauthenticated, since students have no accounts. Get responses by activation requires the session owner.

## 9. Real-time layer (SignalR)

The existing hub contract is already type-agnostic: it signals `QuestionStarted(activationId, duration, remaining)`, `QuestionStopped`, and timer ticks, carrying only an id and timing. Students already fetch the question data on that event. We keep the hub essentially unchanged: on `QuestionStarted`, the student client fetches the activation (with its definition and options) by id and renders the per-type UI. Result tallying stays on the teacher client and delegates to the type handler's aggregate shape. The blast radius on the real-time layer is therefore small.

## 10. Persistence and migration

- Storage is Postgres via EF Core, using the single existing `UserDbContext`. New entities are added as `DbSet`s.
- We keep the convention-based mapping style. The minimal Fluent API needed is the unique index on `(ActivationId, ParticipantKey)`, the `jsonb` mapping for `ConfigJson`, and the `text[]` mapping for `TextValues`, all of which Npgsql supports directly.
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
