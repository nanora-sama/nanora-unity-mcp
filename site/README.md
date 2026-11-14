# Documentation site

Source of https://nanora-sama.github.io/nanora-unity-mcp/ (Astro Starlight, English at `/`, Japanese at `/ja/`).

```sh
cd site
npm ci
npm run dev      # http://localhost:4321/nanora-unity-mcp/
npm run build    # writes dist/
```

- `src/content/docs/` — hand-written pages. Every English page has a Japanese one under `ja/` at the same path.
- `scripts/gen-tools.mjs` — generates `tools/` and `ja/tools/` (one page per tool) from the `[McpTool]` classes and
  `ToolCategories.cs`, and copies images from `../media`. The output is git-ignored; `dev` and `build` run it first.
  It fails if a tool is missing from `ToolCategories.cs` or its input schema is not valid JSON.
- `.github/workflows/docs.yml` builds on pull requests and deploys to GitHub Pages from `main` of the public repository.

When you add or rename a page, add it to the sidebar in `astro.config.mjs`.
