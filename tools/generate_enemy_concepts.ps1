param(
    [string[]]$EnemyKeys = @()
)

$ErrorActionPreference = 'Stop'
$workspace = Split-Path -Parent $PSScriptRoot
$outputDir = Join-Path $workspace 'assets\enemy\concept_generated'
$reference = Join-Path $workspace 'assets\enemy\001_The_Ink_Blob_Ani.png'
$cli = 'C:\Users\Administrator\.agents\skills\gpt-image-2-skill\scripts\gpt_image_2_skill.cjs'
$env:CODEX_HOME = 'C:\Users\Administrator\.codex'

New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

$common = @'
Single isolated enemy, single pose, no sprite sheet, no animation frames, no text, no letters, no label, no frame, no extra props, no scenery. Strict high-angle three-quarter top-down view for a 2D roguelite game, orthographic feel, readable compact silhouette. Pixel art with deliberate crisp pixel clusters, a limited 16-bit palette, a clean dark outline, no anti-aliased painterly edges, no 3D render. Japanese stylized fantasy stationery game art in a magical stationery workshop visual style, cute with subtle eerie mystery, warm handmade atmosphere. Soft low-saturation pastel colors with moderate contrast. Emphasize distinctive stationery materials such as paper, ink, graphite, chalk dust, eraser crumbs, metal ruler surfaces and adhesive gel where relevant. Clean, practical, game-ready concept asset. Pure flat white background extending to every image edge, no floor plane, no cast shadow, no vignette, no gradient, no glow touching the edges, generous empty white margin. Match the attached reference only for pixel density, crisp outline and game-ready readability; do not copy its creature design.
'@

