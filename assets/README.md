# Original artwork / 原创资源

All three SVGs were drawn specifically for this project as editable vector paths and are covered by the repository MIT license. No game textures, icons, fonts, asset bundles, or extracted resources are included. The two technology emblems use abstract faceted-energy and information-lattice motifs; they are not traced copies of game artwork. Items and recipes are intended to reference existing vanilla icons at runtime.

- `source/energy-analysis.svg` and `source/information-topology.svg`: white foreground, transparent background, 256 × 256 view box
- `source/package-icon.svg`: original colored package emblem
- `generated/*.png`: 256 × 256 RGBA textures, with reproducible source/output hashes in `assets-manifest.json`
- `../icon.png`: exact copy of the generated package icon for Thunderstore packaging

Regenerate with `python scripts/generate-assets.py` using Python, Pillow, and official [Inkscape](https://inkscape.org/). Check committed output without Inkscape using `python scripts/generate-assets.py --check`. The renderer version is recorded in the manifest; PNG compression or antialiasing can differ between renderer versions. Never silently update output hashes without inspecting regenerated art.

The PNG dimensions, transparent corners, partial alpha edges, source hashes, and pure-white visible pixels in the technology icons are checked automatically. Actual Unity sprite loading, in-game tint contrast, and technology-tree small-size readability remain **NOT EXECUTED** until the runtime acceptance tests are performed.

## SVG checkout newline policy

The manifest hashes the exact SVG bytes, including newlines. The root `.gitattributes` keeps `assets/source/*.svg` as LF text even when Git uses `core.autocrlf=true`; do not regenerate the manifest merely to accept checkout conversion. Source archives preserve this policy file. Genuine SVG edits still require reviewed regeneration.

Run `python scripts/test-checkout-assets.py` with Git and Pillow installed. The test creates temporary repositories from the included assets and validator, so it also runs from an extracted source archive without a `.git` directory. Fresh checkouts exercise `core.autocrlf=false`, `input` and `true`, verify all three raw SVG hashes and the existing asset check, and reject a deliberate non-newline SVG change. An unprotected control file proves CRLF conversion actually ran. The test does not modify this checkout or user Git settings; it is a Git checkout regression, not native Windows build or game-runtime validation.

三张 SVG 均为本项目原创矢量图形，按仓库 MIT 许可证分发；不包含游戏资源或字体。科技图标为白色前景、透明背景。生成脚本验证尺寸、透明度和哈希；Unity 加载、游戏着色效果和科技树中的小尺寸显示仍待实际游戏验收。
