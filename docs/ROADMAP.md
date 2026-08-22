# ROADMAP — Fangtastic Palette

Status: **shipped**. v1.0.0 is live on Nexus (mod 143). This is a small, complete mod; the roadmap is
short.

## Done
- 🛠 **1.0.1** (prepped 2026-08-22) — bug-fix: the bat tab icon is embedded in the DLL, so it ships
  with the plugin and shows on a fresh install (previously fell back to a placeholder glyph). Ready
  to publish.
- ✅ **1.0.0** — full Bat Form recolour (body/ears/fangs/mouth/face/eyes/pupil/glint/wing-dust),
  intensity sliders, wardrobe tab with live preview + picker, published to Nexus. Dev probe/debug
  harness stripped for release (lives in git history for the mermaid port).

## Next (opportunistic — see [BACKLOG.md](BACKLOG.md))
- 📋 Structural cleanups when a functional change next touches these files (spec value-types, panel
  split, palette-config owner, cache lifecycle) — none blocking; all want in-game verification.

## Set context
- 📋 **Fintastic Palette** (mermaid/Aqua Form) is the planned third entry, intended to reuse this
  mod's recolour machinery. That port is the natural trigger to extract the shared engine (BACKLOG →
  workspace-level) and to split `TextureRecolor` into generic + character-specific halves.

_Living doc — refresh with /project-docs when it drifts._
