// Exercise the installed Codex MCP client via app-server RPC. No model turn is started.
import { spawn } from 'node:child_process';
import { createInterface } from 'node:readline';
import { resolve } from 'node:path';
import { readFile } from 'node:fs/promises';
import { mkdir, writeFile } from 'node:fs/promises';
import assert from 'node:assert/strict';
const cwd = resolve(import.meta.dirname, '../..');
const config = JSON.parse(await readFile(resolve(cwd, '.mcp.json'), 'utf8'));
const endpoint = new URL('/mcp', config.mcpServers['nanora-unity-mcp'].url).href;
const cliArgs = ['app-server', '--stdio'];
if (process.argv.includes('--candidate')) cliArgs.push('-c', `mcp_servers.nanora-unity-mcp.url=${JSON.stringify(endpoint)}`);
const child = spawn('codex', cliArgs,
  { cwd, windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] });
const pending = new Map(); let serial = 0; let stderr = '';
child.stderr.on('data', b => { stderr = (stderr + b).slice(-6000); });
const lines = createInterface({ input: child.stdout });
lines.on('line', line => {
  let message; try { message = JSON.parse(line); } catch { return; }
  if (pending.has(message.id)) {
    const { resolve, reject, timeout } = pending.get(message.id);
    clearTimeout(timeout); pending.delete(message.id);
    if (message.error) reject(Error(JSON.stringify(message.error))); else resolve(message.result);
  }
});
function send(method, params) {
  return new Promise((resolve, reject) => {
    const id = ++serial;
    const timeout = setTimeout(() => { pending.delete(id); reject(Error(`Timed out: ${method}\n${stderr}`)); }, 45000);
    pending.set(id, { resolve, reject, timeout });
    child.stdin.write(JSON.stringify({ id, method, params }) + '\n');
  });
}
try {
  const client = await send('initialize', { clientInfo: { name: 'nanora_mcp_verifier', version: '1.0' }, capabilities: { experimentalApi: true } });
  child.stdin.write(JSON.stringify({ method: 'initialized' }) + '\n');
  const thread = await send('thread/start', { cwd, ephemeral: true });
  const threadId = thread.thread.id;
  const status = await send('mcpServerStatus/list', { threadId });
  const server = status.data.find(s => s.name === 'nanora-unity-mcp');
  assert.ok(server, JSON.stringify(status));
  assert.ok(Object.keys(server.tools).length >= 47, JSON.stringify(server));
  const result = await send('mcpServer/tool/call', { threadId, server: 'nanora-unity-mcp', tool: 'mcp_status', arguments: { include_tool_list: false, include_system_info: false } });
  assert.equal(resolve(result.structuredContent.projectPath).toLowerCase(), cwd.toLowerCase());
  assert.ok(!result.isError, JSON.stringify(result));
  const report = { passed: true, verifiedAt: new Date().toISOString(), client, server: server.name,
    toolCount: Object.keys(server.tools).length, resources: server.resources, status: result.structuredContent };
  await mkdir(resolve(cwd, 'Temp/mcp-validation'), { recursive: true });
  await writeFile(resolve(cwd, 'Temp/mcp-validation/codex.json'), JSON.stringify(report, null, 2));
  console.log(JSON.stringify(report, null, 2));
} finally {
  for (const p of pending.values()) clearTimeout(p.timeout);
  lines.close(); child.stdin.end(); child.kill();
}
