# Directional roaming and social curiosity

Roaming retains a per-pet heading. On climbable backgrounds, destination selection varies it by up to 35 degrees; blocked routes consider wider turns. Edge-only routes choose the direction most aligned with the previous heading. Existing utility scores still decide when to rest or explore.

Each pet has a PetCuriosity instance owned by its behavior controller. Desktop PetSocialPerception supplies observations using runtime pet keys, not appearance IDs. Sight is distance-based (seven normal body lengths), with stronger interest in moving pets. It does not read screen pixels or perform game-room occlusion. Vocal cues extend to twelve body lengths and are emitted only after a successful regular or affection call. New crawling foot contacts produce perception cues within five body lengths; this does not add audible footstep samples. Idle regripping, grabbing and landing do not emit these crawling cues. Impact, propulsion and bite sounds do not generate cues. Muted/rejected calls do not increment the vocal revision.

A notice first produces a glance. During the held glance the head and upper body turn toward the cue, ahead of passive cursor tracking, while explicit clicks and tasks retain priority. Body length is captured from the unbent body geometry, excludes the tail, and scales with desktop size. After a one-second held glance, social utility may win and approach a point 1.5 body lengths from the observed location, using the larger pet¡¯s normal body extent. It stops approaching within a 10% arrival margin. The observed location is a snapshot, not an endlessly updated pursuit target. Sight/footstep interest lasts at most 4 seconds. Vocal interest can remain for 10 seconds before investigation, with its own 12-second cooldown. A new vocal cue can override a weaker sight notice without allowing repeated calls to continuously redirect the listener. Disabled roaming permits looking but not approaching. Sympathy weights social utility: this is a desktop adaptation, not an exact port of Rain World AI.

The coordinator does not set rival targets, bite decisions or food ownership. Tasks, feeding and fighting retain priority. Grabbed listeners can notice cues and turn their heads but cannot walk toward them. Other pets can notice grabbed sources but receive glance-only snapshots, not moving pursuit targets. Charging/airborne listeners retain unexpired memory without accepting new movement instructions. Pausing consumes no new notices; removal clears stale source attention and tracking. Socializing does not automatically vocalize, preventing call-response loops. Existing natural idle calls remain independent.

Verification includes heading continuity, corner escape, initial glance, personal spacing, expiry/cooldown, invalid cues, disabled roaming, click/task priority, duplicate appearance IDs, rivalry separation, removal/self exclusion, distant vocal cues, stationary sound snapshots and paused perception. Prior movement, feeding, fighting and effects checks remain in the full suite. Visual confirmation with multiple desktop pets is separate from these tests.

Optional social-only labels: place `social-diagnostics.enabled` beside the executable and restart the desktop pet. The label reports the source name and cue only while it is the actual graphics gaze target. Remove the marker and restart to hide it. This is enabled in SocialAttentionPreview only, without changing saved preferences.

## Sustained roaming

A new roaming bout receives a utility bonus with a randomized, personality-weighted distance budget of 6¨C12 normal body lengths before the energy multiplier. The bonus declines with travel and fatigue; it is not a promise to travel the entire budget. Small time decay and a three-second blocked-progress check prevent indefinite commitment. Ordinary sight/footstep glances no longer take over roaming. Strong vocal interest still competes, and explicit clicks, tasks, holds and invited owner following retain priority.

When resting wins, the controller retains its destination while reducing running speed and turning drive. Only after slowing does it capture a resting pose. Completing the stop reduces restlessness to provide a distinct rest period. Social head alignment is unchanged by this roaming fix and still needs its separate visual refinement.

The tested free-background bouts travelled about 4.5¨C5.4 body lengths for seeds 1, 42 and 73 before settling, with propulsion disabled. These are measured examples, not fixed durations or distance guarantees. Regression checks cover social interruption, slowdown, calm rest, blocked progress and active user/task priority.

## Contact and visible gaze correction

Physical contact was already independent of the fighting toggle, but ran before motion. The desktop frame now advances all pets, resolves contacts, synchronizes body/head interpolation, then draws every pet. This prevents that frame¡¯s movement from undoing the visible separation. Grabbed pets and den-route exceptions remain in PetContacts; paused frames do not resolve contact. The collision stage does not create rivals or bites.

Social gaze now uses a neck anchor along the limited head aim instead of always anchoring forward along the torso. A resting pet can deliberately rotate its pose around the hips toward a rear target; normal roaming decisions remain unchanged. When attention ends, the current pose is retained instead of rotating back to the old pose. Rain World¡¯s HeadRotation uses the interpolated front-chunk-to-head vector (plus jaw adjustment); verification therefore measures head position relative to the front chunk rather than treating lookPos as proof of facing.

Social labels say Noticed until the actual head vector is within 25 degrees of the selected social target, then Looking at. Tests cover targets ahead, sideways and behind on both sides, expiry, grabbed-pet collision exclusion, mixed sizes, render synchronization and identical peaceful/fighting-enabled contact separation. The full suite retained the same measured roaming bouts (4.5¨C5.4 body lengths for the existing seeds). Visual confirmation is still required.

## Sound-location investigation

When a remembered vocal or affection call wins the utility comparison after the initial glance, an autonomous grounded listener investigates a fixed point near the sound origin. It uses the existing crawl/propulsion and inspection system with a sound-specific cause flag. The inspection samples points around the original sound location at the personal-space radius. It does not refresh from the caller¡¯s live position. Explicit cursor clicks replace this cause; sound investigation itself suppresses automatic answering calls. Existing independent idle vocalizations remain available.

Grabbed simulation ages social memory and updates the head without running locomotion decisions. Releasing preserves still-valid awareness through flight, but investigation is only activated on grounded updates; existing landing recovery prevents immediate propulsion. Grabbing and den-route collision/fighting exceptions are unchanged. No settings schema or saved pet data changes were required.

## Moving-pet visual priority

Per-source screen displacement is measured in normal body lengths per second. Speed is capped and smoothed, with faster response to movement and a gradual fade after stopping. Actual moving held pets get a stronger visual bonus; being held without moving adds no bonus. Spawn, pause and size changes reset the speed baseline, and removed pets release tracking records.

After a 0.75-second attention hold, a clearly stronger moving visual cue may replace weaker non-vocal interest. Vocal memories retain priority and existing cooldowns remain. Visual gaze tracks the selected source through a separate rate-limited GazePoint only inside sight range. Point remains the original movement/investigation destination, and tracking neither extends the interest lifetime nor enables approaches to held sources. This preserves roaming, anti-chasing and sound-location behavior.

Regression checks cover 30/60/120 Hz perception, duplicate appearance IDs, stationary held pets, actual moving held pets, switching hold, vocal protection, gaze displacement bounds, invalid time, expiration, pause, removal, fade-out and sight-range exit.
