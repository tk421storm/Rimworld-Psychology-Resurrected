# Psychology Resurrected

A continuation of The Word-Mule's **Psychology** mod for RimWorld 1.6 and 1.7. Psychology overhauls the game's social and psychological systems: every pawn gets a persistent personality, opinions form through real conversations, and romance, mental breaks and mental illness all follow from who a pawn is.

This branch ports the mod to 1.6/1.7, fixes a long list of bugs it carried, trims systems that no longer fit the game, and builds out dating and romance.

## Origins

- **2017-2018 · The Word-Mule** wrote Psychology for Alpha 17 through B19 and 1.0 ([1.0 Workshop page](https://steamcommunity.com/sharedfiles/filedetails/?id=1552507180), [B19](https://steamcommunity.com/sharedfiles/filedetails/?id=1499800980), [Ludeon forum thread](https://ludeon.com/forums/index.php?topic=34112.0)), then stopped playing RimWorld.
- **2020 onward · the unofficial update.** With The Word-Mule's blessing, the community kept the mod alive as [Psychology (unofficial)](https://steamcommunity.com/sharedfiles/filedetails/?id=2016263135) on the Workshop, maintained by che_lovek and WILDCARD. Nationality did the initial 1.1 patching and KEMPER fixed things through 1.1-1.3.
- **2022-2023 · the 1.3/1.4 rework.** eebasso ([GitHub](https://github.com/eebasso/Rimworld-Psychology-unofficial-v1.1-1.4)), with WILDCARD and KEMPER, rewrote the Psyche tab (word cloud, Big Five radar), linked personality to Ideology, added Indoctrinate, removed the HugsLib dependency, and added per-species settings.
- **The unofficial Workshop line** went on to support 1.5 and 1.6.
- **2026 · Psychology Resurrected.** This repository starts from the live Workshop release ([c09220d](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/c09220d)). It was ported to 1.6 and 1.7, commented and cleaned up ([590ad2f](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/590ad2f), [b4d1036](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/b4d1036)), and has since been fixed and extended as listed below.

## Thanks

Psychology exists because of these people:

- **The Word-Mule**, for the original mod and for letting the community carry it on.
- **che_lovek** and **WILDCARD**, for keeping the unofficial update running on the Workshop for years.
- **eebasso**, for the 1.3/1.4 rework and the public source this repository grew from.
- **KEMPER**, for fixes from 1.1 to 1.3 and the 1.3/1.4 update.
- **Nationality**, for the initial 1.1 patching.
- **Linq**, credited as author on the unofficial update's package.
- **Translators:** Паулк (Russian), Chaos (German), Mr.Gha (Chinese) and Éloïse Rodrake (French).
- **Idea credits from the original:** ({x}) Disarray (passionate work mood bonus), twoski (many of the new mental breaks) and Wellech (the name of the Socialite trait).
- **Tynan Sylvester and Ludeon**, for RimWorld and the modding support behind it.

## Original description

*From The Word-Mule's 1.0 Workshop page. The Mayorship section describes a system this version removes; see [Features Removed](#features-removed).*

> This is a total overhaul of Rimworld's psychological and social systems. Your colonists, and the stories you can tell about them, are what make Rimworld such a fun game. Unfortunately, besides what they can contribute to the colony, colonists are pretty much interchangeable. Now, everyone you encounter will have a unique and recognizable character archetype, giving them massively increased depth and charm. Their personality traits will affect absolutely every facet of their interactions with other people, which have also been expanded to allow your colonists more ways to get to know each other and express their individuality.
>
> **Psyche.** Every person on the rimworld now has their own attitude and personality as part of the new psyche system, entirely separate from traits. You can view a rundown of their personality traits in the Psyche tab. A colonist's psyche has a real and noticeable effect on their social interactions and self-expression. Colonists' relationships with each other are now far more realistic, varied, and driven by their persistent identity.
>
> **Social.** Colonists (and non-colonists) hold conversations with each other about a multitude of topics in order to form opinions on them. Two colonists might briefly remark on something as they pass each other in the hallway, or have a long discussion while working on crafting tables in the same room. Instead of being arbitrarily assigned a compatibility with each other, they will naturally gravitate towards like-minded people and away from those they have strong disagreements with. Colonists will even set aside their differences when the colony is small enough that they have to tolerate each other.
>
> Some colonists may not have an opportunity to get to know each other very well, because they simply don't spend enough time in the same vicinity and never get an opportunity to chat. Parties are a great place for your colonists to get together and make new friends (or enemies). However, they will also now invite each other to hang out when they have free time, and spend a few hours relaxing and talking. Good friends will also hang out to keep their relationship strong, and empathetic colonists might even try to hang out with people they don't like to see if they misjudged them.
>
> If there are relationships in your colony, they will make plans to go on dates when they don't have to work. Unless their schedule changes, they will meet at the scheduled date and time and spend time together to keep their relationship alive. Of course, if they're not meant for each other, a date may also reveal an incompatibility between them.
>
> **Sexuality and Romance.** Instead of being arbitrarily assigned a slot-hogging Gay trait, everyone's sexuality is now graded on the Kinsey scale, from zero to six. The overwhelming majority of colonists will be straight, but their sexual orientations can have much more variety and their romantic interest in each other will be adjusted accordingly. Everyone also has both a sexual and romantic drive, influencing their interest in that part of a relationship. Depending on their personality, colonists may be more likely to have multiple "flings" rather than a single lover, and how they treat breakups is affected accordingly.
>
> **Mayorship.** After a settlement's first year, elections will be held at some point every fall. A slate of colonists will announce their candidacy for mayor, and will present a platform to run on that is chosen based on their personality. A few days later, a polling place will be chosen and colonists will go there to cast their votes. [...] The candidate with the plurality of votes becomes the mayor until the next election. They will receive visits from constituents in their office or bedroom [...].
>
> **Mental Illness and Treatment.** The number of mental breaks has been vastly improved, and colonists will break differently depending on their traits. Inhabitants of the rimworld can also develop anxiety (usually in their teenage years), or acquire it after suffering PTSD from a stressful mental break. They will require occasional treatment (and good medicine, if it can be spared) to keep their anxiety in check, but once managed, it won't affect their day-to-day life. Some traits that can make colonists undesirable, like chemical interest and pyromania, can also now be treated to mitigate their effects at the cost of medical upkeep.
>
> Additionally, the drug methadone is now available to help treat addiction. Instead of removing legs or trapping colonists in walled-up rooms, you can put them on a methadone regimen to eliminate the severe mood penalty, though they will still suffer all the physical effects of addiction. Methadone itself is mildly addictive, so be careful!

The 1.3/1.4 update added the reworked Psyche tab (word cloud and Big Five radar), personality-ideoligion compatibility with the Moral Guide's **Indoctrinate** ability, per-species psychology and romance settings, and continuum sliders in place of many on/off settings.

## Bugs Squashed

Commit links point to this repository ([tk421storm/Rimworld-Psychology-unofficial](https://github.com/tk421storm/Rimworld-Psychology-unofficial)).

**Crashes and errors**
- A mechanoid tending a pawn threw an error because mechs have no mood. [70ae273](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/70ae273)
- Races missing from the species settings threw errors in romance and lovin' code. [47b0c2b](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/47b0c2b)
- Learning another pawn's sexuality checked the wrong pawn for a psyche. [2c0f18e](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/2c0f18e)
- The Individuality thought threw "Collection was modified", and its per-pawn records were never pruned. [fe13c87](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/fe13c87)
- The Psyche button texture failed to load at startup (a BC7 DDS with a bad size). It now ships as PNG only. [faad666](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/faad666)
- Panic attacks errored: the thought wasn't a memory, the state hit a null job, and the countdown ignored the tick interval. [d7003ed](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/d7003ed)
- Every failed psychiatric treatment threw an InvalidCastException. [d76ac8b](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/d76ac8b)
- A conversation threw a NullReferenceException when a pawn left the map mid-talk. [c9a2b03](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/c9a2b03)
- The Hunting Trip break logged "did not have work settings initialized" for visitors and traders. [ceb060e](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/ceb060e)
- Loading a save logged "Null key while loading dictionary ... knownSexualities". [266c023](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/266c023)
- Starting a date threw a NullReferenceException when a partner had no job, and sleeping partners were woken instead of missing the date. [6169a7a](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/6169a7a)
- Panic attacks from tended anxiety ended about 4 seconds after starting, and the anxiety's sleep (nightmare) attack roll never ran. Both read a break chance that anxiety never sets, which vanilla treats as "always". (built, in testing)
- An election could crash if a candidate died mid-campaign. This was fixed, then elections were removed. [f195ceb](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/f195ceb)
- Psychiatric treatment threw an error for patients with no comfort need, such as guests or pawns whose ideoligion ignores comfort, and the operation never finished. [79cbdd7](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/79cbdd7)

**Log spam and performance**
- Debug logging ran in code that runs constantly. [4ce58a8](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/4ce58a8)
- The Psyche tab was re-added, and the race def re-resolved, on every call. [caad997](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/caad997)
- The health card added therapy recipes to the race's shared recipe cache every time it was drawn. [5015aed](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/5015aed)
- The Psyche tab rebuilt its word cloud, Big Five radar and labels on every GUI pass. [9e9dfa2](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/9e9dfa2)
- Converting orientation traits added by other mods was logged as an error. It is intended, so it is now debug info. [5747715](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/5747715)

**Gameplay logic**
- All Sedentary pawns shared one "last moved" record. [3274776](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/3274776)
- Ideoligion certainty froze for most off-screen pawns and ran several times too fast for the rest. A 1.4-era tick throttle no longer matched 1.6's tick intervals. [760d80f](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/760d80f)
- Rejected marriage proposals gave the rejected pawn no mood thought at all. The personality-scaled thoughts are back. [b56cb59](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/b56cb59)
- Psychology's arrest odds (opinion and Social skill) had been disabled since 1.5. [c4c0a51](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/c4c0a51)
- Desensitization from seeing corpses could never fire after Ideology changed corpse observation. [d525e29](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/d525e29)
- The check for adding a trait was inverted, so Kinsey-off sexuality traits were never added. [0e4490a](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/0e4490a)
- Antidepressants never halved the mood penalties of Pessimist, Depressive or Tortured Artist. [30b3bef](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/30b3bef)
- The date cooldown compared two different game clocks. [29645c7](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/29645c7)
- The sort that keeps a pair's strongest conversation memories was thrown away. [064c82e](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/064c82e)
- The DrugFree treatment never ended a running drug binge. [a3d6e9b](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/a3d6e9b)
- A romance check tested the initiator twice instead of the recipient. [5bd99d3](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/5bd99d3)
- The cheating check for hookups was inverted, so pawns only cheated on partners they liked. [eb9b1b8](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/eb9b1b8)
- The lovin' age-curve rescaling did nothing, which made lovin' about 4x more frequent than intended. [bbdbcfc](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/bbdbcfc)
- The romantic-drive factor in marriage-proposal acceptance was computed after the result and never applied. [8f02981](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/8f02981)
- Dates and hang-outs ended the moment they started if either pawn had an old scar or a missing part. Only injuries during the outing end it now. (built, in testing)
- Children and babies could be invited to hang out, then stood still the whole time: they have no recreation need, so they had nothing to do. Hang-outs are now only between pawns with a recreation need. (built, in testing)
- Dates went ahead with a pawn in a non-violent mental break (terrifying hallucinations, a daze...), and a break during a date or hang-out didn't end it. Any mental break now cancels a planned date, ends an outing in progress (with a "cut short" message for dates), and stops hang-out invitations. Social fights are the exception: they're over in moments, so the pawn just rejoins, even if the fight was with their date. (built, in testing)
- The "date planned" letter could give the day before the real date: when the planned hour came earlier in the day than the earliest start, the date happened a day later than the letter said. The letter now gives the real day. (built, in testing)
- When the pawn who planned a date died, the plan stayed on them and their partner was never told. Now the partner gets "date called off" and the plan is removed. (built, in testing)
- A date that came due during a ritual pulled its pawns out of the ritual (or out of a caravan being formed). Now a date waits while either pawn is busy like that, an hour at a time, up to three times, and is then cancelled ("is busy"). Parties and hang-outs still give way to a date. The ritual dialog warns when a participant's planned date will or may fall during the ritual. (built, in testing)
- Once either pawn on a date or hang-out had full recreation, every activity they started ended at once (vanilla stops recreation jobs at full recreation), so they cycled through activities. Activities now run their normal length, as at parties. (built, in testing)
- On dates and hang-outs, games played from a spot (horseshoes, hoopstone, TV) and social relaxing could seat the two too far apart to talk, such as on opposite sides of the horseshoes pin. The second pawn now gets a spot within talking range of the first, and a game or gather spot without two such spots isn't picked. (built, in testing)
- Dates were cut short as "too tired" once either pawn's rest fell below 30%, so evening dates rarely finished. A date now ends, or doesn't start, only once a pawn is close to exhausted (rest below 8%). (built, in testing)

**Messages and translations**
- A successful Cure Anxiety showed no message. [78f8378](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/78f8378)
- A failed psychiatric treatment showed no message. [19a84ca](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/19a84ca)
- The DrugFree tooltip didn't say whose drug desire it alleviates. [0758e80](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/0758e80)
- Setting titles and tooltips were missing in every language. [c3f99fb](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/c3f99fb), [0215072](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/0215072), [fc59d29](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/fc59d29)
- An XML patch operation printed the wrong error label. [e4dfd05](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/e4dfd05)

## Features Removed

**These removals require a new save.** Old saves log one-time "could not find" errors for the removed classes and defs. Vanilla drops those entries, and the next save is clean.

- **Mayors and elections** ([b9f8d49](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/b9f8d49)). Removed: yearly elections, candidates and platforms, the voting gathering, the Mayor hediff and title, constituent visits, the mayoral office table and Office room role, the mayor thoughts and duties, and the Elections settings.
  *Why:* it is out of scope for a psychology mod and fits better as an ideoligion role. It was also buggy on many mod lists.
- **Psychology's funerals** ([e33c0ba](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/e33c0ba)). Removed: funeral planning, the funeral gathering and duty, the Attended Funeral thought, and the funeral letters.
  *Why:* Ideology has its own funeral ritual. Psychology's funerals also could not happen since 1.5, because their only trigger was disabled.
- **Prepare Carefully and Vanilla Psycasters Expanded compatibility** ([b4d1036](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/b4d1036)).
  *Why:* not carried into the 1.6/1.7 port. Prepare Carefully support had been broken since 1.3. Both have since been rebuilt; see Compatibility Updates.
- **Disabled and dead code** ([b4d1036](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/b4d1036)). Removed: patches commented out during the 1.5 update, the old update-intro dialogs, and the performance-profiling scaffolding.
  *Why:* none of it ran.
- **Stripping other mods' patches at startup** ([d053153](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/d053153)). See Compatibility Updates.
- **Therapy operations on races without a psyche** ([5015aed](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/5015aed)). Animals and other psyche-less races no longer list psychiatric treatments.

## Features Expanded

**Romance and dating**
- **Courtship dates** ([bd9b7dd](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/bd9b7dd)). Compatible singles can now ask each other out, not just existing couples. When a date ends, both pawns rate it (compatibility, mood, opinion, luck) and gain a good- or bad-date opinion memory. A date that went well can lead to a romance attempt, which gets more likely with each good date. Dates say "date" or "date night" depending on the couple, and pawns talk at party pace while on one. New sliders cover the courtship chance, date opinions and the romance-after-date odds.
- **Vanilla romance attempts go through Psychology** ([fbe77c8](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/fbe77c8)). A vanilla "romance attempt" becomes the fitting Psychology action:
  - for couples, planning a date night;
  - for singles, asking the pawn out;
  - for pawns looking for something casual, proposing a hookup;
  - for a willing cheater, an affair.

  The social log says which one it was.
- **Romance while socializing** ([fbe77c8](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/fbe77c8)). Pawns relaxing socially, at parties, dancing or playing billiards may look around for someone and try their luck. Configurable, default 10% per interaction.
- **One cheating model** ([eb9b1b8](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/eb9b1b8)). A "Cheating chance" setting, the pawn's opinion of their partner (shaped by how Romantic they are, with its own curve for Lechers) and personality (Experimental, Moralistic, Pure) now decide cheating, for both romance attempts and hookups.
- **Lovin' frequency** ([bbdbcfc](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/bbdbcfc), [aaeceb8](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/aaeceb8)). The time between lovin's follows the age curve (race-aware), scaled by sex drive and a new "Lovin frequency" slider.
- **Romantic drive affects proposals** ([8f02981](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/8f02981)). Pawns with high romantic drive are more likely to accept a marriage proposal.
- **Courtship asks lead with attraction; date messages** (built, in testing). Attraction now matters more than opinion when a pawn decides to ask someone out, and the "Minimum romance opinion" setting (default -5) covers both courtship and romance attempts. Top-left messages report a date starting, how it went, and why it was cut short, cancelled or missed.
- **Shared date activities** (built, in testing). Pawns on a date or hanging out now do things together, one activity after another: games and TV side by side, social relaxing at the same spot, a walk, skygazing, reading near each other, or music and dancing. At the nearest instrument, the better musician plays while the other dances; if someone else is already playing, both dance. Dancing works without Ideology. When either gets hungry, they share a meal: both get food and sit within talking range at a table, and one who finds nothing to eat keeps the other company. Before, a hungry pawn's partner stood around until the hungry one was starving. Activities are XML defs (`DateActivityDef`), so new ones can be added with a small worker class.
- **Date history** (built, in testing). The game keeps a lasting record of every courtship date: who with, and how it went for each pawn. Date memories expire, but the record doesn't. A pawn's history is cleared when they marry.
- **Watch-work dates** (built, in testing). On a date, a pawn with a burning passion may show it off: they work on it in the home area while the other follows and watches, learns a little of the skill, and comes away thinking the worker is good, really good or great at it, worth more opinion the higher their skill.
- **Psychic bonds and dates** (built, in testing). A pawn always says yes to a date with the pawn they're psychically bonded to.
- **One chance to ask someone out** (built, in testing). Vanilla's romance attempt is now the only random way a single pawn asks another out. When it becomes a date request, it carries the weight both used to have, so courtship is as common as before, and every ask goes through the same checks.
- **Date nights are rated** (built, in testing). Couples rate a date night the way singles rate a courtship date (compatibility, mood, opinion, luck), so a date night can be a good or bad date memory of any strength instead of a flat bonus. The message says how it went.
- **Lovin' on date nights** (built, in testing). Once per date, a romance attempt between partners on a date night becomes an invitation to bed. If the other agrees (by sex drive, mood and opinion, and only where their ideoligion allows it), they go to their shared bed for vanilla lovin', with its thoughts and pregnancy chance, then carry on with the date. This replaces the old nudge that sent one partner to lie down now and then.

**Social**
- **Hugs** (paused: built, but turned off for the release candidate until it works reliably in game). Pawns with a strong bond (lovers and spouses, close family, very good friends, when each thinks well of the other) now sometimes hug. When both can stop what they're doing, they walk over, embrace for a moment, and go back to work. A hug lifts both pawns' mood and opinion of each other a little; warm personalities hug more often, and a "Hug chance" setting scales it. The idea comes from the Personality Plus mod.

**Family**
- **Family drive and pregnancy approach** (built, in testing). Each pawn has a family drive: how much they want children. It comes from their own roll, shaped by personality (Romantic, Compassionate, Pure and Nostalgic raise it; Independent, Adventurous and Ambitious lower it). With Biotech, each pawn has their own pregnancy approach, the same with any partner: avoid pregnancy, leave it to chance, or try for a baby. Each child they're still raising makes them less keen on another (half as much for a matriarch or patriarch). Each pair gets the more careful of the two, so a pawn who avoids pregnancy avoids it with everyone, and a pair only tries for a baby if both want to. This covers lovers, hookups and other mods' pairings, from the first time anything checks them. If you change a pair's approach in the social tab, they keep your choice. The family drive shows on the psyche card and can be edited like the other drives. There's a setting to turn this off.
- **Matriarch/Patriarch and Child-free traits** (built, in testing). A matriarch or patriarch wants a big family and is happier with every child, up to five. A child-free pawn always avoids pregnancy (the social tab's pregnancy approach is greyed out for them), enjoys life without children, and is miserable while raising one (until the child is an adult). A pregnancy counts as a child for both. Losing a child (death, loss, stillbirth or miscarriage) hurts a matriarch half again as much, and a child-free pawn half as much. Whether a mother is happy about a new pregnancy follows her family drive instead of a coin flip: child-free pawns never are, matriarchs always are, and a terminated pregnancy is a relief to those who didn't want it.
- **Personality shapes growth moments** (built, in testing). The traits and passions offered to a growing child now lean toward their personality. An empathetic child is offered Kind more often and Psychopath less often, and a compassionate child sees Animals and Medicine more often. It's a nudge, not a guarantee, and vanilla still decides what's allowed.

**Prisoners**
- **Rapport depends on compatibility** ([65e0a47](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/65e0a47), [7bd0c51](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/7bd0c51)). Each warden chat's rapport scales with how well the warden and prisoner's personalities fit. Badly matched pairs can build strained rapport. Conversion talks build the same rapport. There is a setting to go back to vanilla.
- **Recruitment and arrest settings** ([aaeceb8](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/aaeceb8), [c4c0a51](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/c4c0a51)). A slider sets the "faction imprisoned me" penalty. Another sets the peaceful arrest base chance, which now shows on the arrester's info card.

**Testing tools**
- **Dev romance menu and debug logging** ([926139a](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/926139a)). In dev mode, a "DEV: Romance" gizmo can force a romance by intent and start or finish a planned date. A "Print debug info" setting writes `[Psychology]` lines to the log.
- **Date debugging** (built, in testing). The DEV: Romance menu can print the game's date history, or every date the pawns on the map remember. With "Print debug info" on, each pick of a date activity logs the options, what couldn't be done and what started. A date activity that throws an error is logged once and skipped.
- **Test builds** ([a22b603](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/a22b603)). `Source/build-test.ps1` builds for an in-game test and saves a decompiled diff of the DLL.

## Compatibility Updates

- **RimWorld 1.6 and 1.7.**
  - The project builds against NuGet game references, so the game doesn't need to be installed to build.
  - Shared XML lives in `Common/`; each game version gets its own `Assemblies` folder.
  - Optional compatibility content loads through `LoadFolders.xml`, only when the other mod is active.
- **Harmony coexistence** ([d053153](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/d053153)). At startup, Psychology used to remove *every* mod's Harmony patches from seven vanilla romance and social methods. The effect was silent and depended on load order. It no longer does this, so other mods' patches on those methods work alongside Psychology's.
- **Intimacy** ([139828e](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/139828e), [9480203](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/9480203), [1275f0c](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/1275f0c)). Psychology's talks never raised Intimacy's need. Now:
  - Conversations, hang-outs, date planning and asking out count as intimate interactions.
  - Psychology's interaction workers now call the base `Interacted`, so Intimacy's hook fires.
  - Longer conversations give more intimacy, up to about +4% for an epic talk, within Intimacy's social cap.
  - During a date, the date duty blocks Intimacy's own lovin' job but allows its social activity.
- **Humanoid Alien Races** ([8850d60](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/8850d60), [15268d4](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/15268d4), [b6be9db](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/b6be9db)). The HAR compatibility code was never called, so race age curves and lovin' intervals were ignored. It now applies at startup without touching Psychology's static state too early. The HAR + Ideology personality-certainty patches dropped in 1.5 are restored. Orientation traits that HAR races force on pawns are converted to Kinsey ratings without logging errors ([5747715](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/5747715)).
- **Ideology.** Personality-driven certainty drift works again on 1.6's tick intervals ([760d80f](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/760d80f)). Corpse desensitization uses Ideology's observation events ([d525e29](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/d525e29)).
- **Prepare Carefully** ([5e01179](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/5e01179)). A Psyche button in the Backstory panel header opens the editable psyche dialog, replacing a patch that no longer matched Prepare Carefully's 1.6 interface. 1.6 only, since Prepare Carefully has no 1.7 build.
- **Vanilla Psycasts Expanded** ([5e01179](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/5e01179)). With Kinsey on, Word of Love checks Kinsey ratings instead of the orientation traits Psychology converts, so it agrees with who pawns are attracted to.
- **Vanilla Traits Expanded** (built, in testing). Lush and Stoner pawns can be treated to become drug-free, like chemical fascination. Treatment stops binges but not the permanent addiction, and the pawn feels their sobriety is unnatural while it lasts.
- **Character Editor and Pawnmorpher.** Their compatibility assemblies are rebuilt for 1.6/1.7 ([c3ee3bb](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/c3ee3bb)). Pawnmorpher transformations no longer re-resolve the race def each time ([caad997](https://github.com/tk421storm/Rimworld-Psychology-unofficial/commit/caad997)).

## Layout
- `Common/` - Defs, Patches, Languages and Textures shared by every supported game version. `Common/Mods/<Mod>/` holds XML that only loads when that mod is active.
- `1.6/`, `1.7/` - compiled assemblies for each game version (plus optional compatibility assemblies under `Mods/<Mod>/Assemblies`).
- `LoadFolders.xml` - which folders RimWorld loads for each version and which compatibility folders depend on other mods.
- `Source/` - C# source: the main `Psychology` project and compatibility projects for Humanoid Alien Races, Character Editor and Pawnmorpher.

## Building
Game and Harmony references come from NuGet (Krafs.Rimworld.Ref, Lib.Harmony.Ref), so the game does not need to be installed to build.

```
pwsh Source/build.ps1                  # every supported version
pwsh Source/build-test.ps1             # 1.6 build for an in-game test, saves a DLL diff under Source/TestBuilds
dotnet build Source/Psychology.sln -c Release -p:RimWorldVersion=1.6
dotnet build Source/Psychology.sln -c Release -p:RimWorldVersion=1.7   # needs the .NET 10 SDK
```

Output goes straight into `<version>/Assemblies`. Compatibility projects are skipped with a warning when the other mod is not installed in the Steam Workshop folder (override with `-p:WorkshopDir=...`).
