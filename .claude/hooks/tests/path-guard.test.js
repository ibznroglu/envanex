'use strict';

const { test, after } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('fs');
const path = require('path');

const { runHook: spawnHook } = require('./run-hook');

const SCRIPT = path.join(__dirname, '..', 'path-guard.js');
const DEFAULT_PROJECT_DIR = 'C:\\projects\\envanex';

const logDirs = [];

function runHook(...args) {
  const result = spawnHook(...args);
  logDirs.push(result.logDir);
  return result;
}

after(() => {
  for (const dir of logDirs) {
    fs.rmSync(dir, { recursive: true, force: true });
  }
});

function runWrite(filePath, env = {}) {
  const stdin = JSON.stringify({ tool_name: 'Write', tool_input: { file_path: filePath } });
  return runHook(SCRIPT, [], stdin, { CLAUDE_PROJECT_DIR: DEFAULT_PROJECT_DIR, ...env });
}

const GUARD_ERROR = 'guard error';
const PROTECTED_FILE_NAME = 'protected file name';

function protectedSegment(name) {
  return `protected segment "${name}"`;
}

function assertBlocked(result, reason) {
  assert.equal(result.status, 2, `expected a block, got ${result.status}; stderr: ${result.stderr}`);
  assert.ok(result.stderr.includes(reason), `expected reason "${reason}" in stderr: ${result.stderr}`);
}

function assertAllowed(result) {
  assert.equal(result.status, 0, `expected an allow, got ${result.status}; stderr: ${result.stderr}`);
}

function readLog(logDir) {
  return fs.readFileSync(path.join(logDir, 'hooks.jsonl'), 'utf8');
}

// Blocks

test('blocks a backslash obj path', () => {
  assertBlocked(runWrite('C:\\projects\\envanex\\obj\\guard-test.txt'), protectedSegment('obj'));
});

test('blocks a forward-slash obj path', () => {
  assertBlocked(runWrite('obj/guard-test.txt'), protectedSegment('obj'));
});

test('blocks a nested bin path', () => {
  assertBlocked(runWrite('src\\Envanex.Web\\bin\\Debug\\x.dll'), protectedSegment('bin'));
});

test('blocks a git-bash style path', () => {
  assertBlocked(runWrite('/c/projects/envanex/obj/x'), protectedSegment('obj'));
});

test('blocks with a git-bash-style CLAUDE_PROJECT_DIR', () => {
  assertBlocked(runWrite('obj\\x.txt', { CLAUDE_PROJECT_DIR: '/c/projects/envanex' }), protectedSegment('obj'));
});

test('blocks an uppercase OBJ segment', () => {
  assertBlocked(runWrite('OBJ\\x'), protectedSegment('obj'));
});

test('blocks .git, .vs, .idea and packages segments', () => {
  const cases = [
    ['.git\\config', '.git'],
    ['.vs\\x', '.vs'],
    ['.idea\\workspace.xml', '.idea'],
    ['packages\\x.nupkg', 'packages'],
  ];
  for (const [target, segment] of cases) {
    assertBlocked(runWrite(target), protectedSegment(segment));
  }
});

test('blocks .env', () => {
  assertBlocked(runWrite('.env'), PROTECTED_FILE_NAME);
});

test('blocks .env.local', () => {
  assertBlocked(runWrite('.env.local'), PROTECTED_FILE_NAME);
});

test('blocks .env.example.bak', () => {
  assertBlocked(runWrite('.env.example.bak'), PROTECTED_FILE_NAME);
});

test('blocks settings.local.json', () => {
  assertBlocked(runWrite('.claude\\settings.local.json'), PROTECTED_FILE_NAME);
});

test('blocks appsettings.Development.Local.json', () => {
  assertBlocked(runWrite('src\\Envanex.Web\\appsettings.Development.Local.json'), PROTECTED_FILE_NAME);
});

