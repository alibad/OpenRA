# Dynamic environment system

The RA mod cycles through all five events without repeats until the shuffled event bag is exhausted. A warning card appears ten seconds before activation. The first warning starts after thirty seconds; events last 45–60 seconds and are separated by 40–60 second clear-weather windows.

| Event | Main gameplay pressure | Modern-faction adaptation | Classic-faction adaptation |
| --- | --- | --- | --- |
| Shamal Front | Ground/air handling, vision, range | Saudi 65% | Russia 35% |
| Oil-Fire Smoke | Severe ground vision and vehicle handling | Yemen 60%, Iran 45% | France 35% |
| Coastal Squall | Aircraft/ship speed, reload, accuracy | Turkey 60% | England 45% |
| Heat Mirage | Rangefinding, accuracy, false radar contacts | Yemen 60%, Saudi 45% | Ukraine 35% |
| Night Blackout | Vision, stealth detection, targeting | Iran 65%, Turkey 60% | Germany 45% |

Adaptation closes the listed percentage of the gap between each event penalty and normal performance. It never gives a value better than clear-weather performance. Effects activate only when Saudi Arabia, Yemen, Turkey, or Iran participates, so the stock RA experience is unchanged unless a modern faction is in the match.

Run `python packaging/environment/generate_environment_assets.py` to deterministically rebuild the original audio loops, and `python packaging/environment/test_environment_effects.py` for fast structural checks. OpenRA's own YAML and content validators remain the authoritative integration tests.
