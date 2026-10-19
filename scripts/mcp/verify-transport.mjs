import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';
import { mkdir, writeFile } from 'node:fs/promises';

const project = resolve(import.meta.dirname, '../..');
const config = JSON.parse(await readFile(resolve(project, '.mcp.json'), 'utf8'));
const base = new URL(config.mcpServers['nanora-unity-mcp'].url).origin;
const headers = { 'Content-Type': 'application/json', Accept: 'application/json, text/event-stream', 'MCP-Protocol-Version': '2025-06-18' };
const envelope = (method, id = 1, params) => ({ jsonrpc: '2.0', id, method, params });
async function post(message, path = '/mcp', extra = {}) {
  return fetch(base + path, { method: 'POST', headers: { ...headers, ...extra },
    body: typeof message === 'string' ? message : JSON.stringify(message), signal: AbortSignal.timeout(20000) });
}
async function rpc(method, params, id = 1) {
  const response = await post(envelope(method, id, params));
  assert.equal(response.status, 200);
  const reply = await response.json();
  assert.equal(reply.id, id);
  assert.equal(reply.error, undefined, JSON.stringify(reply.error));
  return reply.result;
}
const health = await (await fetch(base + '/health')).json();
assert.equal(resolve(health.projectPath).toLowerCase(), project.toLowerCase());
const pkg = JSON.parse(await readFile(resolve(project, 'Assets/NanoraUnityMCP/package.json'), 'utf8'));
assert.equal(health.version, pkg.version);
const initParams = { protocolVersion: '2025-06-18', capabilities: {}, clientInfo: { name: 'nanora-transport-test', version: '1' } };
const initialized = await rpc('initialize', initParams, 'init');
assert.equal(initialized.protocolVersion, '2025-06-18');
assert.ok(initialized.instructions.includes(health.project));
assert.equal(initialized.serverInfo.instructions, undefined);
for (const method of ['notifications/initialized', 'notifications/cancelled', 'notifications/unknown']) {
  const response = await post({ jsonrpc: '2.0', method });
  assert.equal(response.status, 202);
  assert.equal(await response.text(), '');
}
assert.deepEqual(await rpc('ping', undefined, 0), {});
const tools = (await rpc('tools/list')).tools;
assert.deepEqual(tools.map(t => t.name), tools.map(t => t.name).sort());
for (const name of ['mcp_status', 'get_console_logs', 'execute_csharp', 'wait_for_selector', 'assert_ui_state'])
  assert.ok(tools.some(t => t.name === name), `${name} registered`);
const resources = (await rpc('resources/list')).resources;
assert.equal(resources.length, 2);
const state = JSON.parse((await rpc('resources/read', { uri: 'unity://editor/state' })).contents[0].text);
assert.equal(state.compilationFailed, false);
assert.equal(state.isCompiling, false);
assert.ok(state.scenes.some(s => s.name === 'L01_CenterStage_Leartes'));
assert.deepEqual((await rpc('resources/templates/list')).resourceTemplates, []);
assert.equal((await (await post('{')).json()).error.code, -32700);
const invalid = await (await post('[]')).json();
assert.equal(invalid.error.code, -32600);
assert.equal(invalid.id, null);
assert.equal((await (await post(envelope('missing-method', 90))).json()).error.code, -32601);
assert.equal((await (await post(envelope('tools/call', 91, []))).json()).error.code, -32602);
assert.equal((await post(envelope('ping'), '/mcp', { Origin: 'https://example.com' })).status, 403);
assert.equal((await post(envelope('ping'), '/mcp', { 'MCP-Protocol-Version': 'invalid' })).status, 400);
assert.equal((await fetch(base + '/mcp')).status, 405);
assert.equal((await fetch(base + '/mcp', { method: 'DELETE' })).status, 405);
assert.equal((await post(envelope('ping'), '/message?sessionId=expired')).status, 404);
assert.equal((await rpc('initialize', { ...initParams, protocolVersion: '2099-01-01' })).protocolVersion, '2025-11-25');
assert.equal((await rpc('initialize', { ...initParams, protocolVersion: '2025-11-25' })).protocolVersion, '2025-11-25');
assert.equal((await rpc('initialize', { ...initParams, protocolVersion: '2024-11-05' })).protocolVersion, '2024-11-05');

async function connectSse() {
  const abort = new AbortController();
  const response = await fetch(base + '/sse', { signal: abort.signal });
  assert.equal(response.status, 200);
  const events = [];
  const reader = response.body.getReader();
  const reading = (async () => {
    const decoder = new TextDecoder();
    let buffer = '';
    try {
      while (true) {
        const chunk = await reader.read();
        if (chunk.done) break;
        buffer += decoder.decode(chunk.value, { stream: true }).replaceAll('\r', '');
        let end;
        while ((end = buffer.indexOf('\n\n')) >= 0) {
          const packet = buffer.slice(0, end); buffer = buffer.slice(end + 2);
          const lines = packet.split('\n');
          if (lines.some(l => l.startsWith('data: '))) events.push({
            event: lines.find(l => l.startsWith('event: '))?.slice(7) ?? 'message',
            data: lines.filter(l => l.startsWith('data: ')).map(l => l.slice(6)).join('\n') });
        }
      }
    } catch (error) { if (!abort.signal.aborted) throw error; }
  })();
  async function waitFor(predicate) {
    for (let i = 0; i < 100; i++) { const event = events.find(predicate); if (event) return event; await delay(50); }
    throw Error('SSE event did not arrive');
  }
  const endpoint = new URL((await waitFor(e => e.event === 'endpoint')).data);
  return { events, waitFor, path: endpoint.pathname + endpoint.search, close: async () => { abort.abort(); await reading; } };
}
const peers = [];
try {
  peers.push(await connectSse(), await connectSse());
  const [a, b] = peers;
  assert.notEqual(a.path, b.path);
  await Promise.all([
    post(envelope('ping', 7), a.path).then(r => assert.equal(r.status, 202)),
    post(envelope('initialize', 7, initParams), b.path).then(r => assert.equal(r.status, 202))
  ]);
  const aReply = JSON.parse((await a.waitFor(e => e.event === 'message')).data);
  const bReply = JSON.parse((await b.waitFor(e => e.event === 'message')).data);
  assert.deepEqual(aReply.result, {});
  assert.equal(bReply.result.serverInfo.name, 'nanora-unity-mcp');
  await rpc('ping', undefined, 88);
  await delay(200);
  for (const peer of peers) assert.equal(peer.events.filter(e => e.event === 'message').length, 1, 'responses must not leak between clients');
} finally { await Promise.all(peers.map(p => p.close())); }

const status = await rpc('tools/call', { name: 'mcp_status', arguments: { include_tool_list: false, include_system_info: false } });
assert.ok(!status.isError, JSON.stringify(status));
assert.equal(resolve(status.structuredContent.projectPath).toLowerCase(), project.toLowerCase());
const report = { passed: true, server: health, toolCount: tools.length, resources: resources.map(r => r.uri), editor: state,
  checks: ['handshake', 'notification 202', 'JSON-RPC errors', 'version negotiation', 'Origin validation', 'HTTP methods', 'SSE session isolation', 'tool call', 'live compilation state'] };
await mkdir(resolve(project, 'Temp/mcp-validation'), { recursive: true });
await writeFile(resolve(project, 'Temp/mcp-validation/transport.json'), JSON.stringify(report, null, 2));
console.log(JSON.stringify(report, null, 2));
