# Claude handoff: modern faction research contracts

This is a bounded parallel assignment. It is intentionally documentation-first
so it cannot collide with sprite generation or shared OpenRA integration.

## Copy-paste assignment

> Work in the canonical OpenRA repository only through an isolated branch and
> worktree named for your task; do not switch or clean the user's canonical
> checkout. Read the workspace `AGENTS.md` before doing anything. Preserve all
> existing work, do not reset, force-push, publish, or add hosted CI.
>
> Produce implementation-ready research contracts for these optional modern
> factions, in this order: United States (`usa`), Israel (`israel`), Germany
> (Modern, internal id `bundeswehr`), and the paired North/South Korea factions
> (`northkorea`, `southkorea`). Read `docs/modern-faction-roadmap.md`,
> `docs/experience-capability-packs.md`, and
> `docs/custom-infantry-identities.md` first.
>
> For this first assignment, edit documentation only. Do not create or edit
> SHP/PNG assets, art generators, sequence YAML, `mods/ra/experiences.yaml`,
> shared manifests, global rules, or existing faction files. Do not implement
> gameplay yet.
>
> Create one contract per production unit: `docs/faction-spec-usa.md`,
> `docs/faction-spec-israel.md`, `docs/faction-spec-bundeswehr.md`, and
> `docs/faction-spec-koreas.md`. Finish and request review of the U.S. contract
> before expanding the later documents beyond a sourced outline.
>
> Each contract must include:
>
> 1. doctrine, strengths, tradeoffs, tech shape, and intended counterplay;
> 2. a proposed roster split into infantry, vehicles, aircraft, navy,
>    buildings, and defenses;
> 3. stable actor-id proposals that cannot collide with existing actor ids;
> 4. for every actor: gameplay role, current/procurement/prototype status,
>    official source and access date, visual landmarks, scale reference,
>    player-color zones, animation needs, weapon or ability, counterplay, tech
>    tier, and approximate cost band;
> 5. an overlap matrix against stock Allies/Soviets and the China, Iran, Saudi,
>    Yemen, and Turkey packs;
> 6. a justification for every unit that is not merely another tank, rifleman,
>    missile launcher, or renamed stock weapon;
> 7. open decisions and confidence levels. Never convert uncertain reporting
>    into a factual capability claim.
>
> Prefer official defense or manufacturer sources for equipment identity and
> independent authoritative sources for contested capability claims. Record
> whether a system is fielded, ordered, demonstrated, or conceptual. For the
> U.S. tank slot, explicitly recommend either the fielded M1A2 SEP v3 or an
> honestly labeled near-future M1E3; do not present the M1E3 or XM30 as mature
> current inventory. Treat North Korean claims with extra caution and require
> corroboration. Do not use strategic nuclear weapons as ordinary skirmish
> superweapons.
>
> All visual descriptions must be usable by a pixel artist at native OpenRA
> scale: identify the two or three landmarks that remain recognizable in a
> roughly 50x39 infantry frame or a 32-facing vehicle sheet. Do not suggest
> copying Command & Conquer, another mod, photographs, logos, or unlicensed
> third-party art. The result should define original project art.
>
> End each document with a short `Ready to freeze` checklist and a list of
> decisions that require product-owner approval. Run markdown/link checks if
> available, commit only your documentation files, and report the commit hash.
> Do not merge or push to `main`.

## Why this division works

Research contracts are large, separable, text-heavy tasks that benefit from a
second model's source review. Sprite generation is tightly coupled to frame
counts, palette indices, pivots, sequence YAML, and live rendering, so it stays
with the art/integration workstream. The stable handoff boundary is the reviewed
actor-id and visual contract, not a partly generated sprite sheet.

Once a contract is approved, a second Claude assignment may implement only new
faction-local rules and weapon files in a dedicated worktree. That later task
must consume frozen actor ids and may not edit art, sequences, the Experience
catalog, shared rules, or generator manifests. Integration remains serialized.

## Review rubric for returned work

A returned contract is accepted only if:

- every proposed actor has an explicit visual and tactical distinction;
- evidence distinguishes fielded systems from prototypes and procurement;
- the roster fits OpenRA production domains and has deliberate counters;
- no actor id collides with `rg` results in the repository;
- all equipment can be represented clearly at native sprite scale;
- player-color placement and animation requirements are specified;
- claims about current conflicts do not become faction stereotypes;
- unresolved product choices are visible rather than silently assumed.
