param(
    [ValidateSet('all', 'originals', 'sheets')]
    [string]$Stage = 'all',

    [string[]]$EnemyKeys = @(),

    [switch]$Force
)

$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$outputDir = Join-Path $workspace 'assets\enemy\concept_generated\core_set_v1'
$cli = 'C:\Users\Administrator\.agents\skills\gpt-image-2-skill\scripts\gpt_image_2_skill.cjs'
$env:CODEX_HOME = 'C:\Users\Administrator\.codex'

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

$style = @'
Keep the attached reference as the source of truth for silhouette, proportions, anatomy, face placement and core palette. Refine it rather than redesigning it. Match the Arcane Stationery Workshop art direction: Japanese stylized fantasy stationery, cute but subtly eerie, warm handmade feeling, strict high-angle three-quarter top-down view suited to a 2D roguelite, orthographic feel, readable compact silhouette. Render as polished high-resolution pixel art with deliberate crisp pixel clusters, limited 16-bit-inspired colors, clean dark navy-brown outlines and controlled internal texture. No painterly anti-aliased edges, no vector art, no 3D render, no photorealism. Use soft low-saturation cream, parchment, ink blue, moss green and dusty danger red as appropriate, with moderate gameplay-readable contrast. Warm off-white paper background extending to every edge, no floor plane, no cast shadow, no vignette, no decorative border. No readable text, letters, labels, numbers, logo or watermark.
'@

$enemies = @(
    @{
        Key = '01'
        Name = 'ink_blob'
        Reference = Join-Path $workspace 'assets\enemy\001_The_Ink_Blob_Ani.png'
        Original = @'
Create one single-monster concept art image, not a sprite sheet and not multiple poses. Depict the Ink Blob in a slow stalking pose. Preserve the sprite's top-heavy hanging ink-clot body, irregular wet crown, tiny luminous eyes and narrow lower fringe of dripping tendrils. Its material is deep blue-black fountain-pen ink mixed with swollen paper fiber: glossy wet lobes, translucent indigo edges, thin capillary filaments and a few pale illegible glyph-like fragments drowned beneath the surface. Keep a short attached smear directly beneath and behind the creature to communicate that it consumes safe floor space, but do not create a scene. The creature should feel weak in direct combat yet oppressive as a mobile source of sticky pollution. One isolated subject with generous empty margin.
'@
        Sheet = @'
Create one visual-only professional monster design sheet on a 3:2 landscape page. Keep the same exact Ink Blob design, proportions, palette and face. Arrange six clean separated pixel-art studies with no captions: a large three-quarter idle view; a strict top-down footprint silhouette; a slow creeping pose pulling a wet ink trail; two Ink Blobs leaning toward one another to show clustering while still clearly separated as a behavior vignette; a hit/death splatter state; and two enlarged material close-ups showing the wet membrane, capillary threads and drowned paper fibers. Make the trail broad and mechanically readable as a slow/damage hazard. The page must read as a game-production design sheet, not a cinematic scene or animation strip.
'@
    },
    @{
        Key = '02'
        Name = 'crispy_paged_doll'
        Reference = Join-Path $workspace 'assets\enemy\002_The_Crispy_Paged_Doll_Ani.png'
        Original = @'
Create one single-monster concept art image, not a sprite sheet and not multiple poses. Depict the Crispy Paged Doll in its watchful mid-range stance immediately before a charge. Preserve the sprite's bald rounded paper head, narrow humanoid frame, layered folded-paper torso, long thin jointed limbs and slightly hunched posture. Build the body from dry yellowed notebook and book paper: curled corners, deckled edges, faded blue rules, tiny binding fibers, brittle cracks at every joint and a restrained fall of paper dust. Show an orange-red paper core only through a few cracks in the chest. The pose should hint that the torso can fold inward into a sharp charging wedge. It must feel fragile, fast and tragically cute rather than undead flesh or a human skeleton. One isolated subject with generous empty margin.
'@
        Sheet = @'
Create one visual-only professional monster design sheet on a 3:2 landscape page. Keep the same exact Crispy Paged Doll design, proportions, palette, paper construction and face. Arrange six clean separated pixel-art studies with no captions: a large three-quarter neutral view; a side/back construction view revealing folded layers and fiber joints; a compressed wind-up pose folding inward; a sharp straight-line dash pose with a short restrained paper-dust motion trail; a wall-impact dizzy pose with the orange-red paper core exposed; and a death burst study showing three to five short-range paper scraps plus close-ups of the cracked joint and core. Make the wind-up, dash and stun silhouettes instantly distinguishable for gameplay telegraphing. The page must read as a game-production design sheet, not a cinematic scene or ordinary animation strip.
'@
    },
    @{
        Key = '03'
        Name = 'black_spot_fungus'
        Reference = Join-Path $workspace 'assets\enemy\003_the_mold_spot_fungus.png'
        Original = @'
Create one single-monster concept art image, not a sprite sheet and not multiple poses. The attached 144x36 sprite sheet contains four animation frames of the same 36x36 Black Spot Fungus Colony and is the absolute authority for its design. Reconstruct the creature shown in one representative frame at high detail without changing its silhouette: one compact, chunky, roughly circular domed fungal mound; clearly raised rather than flat; only slightly wider than tall; an irregular charcoal-black and dark moss-green lumpy body; a small face tucked into the front; several short pale yellow-green root-like fungal feet or tendrils hanging directly from the lower rim; and a tight crown of several rounded knobbly spore growths integrated into the top surface. Preserve the clustered top-heavy arrangement and the sprite's gray-blue/lavender secondary highlights, but apply the user's authoritative gameplay color correction: active spore sacs and released spores are muted crimson red, never purple projectiles. The red sacs must remain small integrated growths, not tall mushrooms. Show the colony in a subtle breathing/awakening pose with at most a few red spores hovering very close to it. It is a single compact creature, not an environment. Absolutely no broad floor carpet, flat circular stain, giant fungal mat, radial web, wreath, paper-scrap ring, separate daughter colony or scenery. One isolated subject with generous empty margin.
'@
        Sheet = @'
Create one visual-only professional monster design sheet on a 3:2 landscape page. The attached corrected concept and the original sprite design are absolute authorities. Every study must remain the same compact, chunky, roughly circular raised fungal mound: dark charcoal and moss-green lumpy body, tight crown of several rounded top growths, small front face, and short pale yellow-green root-feet hanging from the lower rim. Never flatten or spread it into a carpet, floor stain, wide disc, radial web or ring of paper scraps. Keep its footprint compact in every view. Active spore sacs and released spores are muted crimson red; gray-blue/lavender may remain only as secondary body highlights, never as projectile colors. Arrange six clean separated pixel-art studies with no captions: a large three-quarter view matching the sprite silhouette; a strict front view emphasizing the small face and dangling root-feet; a back/side construction view showing the tight crown attached to the mound; a four-beat breathing animation study with only subtle squash and crown bobbing; a compact awakening pose where the red sacs swell slightly; and an eight-direction pulse of small red toxic spores around the unchanged compact body, plus two small material close-ups of black mold spots and short branching hyphae. The page must read as a game-production character sheet, not a botanical poster or environmental pollution map.
'@
    }
)

