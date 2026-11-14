// Generates one reference page per MCP tool, in English and Japanese, from the C# tool classes.
// The pages are build output (git-ignored) so they can never drift from the code.
//
//   node scripts/gen-tools.mjs
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { slug } from 'github-slugger';

const siteDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const pkgDir = path.resolve(siteDir, '../Assets/NanoraUnityMCP');
const editorDir = path.join(pkgDir, 'Editor');
const repoUrl = 'https://github.com/nanora-sama/nanora-unity-mcp';
const version = JSON.parse(fs.readFileSync(path.join(pkgDir, 'package.json'), 'utf8')).version;

const walk = (dir) => fs.readdirSync(dir, { withFileTypes: true }).flatMap((e) =>
  e.isDirectory() ? (e.name === 'Tests' ? [] : walk(path.join(dir, e.name)))
    : e.name.endsWith('.cs') ? [path.join(dir, e.name)] : []);

// C# string literals: "..." with \ escapes, and @"..." with "" escapes.
function readLiterals(src) {
  const out = [];
  const re = /@"((?:[^"]|"")*)"|"((?:[^"\\]|\\.)*)"/gs;
  let m;
  while ((m = re.exec(src))) {
    out.push(m[1] !== undefined ? m[1].replace(/""/g, '"')
      : m[2].replace(/\\(["\\nt])/g, (_, c) => ({ n: '\n', t: '\t' }[c] ?? c)));
  }
  return out.join('');
}

function parseTools() {
  const tools = [];
  for (const file of walk(editorDir)) {
    const src = fs.readFileSync(file, 'utf8');
    const marks = [...src.matchAll(/\[McpTool\("([^"]+)"\)\]/g)];
    marks.forEach((mark, i) => {
      const body = src.slice(mark.index, marks[i + 1]?.index ?? src.length);
      const desc = body.match(/string\s+Description\s*=>\s*([\s\S]*?);/);
      const schema = body.match(/InputSchema\s*=>\s*JObject\.Parse\(\s*(@"(?:[^"]|"")*")\s*\)/);
      let inputSchema = null;
      if (schema) {
        try { inputSchema = JSON.parse(readLiterals(schema[1])); }
        catch (e) { throw new Error(`${mark[1]}: input schema is not valid JSON (${e.message})`); }
      }
      tools.push({
        name: mark[1],
        description: desc ? readLiterals(desc[1]).trim() : '',
        schema: inputSchema,
        source: path.relative(path.resolve(siteDir, '..'), file).replace(/\\/g, '/'),
      });
    });
  }
  return tools.sort((a, b) => a.name.localeCompare(b.name));
}

function parseCategories() {
  const all = fs.readFileSync(path.join(editorDir, 'Core/ToolCategories.cs'), 'utf8');
  const src = all.slice(0, all.indexOf('ToolBriefs'));
  const starts = [...src.matchAll(/\["(\w+)"\]\s*=\s*new ToolCategoryInfo\(/g)];
  return starts.map((m, i) => {
    const args = src.slice(m.index + m[0].length, starts[i + 1]?.index ?? src.length);
    const strings = [...args.matchAll(/"((?:[^"\\]|\\.)*)"/g)].map((s) => s[1]);
    return { key: m[1], name: strings[0], description: strings[1], tools: strings.slice(2) };
  });
}

// Japanese names for the categories; the English ones come from ToolCategories.cs.
const jaCategory = {
  scene: ['シーン', 'GameObject を作る・変える'], query: ['検索', 'オブジェクトとアセットを探す'],
  batch: ['一括操作', 'たくさんのオブジェクトをまとめて操作'], code: ['コード', 'C# の実行とコンパイル'],
  playmode: ['Play モード', 'Play モードの操作'], test: ['テスト', 'テストの実行・E2E シナリオ・見た目の回帰・性能の予算'],
  performance: ['性能', 'フレーム・メモリ・CPU の計測'], materials: ['マテリアル', '壊れたマテリアルの診断と修復'],
  diagnostics: ['診断', 'スクリーンショット・ログ・接続と設定の確認'], scene_mgmt: ['シーン管理', 'シーンファイルを開く・保存する'],
  animation: ['アニメーション', 'Animator とアニメーションクリップ'], ui: ['UI', 'UI の要素と Canvas'],
  audio: ['音', 'AudioSource と再生'], lighting: ['ライト', 'ライトと全体のライティング設定'],
  camera: ['カメラ', 'カメラの操作と Cinemachine'], editor: ['Editor', 'Editor の操作・リフレクション・一括実行'],
  assets: ['アセット', 'アセットファイルとプレハブの操作'], packages: ['パッケージ', 'Package Manager でパッケージを探す・入れる・外す'],
  meta: ['SuperSave（メタツール）', 'SuperSave モードで分類をたどり、ツールを呼ぶ'],
};

const L = {
  en: {
    prefix: '', dir: 'tools',
    metaDesc: (t) => `${t.name} — ${t.description} An MCP tool of nanora-unity-mcp, the MCP server that runs inside the Unity Editor.`,
    titleSuffix: 'Unity MCP tool reference',
    category: 'Category', params: 'Parameters', noParams: 'This tool takes no parameters.',
    head: ['Name', 'Type', 'Required', 'Default', 'Description'], yes: 'yes', values: 'One of',
    example: 'Example call', exampleNote: 'The arguments below are the required ones; add the optional ones you need.',
    supersave: 'In SuperSave mode', supersaveText: (t) => `When Unity runs with \`NANORA_MCP_SUPERSAVE=1\`, call it through the \`execute\` meta tool: \`{"tool": "${t.name}", "args": { … }}\`. See [SuperSave mode](${'BASE'}guides/supersave/).`,
    source: 'Source', sourceText: (t) => `Generated from [\`${t.source}\`](${repoUrl}/blob/main/${t.source}) (version ${version}). The client receives the same schema from \`tools/list\`.`,
    dynamic: 'The parameters of this tool are built at run time; ask the server with `tools/list`.',
    indexTitle: 'Tool reference', indexDesc: `All ${'N'} MCP tools of nanora-unity-mcp by category, with parameters and example calls, generated from the source.`,
    indexIntro: (n, c) => `nanora-unity-mcp exposes ${n} tools in ${c} categories, plus the three SuperSave meta tools. Each page is generated from the tool's C# class, so the parameters match what your AI client receives from \`tools/list\`.`,
    metaName: 'SuperSave (meta tools)', metaDescription: 'Browse categories and call tools when SuperSave mode hides the full list',
  },
  ja: {
    prefix: 'ja/', dir: 'ja/tools',
    metaDesc: (t) => `Unity Editor の中で動く MCP サーバー nanora-unity-mcp の ${t.name} ツール。${t.description}`,
    titleSuffix: 'Unity MCP のツール',
    category: '分類', params: 'パラメータ', noParams: 'パラメータはありません。',
    head: ['名前', '型', '必須', '既定値', '説明'], yes: '必須', values: '値',
    example: '呼び出しの例', exampleNote: '必須の引数だけを入れています。ほかは必要に応じて足してください。',
    supersave: 'SuperSave モードでは', supersaveText: (t) => `Unity を \`NANORA_MCP_SUPERSAVE=1\` で起動したときは、メタツール \`execute\` から \`{"tool": "${t.name}", "args": { … }}\` の形で呼びます。[SuperSave モード](${'BASE'}ja/guides/supersave/)を参照。`,
    source: '出典', sourceText: (t) => `[\`${t.source}\`](${repoUrl}/blob/main/${t.source})（版 ${version}）から生成しています。説明文はツール定義のまま（英語）です。AI クライアントは \`tools/list\` で同じスキーマを受け取ります。`,
    dynamic: 'このツールのパラメータは実行時に組み立てられます。`tools/list` でサーバーに問い合わせてください。',
    indexTitle: 'ツールリファレンス', indexDesc: 'nanora-unity-mcp の MCP ツール全部を分類ごとに。パラメータと呼び出しの例をソースから生成。',
    indexIntro: (n, c) => `nanora-unity-mcp は ${c} 分類・${n} ツールと、SuperSave モード用のメタツール 3 本を持っています。各ページはツールの C# クラスから生成しているので、AI クライアントが \`tools/list\` で受け取る内容と同じです。`,
    metaName: 'SuperSave（メタツール）', metaDescription: 'SuperSave モードで分類をたどり、ツールを呼ぶ',
  },
};

const BASE = '/nanora-unity-mcp/';
const esc = (s) => String(s ?? '').replace(/\|/g, '\\|').replace(/\r?\n/g, ' ');
const yamlStr = (s) => JSON.stringify(s);

function exampleArgs(schema) {
  const args = {};
  for (const key of schema?.required ?? []) {
    const p = schema.properties?.[key] ?? {};
    args[key] = p.enum?.[0] ?? p.default ?? ({ string: '…', integer: 0, number: 0, boolean: true, array: [], object: {} }[p.type] ?? '…');
  }
  return args;
}

function toolPage(t, cat, lang) {
  const l = L[lang];
  const catName = lang === 'ja' ? jaCategory[cat.key]?.[0] ?? cat.name : cat.name;
  const props = t.schema?.properties ?? {};
  const required = new Set(t.schema?.required ?? []);
  const rows = Object.entries(props).map(([k, p]) => {
    const type = Array.isArray(p.type) ? p.type.join(' \\| ') : p.type ?? (p.oneOf ? 'any' : '');
    const text = esc(p.description).trim();
    const desc = (text && p.enum && !/[.:)]$/.test(text) ? `${text}.` : text) + (p.enum ? ` ${l.values}: ${p.enum.map((v) => `\`${v}\``).join(', ')}` : '');
    const def = p.default === undefined ? '' : `\`${esc(JSON.stringify(p.default))}\``;
    return `| \`${k}\` | ${type} | ${required.has(k) ? l.yes : ''} | ${def} | ${desc} |`;
  });
  const params = !t.schema ? l.dynamic
    : rows.length === 0 ? l.noParams
    : [`| ${l.head.join(' | ')} |`, `|${l.head.map(() => '---').join('|')}|`, ...rows].join('\n');
  const call = JSON.stringify({ name: t.name, arguments: exampleArgs(t.schema) }, null, 2);
  const fix = (s) => s.replaceAll('BASE', BASE);
  return `---
title: ${yamlStr(t.name)}
description: ${yamlStr(l.metaDesc(t).slice(0, 300))}
head:
  - tag: title
    content: ${yamlStr(`${t.name} — ${l.titleSuffix} | nanora-unity-mcp`)}
---

${t.description}

**${l.category}:** [${catName}](${BASE}${l.dir}/#${slug(catName)})

## ${l.params}

${params}

## ${l.example}

${l.exampleNote}

\`\`\`json
${call}
\`\`\`
${cat.key === 'meta' ? '' : `
## ${l.supersave}

${fix(l.supersaveText(t))}
`}
## ${l.source}

${l.sourceText(t)}
`;
}

function indexPage(tools, cats, lang) {
  const l = L[lang];
  const byName = new Map(tools.map((t) => [t.name, t]));
  const counted = tools.filter((t) => !cats.find((c) => c.key === 'meta').tools.includes(t.name)).length;
  const sections = cats.map((c) => {
    const [name, desc] = lang === 'ja' && jaCategory[c.key] ? jaCategory[c.key] : [c.name, c.description];
    const list = c.tools.filter((n) => byName.has(n)).map((n) =>
      `- [\`${n}\`](${BASE}${l.dir}/${n.replace(/_/g, '-')}/) — ${byName.get(n).description.split(/(?<=\.)\s/)[0]}`);
    return `## ${name}\n\n${desc}\n\n${list.join('\n')}`;
  });
  return `---
title: ${yamlStr(l.indexTitle)}
description: ${yamlStr(l.indexDesc.replace('N', counted))}
sidebar:
  order: 0
---

${l.indexIntro(counted, cats.length - 1)}

${sections.join('\n\n')}
`;
}

const tools = parseTools();
const cats = parseCategories();
const metaTools = tools.filter((t) => ['list_categories', 'list_tools', 'execute'].includes(t.name)).map((t) => t.name);
cats.push({ key: 'meta', name: L.en.metaName, description: L.en.metaDescription, tools: metaTools });

// list_tools builds its schema in code (the category enum); mirror it here.
const listTools = tools.find((t) => t.name === 'list_tools');
if (listTools && !listTools.schema) {
  listTools.schema = { type: 'object', required: ['category'], properties: { category: {
    type: 'string', description: 'Category name, as listed by list_categories',
    enum: cats.filter((c) => c.key !== 'meta').map((c) => c.key) } } };
}

const uncategorized = tools.filter((t) => !cats.some((c) => c.tools.includes(t.name)));
if (uncategorized.length) throw new Error(`tools missing from ToolCategories.cs: ${uncategorized.map((t) => t.name).join(', ')}`);
const missing = cats.flatMap((c) => c.tools).filter((n) => !tools.some((t) => t.name === n));
if (missing.length) console.warn(`listed in ToolCategories.cs but no [McpTool] class: ${missing.join(', ')}`);

for (const lang of ['en', 'ja']) {
  const dir = path.join(siteDir, 'src/content/docs', L[lang].dir);
  fs.rmSync(dir, { recursive: true, force: true });
  fs.mkdirSync(dir, { recursive: true });
  for (const t of tools) {
    const cat = cats.find((c) => c.tools.includes(t.name));
    fs.writeFileSync(path.join(dir, `${t.name.replace(/_/g, '-')}.md`), toolPage(t, cat, lang));
  }
  fs.writeFileSync(path.join(dir, 'index.md'), indexPage(tools, cats, lang));
}
console.log(`generated ${tools.length} tool pages x 2 languages (${cats.length} categories, version ${version})`);

// Images come from the repository's media folder so they are not stored twice.
const media = path.resolve(siteDir, '../media');
const copies = {
  'icon-160.png': ['public/favicon.png', 'src/assets/icon.png'],
  'social-preview.png': ['public/og.png'],
  'social-preview-ja.png': ['public/og-ja.png'],
  'screenshot-editor.png': ['public/screenshot-editor.png'],
  'demo/demo-stream.gif': ['public/demo-stream.gif'],
  'demo/demo-game.gif': ['public/demo-game.gif'],
};
for (const [from, targets] of Object.entries(copies)) {
  for (const to of targets) {
    fs.mkdirSync(path.dirname(path.join(siteDir, to)), { recursive: true });
    fs.copyFileSync(path.join(media, from), path.join(siteDir, to));
  }
}