$assets = @(
    @{ Key = '02'; File = '02_soggy_paper_slime.png'; Description = 'Soggy Paper Slime: an extremely low, wide, collapsed semi-liquid ooze made primarily from mashed swollen gray-white cellulose fibers, cloudy water, ragged soggy strands and viscous paper slurry. Nearly no intact pages: only two or three tiny dissolving scraps and faint blurred blue ruled-line traces beneath the wet surface. Broad puddled lobes, slumped dripping edges, stretched pulp strands, bubbles and glossy wet pixel highlights make it look rotten, saturated and unable to hold a mound. Cute droopy eyes are sunk directly into the slurry. It must read as sodden decomposed paper pulp, never a stack of notebooks, dry paper ball, rock or firm clay.' },
    @{ Key = '04'; File = '04_black_spot_fungus.png'; Description = 'Black Spot Fungus Colony: an extremely low, broad, irregular fungal stain lying almost flush against the ground, never a standing monster or slime mound. The center is a dense dark moss-green and desaturated olive fungal mat, while the surface is covered with many black spots of varied sizes: pepper-like specks, round colonies and larger irregular soot-black blotches. Fine pale gray-green hyphae and hair-thin branching mycelial threads spread continuously around the broken perimeter, making the silhouette resemble polluted floor. Two tiny dim eyes are nearly hidden among the spots. No mushroom caps, upright stalks, blob body or legs.' },
    @{ Key = '06'; File = '06_dried_ink_ghost.png'; Description = 'Dried Ink Ghost: a slim hollow floating ghost shaped like a transparent fountain-pen barrel, with only a few cracked black-blue dried ink clots rattling inside; a scratchy nib-like pointed tail, tiny cap-rim shoulders, faint translucent body and a weary mischievous face. Its silhouette must immediately read as an empty dried-out pen tube.' },
    @{ Key = '07'; File = '07_eraser_crumb_spirit.png'; Description = 'Eraser Crumb Spirit: a compact round creature assembled from many gray-white and pale pink eraser crumbs; uneven granular surface, loose crumbs orbiting close to its body, two stubby arms, a cute wary face, and a few curled rubbing residues. Make it look capable of briefly scattering and recombining.' },
    @{ Key = '08'; File = '08_brittle_shell_clip_bug.png'; Description = 'Brittle Shell Clip Bug: a stationery insect protected by a transparent semicircular aged-plastic carapace filled with bright white stress cracks; reflective frontal shell, small soft pastel abdomen visible behind it, binder-clip and plastic-fastener leg shapes. Defensive wedge silhouette with its hard shell clearly facing forward.' },
    @{ Key = '09'; File = '09_rust_clip_bug.png'; Description = 'Rust Clip Bug: a small fast insect built from bent paper clips and tiny staple segments; orange-red rust patches, two sharp staple-like mouthparts, springy wire legs and a curled metal body. Low aggressive silhouette that clearly suggests rapid tracking and repeated close-range stabbing.' },
    @{ Key = '10'; File = '10_corrosion_knight.png'; Description = 'Corrosion Knight, elite enemy: a heavy armored stationery knight assembled from an old stapler casing, scratched metal rulers and black binder clips; left arm holds a broad rusted clipboard shield, right arm wields an oversized staple spear; orange corrosion blooms, exposed dark rust core hinted on the back, stout imposing but charming proportions.' },
    @{ Key = '11'; File = '11_peeling_label_ghost.png'; Description = 'Peeling Label Ghost: a floating semi-transparent adhesive label spirit shaped like a thin sheet, with all corners curling away, cloudy yellowed glue patches, a few illegible faded marks, and a small sly face. The body ripples like an old sticker caught by a breeze and its edges visibly lose adhesion.' },
    @{ Key = '12'; File = '12_tape_serpent.png'; Description = 'Tape Serpent: a long coiled snake made from overlapping translucent amber-clear tape, with trapped paper scraps, tiny hairs and metal fragments visible inside; its head is a small serrated tape-dispenser cutter, with a readable cute but dangerous face integrated below the blade. Sticky glossy texture and clear winding silhouette.' },
    @{ Key = '13'; File = '13_leaking_glue_slug.png'; Description = 'Leaking Glue Slug, elite enemy: a large translucent pale-yellow slug made of thick adhesive gel; bubbles, peeled label fragments and cloudy dead glue chunks floating inside; broad heavy body, two short dispenser-nozzle feelers, glossy sticky ridges. Suggest a short neat pool of glue directly attached under the body without creating a separate floor scene.' },
    @{ Key = '14'; File = '14_broken_lead_bug.png'; Description = 'Broken Lead Bug: a slender segmented crawler built from short black graphite cores connected by pale wood shavings; head is a freshly snapped sharp pencil-lead point, tiny wood-splinter legs, graphite dust highlights and a simple alert face. Basic readable melee tracker silhouette.' },
    @{ Key = '15'; File = '15_split_wood_wolf.png'; Description = 'Split Wood Wolf, elite enemy: a swift wolf-shaped creature assembled from cracked pencil shafts and splintered wooden ruler pieces; jagged lateral silhouette, graphite-colored magical light glowing through body fissures, sharpened pencil-tip claws and torn eraser-red accents. Dynamic crouched pounce pose while remaining a single compact game sprite concept.' },
    @{ Key = '16'; File = '16_wax_teardrop_monster.png'; Description = 'Wax Teardrop Monster: a squat creature made from slowly dripping colored crayon wax, mainly dusty coral with muted blue and yellow layers; semi-hardened cracked wax shell over a soft molten center, rounded gummy silhouette, tiny cute face and several attached drips. Clearly sticky and warm without fire or scenery.' },
    @{ Key = '17'; File = '17_faded_color_bird.png'; Description = 'Faded Color Bird: a small hovering bird assembled from overlapping semi-transparent pigment swatches; cyan, rose and butter-yellow feathers fade toward chalky white at wing tips, with a very short attached trail of residual color pixels. Elegant readable flying silhouette and a mysterious ink-dot eye.' },
    @{ Key = '18'; File = '18_moldy_cloth_bat.png'; Description = 'Moldy Cloth Bat: a flying bat with wings sewn from frayed old fabric scraps; loose stitches, unraveling seams, patched beige and muted green cloth, tiny gray-green spore dust caught close around the wings, and a cute eerie button-like face. Strong spread-wing top-down silhouette.' },
    @{ Key = '19'; File = '19_ultraviolet_eye.png'; Description = 'Ultraviolet Eye: a floating magical surveillance eye with a luminous violet pupil, surrounded by a circular structure resembling a compact ultraviolet lamp tube and small metal stationery brackets; pupil narrowed as if locking onto a target, cold lavender glow kept inside the silhouette. Symmetrical turret-like readable shape.' },
    @{ Key = '20'; File = '20_dust_marquis.png'; Description = 'Dust Marquis, elite enemy: a large noble-shaped dust creature wearing the silhouette of a ragged old cloak; body made from soft gray dust, chalk powder, lint and eraser particles; crooked top hat folded from discarded paper labels, tiny elegant arms and a smug cute eerie face. Imposing upper-heavy silhouette with close-held dust motes.' }
)

if ($EnemyKeys.Count -gt 0) {
    $normalizedKeys = $EnemyKeys | ForEach-Object { ([string]$_).PadLeft(2, '0') }
    $assets = $assets | Where-Object { $_.Key -in $normalizedKeys }
}

foreach ($asset in $assets) {
    $out = Join-Path $outputDir $asset.File
    if (Test-Path -LiteralPath $out) {
        Write-Host "SKIP $($asset.Key): $out"
        continue
    }

    $prompt = "Create one isolated game enemy asset. $($asset.Description) $common"
    Write-Host "GENERATE $($asset.Key): $($asset.File)"
    & node $cli --json --provider codex images edit `
        --ref-image $reference `
        --prompt $prompt `
        --out $out `
        --format png `
        --size 1024x1024 `
        --quality high

    if ($LASTEXITCODE -ne 0) {
        throw "Generation failed for enemy $($asset.Key)."
    }
}
