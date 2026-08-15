#!/usr/bin/env python3
"""Compatibility entry point for the consolidated custom-infantry generator.

Iran world sprites and icons are now generated together with the other custom
factions so silhouette uniqueness can be checked across the complete roster.
"""

from generate_faction_infantry_art import main


if __name__ == "__main__":
	raise SystemExit(main())
