# Windows development launch

Use `launch-game.cmd` from the canonical OpenRA checkout. It starts the shared
companion, discovers both games, and restores the last game selection.

The sibling OpenRA-AI repository supplies the companion and maintained RA2
overlay. `OpenRA-AI/scripts/setup.ps1 -SkipWeb` installs source dependencies,
the checksum-pinned local AI pack, builds the engine/launcher, and prepares RA2.
The source launcher can bootstrap missing companion dependencies and cached RA2
data. First setup requires internet access; repeat launches reuse installed data.

RA2 commercial content is never bundled. The importer checks registered Steam
libraries (including secondary drives) and safely imports owned archives into
the active support directory. Missing content produces an actionable in-game
import prompt; World War III remains usable. Advanced non-Steam installations
can set `OPENRA_AI_RA2_CONTENT_DIR` to their owned content directory.

The local model gateway, speech servers and companion use separate available
loopback ports. They do not replace an existing router on port 4000. The game
waits for companion control readiness; model loading is asynchronous and is
reported under Settings > AI. Native AUTO does not wait on model inference.
External/custom provider choices are preserved.

`-NoCompanion` intentionally bypasses bootstrap. `-NoSpeech` disables speech for
that launch. `-ValidateOnly` checks launcher inputs without starting processes.
Logs are in the active support directory's `Logs` folder.

The Windows product wrapper delegates here rather than maintaining a second
engine/companion launch implementation. Release packaging ships both game
definitions and a self-contained .NET engine; source development requires .NET 10.