test('blocks .user', () => {
  assertBlocked(runWrite('src\\Envanex.Web\\Envanex.Web.csproj.user'), PROTECTED_FILE_NAME);
});

test('blocks .pfx', () => {
  assertBlocked(runWrite('certs\\dev.pfx'), PROTECTED_FILE_NAME);
});

test('blocks .snk', () => {
  assertBlocked(runWrite('keys\\envanex.snk'), PROTECTED_FILE_NAME);
});

test('blocks secrets.json', () => {
  assertBlocked(
    runWrite('C:\\Users\\jesus\\AppData\\Roaming\\Microsoft\\UserSecrets\\x\\secrets.json'),
    PROTECTED_FILE_NAME,
  );
});

test('blocks on malformed JSON', () => {
  const result = runHook(SCRIPT, [], '{not json', { CLAUDE_PROJECT_DIR: DEFAULT_PROJECT_DIR });
  assertBlocked(result, GUARD_ERROR);
});

test('blocks on an empty file path', () => {
  assertBlocked(runWrite(''), GUARD_ERROR);
});

test('blocks when no project dir can be determined', () => {
  const stdin = JSON.stringify({ tool_name: 'Write', tool_input: { file_path: 'docs/x.md' } });
  const result = runHook(SCRIPT, [], stdin, { CLAUDE_PROJECT_DIR: undefined });
  assertBlocked(result, GUARD_ERROR);
  assert.ok(result.stderr.includes('no project dir'), result.stderr);
});

test('block writes the reason to stderr before exiting', () => {
  const result = runWrite('obj/guard-test.txt');
  assertBlocked(result, protectedSegment('obj'));
  assert.ok(result.stderr.includes('Blocked by path-guard:'), result.stderr);
  assert.ok(result.stderr.includes('obj/guard-test.txt'), result.stderr);
});

test('redacts the block message on stderr', () => {
  const result = runWrite('obj/Password=S3cretValue2.txt');
  assertBlocked(result, protectedSegment('obj'));
  assert.ok(result.stderr.includes('Blocked by path-guard:'), result.stderr);
  assert.ok(!result.stderr.includes('S3cretValue2'), result.stderr);
  const log = readLog(result.logDir);
  assert.ok(!log.includes('S3cretValue2'), log);
});

// Allows

test('allows .env.example', () => {
  assertAllowed(runWrite('.env.example'));
  assertAllowed(runWrite('C:\\projects\\envanex\\.env.example'));
});

test('allows src\\Envanex.Web\\Program.cs', () => {
  assertAllowed(runWrite('src\\Envanex.Web\\Program.cs'));
});

test('allows a segment that only contains obj', () => {
  assertAllowed(runWrite('docs/objects.md'));
  assertAllowed(runWrite('src/Binary/x.cs'));
});

test('allows .github/workflows/ci.yml', () => {
  assertAllowed(runWrite('.github/workflows/ci.yml'));
});

test('allows .gitignore', () => {
  assertAllowed(runWrite('.gitignore'));
});

test('allows .gitattributes', () => {
  assertAllowed(runWrite('.gitattributes'));
});

test('allows thoughts/shared/plans/x.md', () => {
  assertAllowed(runWrite('thoughts/shared/plans/x.md'));
});

// Log

test('writes one hook-log line per decision', () => {
  const result = runWrite('obj/guard-test.txt');
  assertBlocked(result, protectedSegment('obj'));
  const lines = readLog(result.logDir).trim().split('\n');
  assert.equal(lines.length, 1);
  const entry = JSON.parse(lines[0]);
  assert.equal(entry.decision, 'block');
  assert.equal(entry.hook, 'path-guard');
  assert.equal(entry.project_dir_raw, DEFAULT_PROJECT_DIR);
  const raw = lines[0];
  assert.ok(raw.includes('"decision":"block"'), raw);
  assert.ok(raw.includes('"hook":"path-guard"'), raw);
  assert.ok(raw.includes('"project_dir_raw"'), raw);
});
