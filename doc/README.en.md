# NovalAi3 Auto Batch Generation Tool

Language: [简体中文](../README.md) | English | [日本語](README.ja-JP.md)

![platform](https://img.shields.io/badge/platform-Windows-0078D6?logo=windows&logoColor=white)
![framework](https://img.shields.io/badge/.NET%20Framework-4.8-512BD4?logo=dotnet&logoColor=white)
![release](https://img.shields.io/github/v/release/CyanAutumn/NovalAi3AutoMatic)

A Windows client for NovelAI batch image generation and director tools. Supports multiple models, prompt templates, Vibe/Img2Img reference images, and automated runs.

![UI screenshot](source/image.png)

## Table of Contents
- [Features](#features)
- [System Requirements](#system-requirements)
- [Download & Install](#download--install)
- [First-time Setup](#first-time-setup)
- [Usage Guide](#usage-guide)
- [Parameter Reference](#parameter-reference)
- [Anlas Balance & Cost](#anlas-balance--cost)
- [Output & Naming](#output--naming)
- [Configuration & File Locations](#configuration--file-locations)
- [Auto Update](#auto-update)
- [FAQ](#faq)
- [Development & Build](#development--build)
- [Links](#links)
- [License](#license)

## Features
- Batch generation with pacing: run count and short/long sleep intervals.
- Multi-model support: NAI3 / NAI3 Furry / NAI4 Preview / NAI4 Full / NAI4.5 Curated / NAI4.5 Full / NAI5 Curated / NAI5 Full.
- Anlas tracking: can be enabled in the settings; it queries the balance, records the real cost and shows the estimated cost / remaining Anlas next to the Generate button (NAI5 additionally shows the Opus quota bar).
- Prompt templates: fixed/random artist, random prompt, Wildcard placeholders.
- Reference images: Vibe multi-reference (including `.naiv4vibe`), Img2Img strength/noise.
- Director tools: background removal, line art, sketch, colorize, emotion, declutter; single or batch.
- Output & logging: PNG/WEBP, prompt saved to TXT, filename format options, logs on disk.
- Preset management: save/switch multiple presets, auto-restore last session.
- Built-in auto-update (GitHub Releases).

## System Requirements
- Windows 10/11
- .NET Framework 4.8
- NovelAI account Token (optional proxy)

## Download & Install
1. Download and extract from Releases: <https://github.com/CyanAutumn/NovalAi3AutoMatic/releases>
2. Run `AutoNai3Tools.exe`.
3. First run creates default output and config folders.

## First-time Setup
1. Set `Token` (from NovelAI account).
2. If needed, set `Proxy`, e.g. `http://127.0.0.1:7890`.
3. Set directories:
   - Output directory `OutputPath`
   - Random prompt directory `RandomPromptFolderPath`
   - Wildcard directory `WildcardFolderPath`
4. Adjust model, resolution, Sampler, Steps, etc.

## Usage Guide

### Basic Generation Flow
1. Enter Prompt / Negative Prompt.
2. Select model and parameters (Steps, Sampler, Scale, CFG, etc.).
3. Set “Run Count” and “Keep Params Count”.
4. Click “Generate”; you can stop anytime.

### Prompt Template Syntax
| Syntax | Description | Example |
| --- | --- | --- |
| `<固定画师>` | Use content from “Fixed Artist” | `1girl, <固定画师>` |
| `<随机画师>` | Randomly combine from artist list | `1girl, <随机画师>` |
| `<随机提示词>` | Randomly pick from random prompt folder | `<随机提示词>` |
| `<随机提示词:顺序>` | Cycle in file order | `<随机提示词:顺序>` |
| `<xxx>` | Wildcard: read one line from `wildcard/xxx.txt` | `<衣服>` |
| `<xxx:顺序>` | Wildcard in sequence | `<衣服:顺序>` |

Notes:
- Each `.txt` file in the random prompt folder is treated as a prompt fragment (comma-separated). Newlines are treated as spaces.
- Each line in a Wildcard file is a candidate. Clicking a snippet inserts it into the prompt.
- Prompt blacklist and regex blacklist mainly filter random prompt results.

### Random Prompt Folder Format
Put multiple `.txt` files in the folder. Each file is a prompt snippet:
```
1girl, solo, masterpiece, best quality
```
`<随机提示词>` picks a random file. `<随机提示词:顺序>` cycles by file order.

### Wildcard Folder & Management
Each `.txt` file under `wildcard/` maps to a `<xxx>` placeholder:
```
wildcard/
  衣服.txt
  发型.txt
```
Example `衣服.txt` (one option per line):
```
hoodie
long coat
school uniform
```

### Artist List Format & Weights
“Random Artist” is grouped by line; within a line, use `|` to separate artists. Each artist supports weight parameters:
```
artistA|artistB
artistC,0,2,0,3|artistD
```
Weight format:
- `name,downMin,downMax,upMin,upMax`
- The system randomly wraps with `[]` or `{}` for down/up weight.
- If an artist ends with `::` (e.g. `artistE::`), it becomes `x::artistE::`.
- Optionally enable “Artist Modify” to add the `artist:` prefix.

### Vibe Reference Images
- Supports multiple references and `.naiv4vibe` files.
- Each image has `informationExtracted` and `referenceStrength`.
- `.naiv4vibe` generates a local cache on first use for faster reuse.

### Img2Img
- Select input image, set `Strength` and `Noise`.
- If no image is selected, Img2Img params are not sent.

### Director Tools
- Background removal, line art, sketch, colorize, emotion, declutter.
- Single image or folder batch.
- Colorize/emotion modes accept extra prompt and defry parameters.
- Outputs are saved to `OutputPath`.

### Preset Management
- Save/load/delete presets.
- Auto-load “auto-save from last close” on startup.

### Resolution & Parameter Hold
- “Keep Params Count” controls refresh frequency:
  - Example: set to 3 → refresh on run 1, keep for runs 2–3, refresh on run 4.
- You can keep random artist, Wildcard, random prompt, and resolution separately.
- Resolution modes:
  - Fixed: always use current resolution
  - Sequential: cycle through list order
  - Random: random pick from list

## Parameter Reference
| Parameter | Description | Notes |
| --- | --- | --- |
| Model | Model selection | NAI3 / NAI3 Furry / NAI4 Preview / NAI4 Full / NAI4.5 Curated / NAI4.5 Full / NAI5 Curated / NAI5 Full |
| Steps | Steps count | 1-28 (clamped) |
| Sampler | Sampler | `k_euler` / `k_euler_ancestral` / `k_dpmpp_2s_ancestral` / `k_dpmpp_2m_sde` / `k_dpmpp_2m` / `k_dpmpp_sde` / `ddim_v3` |
| Noise Schedule | Noise strategy | `native` / `karras` / `exponential` / `polyexponential` |
| Scale | Prompt Guidance | 0-10, 1 decimal |
| CFG Rescale | CFG Rescale | Same as NovelAI |
| SMEA / DYN | Sampling optimization | DYN turns off when SMEA is off |
| Decrisp | Decrisp | Same as NovelAI |
| Variety | Diversity | Off / On / Custom risk params |
| Resolution | Width / Height | Controlled by resolution list & mode |
| Resolution List | Multi-line input | e.g. `832x1216` |
| Run Count | RunNum | Total runs per click |
| Keep Params Count | RunKeepParams | Random refresh frequency |
| Fixed Seeds | FixedSeeds | If off, Seed randomizes each run |
| Seed | Seeds | Used when Fixed Seeds is on |
| Output Format | ImageFormat | PNG / WEBP |
| Output Filename Format | OutputFileNameFormat | NovalAI / All Artists / Date |
| Save Prompt | SavePromptToTxt | Save full prompt or without artists |
| Proxy | Proxy | Only if needed |
| Blacklist | PromptBlackList / Regex | Filters random prompts |

> Note: NAI2 (`nai-diffusion-2`) is officially retired and removed from NovelAI's model list; the server answers `model nai-diffusion-2 doesn't exist`, so the tool no longer offers it. A preset that still stores NAI2 is switched to NAI3 on load, with a log entry.

## NovelAI V5 Notes
Request bodies for `nai-diffusion-5-curated` / `nai-diffusion-5-full` follow the official web client rules and match the [official model docs](https://docs.novelai.net/en/image/models). Differences from V4.5:
- Uses `params_version: 4` and sends `qualityPresetId` (`standard` / `none`) plus `ucPresetId` instead of the legacy `qualityToggle` / `ucPreset`.
- The noise schedule is forced to `karras` (the official client does this for V5), so the Noise Schedule option has no effect on V5.
- With the `k_euler_ancestral` sampler, brownian noise is enabled (`prefer_brownian: true`, `deliberate_euler_ancestral_bug: false`); other samplers invert both flags.
- V5 does not support SMEA / DYN, Decrisp (`dynamic_thresholding`) or Variety (`skip_cfg_above_sigma`); those options are ignored for V5.
- Vibe Transfer is not exposed for V5 in the official client: a log warning is written when Vibe references are configured, and whether they take effect depends on the server.
- Official defaults: Steps 23, Prompt Guidance 7, Sampler `k_euler_ancestral`, resolution 832x1216.

## Anlas Balance & Cost
NovelAI exposes a balance endpoint but no public "price for these parameters" endpoint (the `/ai/generate-image/request-price` route that appears in the official frontend returns 404). This tool therefore:

- **Toggle**: `Track Anlas usage` on the settings page (on by default). When it is off, the balance endpoint is never called and the Generate button shows no extra information.
- **Button**: with tracking on, the Generate button gets an `estimate / remaining` suffix, e.g. `Generate    Anlas ≈23 / 8992`. A `≈` marks the value estimated by the fitted formula; without `≈` the value comes from the local measured cache. The balance shows `?` until it has been loaded (or when the endpoint is unavailable). While generating, the button reads `Stop    Anlas …`.
- **NAI5 is different**: NAI5 spends the Opus subscription quota instead of Anlas, so it shows `Generate    quota 0.085% / 100% + Anlas ≈35 / 8992` (the Anlas part only appears when those parameters really do cost Anlas; 1088x1088 or smaller with steps <= 28 is free and shows the quota bar alone).
- **Balance**: `GET {Api}/user/subscription`; the Anlas balance is `trainingStepsLeft.fixedTrainingStepsLeft + trainingStepsLeft.purchasedTrainingSteps` (same formula as the official web client) and the quota bar percentage comes from `usage.percent`.
- **Cost**: the balance is read before each generation and again afterwards; the difference is the real cost of that image and is written to the log together with model / size / steps / image count / sampler.
- **Local cache**: measured costs are stored per "model + size + steps + image count + action/strength" in `C:\Users\Public\Documents\auto_nai3_system\anlas_cost_cache.toml`. A cache hit is reused directly (the button then shows the measured value instead of an estimate); only a miss falls back to the before/after balance difference. Prompts never affect the cost, so they are not part of the key. Entries expire after 8 hours and are then measured again.
- A **Query Anlas** button in the top-right of the Log tab shows the balance, the Opus usage percentage and the subscription expiry at any time.
- **Request rate**: at most one extra balance request per image, results are reused for 30 seconds, failures back off exponentially (2 s up to 60 s), 5 consecutive failures stop tracking for the session and a 429 backs off 60 s; this puts no load on the server.
- If the configured API is a third-party proxy without this endpoint, a single warning is logged and automatic tracking stops for the session; generation is unaffected.

### Billing rules (measured against the live API)
- **Free**: a single image with width x height <= 1024x1024 (1,048,576 pixels) and steps <= 28 costs no Anlas.
- With more than one image (`n_samples > 1`) at a free size/steps, the first image is still free and the rest are billed with the formula below.
- Otherwise:

```
cost = ceil( megapixels x n_samples x f(steps) x modelMultiplier )
f(steps) = steps x 4/7 + 3.2
modelMultiplier: V5 (nai-diffusion-5-*) = 1.5, other models = 1.0
```

Measured samples (difference method, Opus / tier 3):

| Model | Size | Steps | Images | Measured | Estimate |
| --- | --- | --- | --- | --- | --- |
| NAI4.5 Full | 1024x1024 | 28 | 1 | 0 | 0 |
| NAI4.5 Full | 1024x1024 | 29 | 1 | 21 | 21 |
| NAI4.5 Full | 1088x1088 | 28 | 1 | 23 | 23 |
| NAI4.5 Full | 1088x1088 | 50 | 1 | 38 | 38 |
| NAI4.5 Full | 1472x1472 | 28 | 1 | 42 | 42 |
| NAI4.5 Full | 1472x1472 | 50 | 1 | 69 | 69 |
| NAI4.5 Full | 1088x1088 | 28 | 2 | 46 | 46 |
| NAI4.5 Full | 512x512 | 28 | 2 | 5 | 5 |
| NAI5 Full | 1088x1088 | 28 | 1 | 35 | 35 |
| NAI5 Full | 1472x1472 | 28 | 1 | 63 | 63 |

Notes:
- Sampler, Curated/Full and the difference between NAI3 and NAI4.5 do not change the cost.
- The server rejects requests above 1536x2048 with HTTP 400.
- The effect of Img2Img strength on cost is undocumented; the measured value in the log stays accurate.
## Output & Naming
Filename format depends on “Output Filename Format”:
- `NovalAI`: `{prompt} s-{seed}`
- `All Artists`: `{artist_summary}_{seed}`
- `Date`: `yyyyMMdd_HHmmss`

If “Save prompt to TXT” is enabled, a `.txt` file will be written next to the image.

## Configuration & File Locations
| Item | Default | Notes |
| --- | --- | --- |
| Output folder | `.\output` | Configurable in settings |
| Wildcard folder | `.\wildcard` | Stores `*.txt` snippets |
| Random prompt folder | `.\prompt\prompt_by_风吟` | Stores `*.txt` prompts |
| Presets | `C:\Users\Public\Documents\auto_nai3_2\*.toml` | Preset files |
| System config | `C:\Users\Public\Documents\auto_nai3_system\config.toml` | Token, sleep settings, etc. |
| Logs | `logs/mylog.txt` | log4net output |

## Auto Update
In non-debug mode, the app checks updates on startup via GitHub Releases.

## FAQ
1. Token invalid or request failed  
   Check token expiration and network access to NovelAI.

2. Wildcard/random prompt not working  
   Ensure folder exists with `.txt` files and the path is not empty.

3. No preview but files saved  
   WebP preview decode may fail; files are still saved.

4. Cannot save config  
   Check write permission for `C:\Users\Public\Documents\`.

## Development & Build
- Dependencies: Visual Studio 2022 + .NET Framework 4.8
- Open `AutoNai3Tools.sln`, restore NuGet packages, build `Release`.
- Project structure:
  - `controllers/` generation pipeline and director tools
  - `services/` config and wildcard services
  - `utils/` requests, logs, prompt parsing, Vibe handling
  - `body/` request bodies per model

## Links
- User guide: <https://cyanautumn.github.io/NovalAi3AutoMaticDoc/>
- NovelAI official docs: <https://docs.novelai.net/en/image/>
- NovelAI official model list: <https://docs.novelai.net/en/image/models>
- Prompt parsing: <https://spell.novelai.dev/>
- WD-Tagger: <https://huggingface.co/spaces/SmilingWolf/wd-tagger>

## License
License not specified.