if ($EnemyKeys.Count -gt 0) {
    $normalizedKeys = $EnemyKeys | ForEach-Object { ([string]$_).PadLeft(2, '0') }
    $enemies = $enemies | Where-Object { $_.Key -in $normalizedKeys }
}

function Invoke-ImageEdit {
    param(
        [string]$Reference,
        [string]$Prompt,
        [string]$Out,
        [string]$Size,
        [switch]$Overwrite
    )

    if ((Test-Path -LiteralPath $Out) -and -not $Overwrite) {
        Write-Host "SKIP: $Out"
        return
    }

    & node $cli --json --json-events --provider codex images edit `
        --ref-image $Reference `
        --prompt $Prompt `
        --out $Out `
        --format png `
        --size $Size `
        --quality high

    if ($LASTEXITCODE -ne 0) {
        throw "Image generation failed: $Out"
    }
}

if ($Stage -in @('all', 'originals')) {
    foreach ($enemy in $enemies) {
        $out = Join-Path $outputDir "$($enemy.Key)_$($enemy.Name)_original.png"
        $prompt = "$($enemy.Original) $style"
        Write-Host "GENERATE ORIGINAL: $($enemy.Key) $($enemy.Name)"
        Invoke-ImageEdit -Reference $enemy.Reference -Prompt $prompt -Out $out -Size '1536x1536' -Overwrite:$Force
    }
}

if ($Stage -in @('all', 'sheets')) {
    foreach ($enemy in $enemies) {
        $reference = Join-Path $outputDir "$($enemy.Key)_$($enemy.Name)_original.png"
        if (-not (Test-Path -LiteralPath $reference)) {
            throw "Missing original-art reference: $reference"
        }

        $out = Join-Path $outputDir "$($enemy.Key)_$($enemy.Name)_design_sheet.png"
        $prompt = "$($enemy.Sheet) $style"
        Write-Host "GENERATE DESIGN SHEET: $($enemy.Key) $($enemy.Name)"
        Invoke-ImageEdit -Reference $reference -Prompt $prompt -Out $out -Size '1536x1024' -Overwrite:$Force
    }
}
