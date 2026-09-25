'use strict';

const { test, beforeEach, afterEach } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('fs');
const os = require('os');
const path = require('path');

const {
  normalizePath,
  normalizeProjectDir,
  getTargetPath,
  getCommand,
  logDecision,
} = require('../lib/guard-common');

const PROJECT = 'c:/projects/envanex';

let tempDirs = [];
let savedOverride;

function makeTempDir(prefix) {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), prefix));
  tempDirs.push(dir);
  return dir;
}

beforeEach(() => {
  savedOverride = process.env.ENVANEX_HOOK_LOG_DIR;
  process.env.ENVANEX_HOOK_LOG_DIR = makeTempDir('envanex-hooklog-');
});

afterEach(() => {
  if (savedOverride === undefined) {
    delete process.env.ENVANEX_HOOK_LOG_DIR;
  } else {
    process.env.ENVANEX_HOOK_LOG_DIR = savedOverride;
  }
  for (const dir of tempDirs) {
    fs.rmSync(dir, { recursive: true, force: true });
  }
  tempDirs = [];
});

function ctxFor(projectDir) {
  return { hook: 'guard-common-test', projectDir, projectDirRaw: projectDir };
}

function logAndRead(target) {
  logDecision(ctxFor(PROJECT), {
    hook: 'guard-common-test',
    decision: 'block',
    reason: 'test',
    tool_name: 'Bash',
    target,
  });
  const file = path.join(process.env.ENVANEX_HOOK_LOG_DIR, 'hooks.jsonl');
  const lines = fs.readFileSync(file, 'utf8').trim().split('\n');
  assert.equal(lines.length, 1);
  return lines[0];
}

// normalizePath

test('normalizePath converts backslashes', () => {
  assert.equal(normalizePath('C:\\projects\\envanex\\obj\\x', PROJECT), 'c:/projects/envanex/obj/x');
});

test('normalizePath maps /c/ to c:/', () => {
  assert.equal(normalizePath('/c/projects/envanex/obj/x', PROJECT), 'c:/projects/envanex/obj/x');
});

test('normalizePath maps an uppercase /C/ drive', () => {
  assert.equal(normalizePath('/C/projects/envanex/obj/x', PROJECT), 'c:/projects/envanex/obj/x');
  assert.equal(normalizePath('/C', PROJECT), 'c:/');
});

test('normalizePath joins a relative path to the project dir', () => {
  assert.equal(normalizePath('obj/x.txt', PROJECT), 'c:/projects/envanex/obj/x.txt');
  assert.equal(normalizePath('src\\Envanex.Web\\Program.cs', PROJECT), 'c:/projects/envanex/src/envanex.web/program.cs');
});

test('normalizePath treats a leading / without a drive letter as absolute', () => {
  assert.equal(normalizePath('/tmp/x', PROJECT), '/tmp/x');
});

test('normalizePath collapses . and .. segments', () => {
  assert.equal(normalizePath('thoughts/./shared/reviews/../plans/x.md', PROJECT), 'c:/projects/envanex/thoughts/shared/plans/x.md');
  assert.equal(normalizePath('C:\\a\\b\\..\\..\\c', PROJECT), 'c:/c');
});

test('normalizePath drops .. above the root', () => {
  assert.equal(normalizePath('c:/../x', PROJECT), 'c:/x');
  assert.equal(normalizePath('/../../x', PROJECT), '/x');
});

test('normalizePath lowercases and strips a trailing slash', () => {
  assert.equal(normalizePath('C:\\Projects\\Envanex\\OBJ\\', PROJECT), 'c:/projects/envanex/obj');
  assert.equal(normalizePath('C:\\', PROJECT), 'c:/');
});

test('normalizeProjectDir gives the same result for C:\\, C:/ and /c/ forms', () => {
  const expected = 'c:/projects/envanex';
  assert.equal(normalizeProjectDir('C:\\projects\\envanex'), expected);
  assert.equal(normalizeProjectDir('C:/projects/envanex'), expected);
  assert.equal(normalizeProjectDir('/c/projects/envanex'), expected);
  assert.equal(normalizeProjectDir('C:\\projects\\envanex\\'), expected);
});

test('normalizeProjectDir rejects a relative dir', () => {
  assert.throws(() => normalizeProjectDir('projects/envanex'), /not absolute/);
  assert.throws(() => normalizeProjectDir('.'), /not absolute/);
});

// Target extractors

const BASH_INPUT = { tool_name: 'Bash', tool_input: { command: 'git status' } };

test('getCommand returns tool_input.command for a Bash-shaped input', () => {
  assert.equal(getCommand(BASH_INPUT), 'git status');
});

test('getCommand throws on a missing or empty command', () => {
  assert.throws(() => getCommand({ tool_name: 'Bash', tool_input: {} }), /command is missing or empty/);
  assert.throws(() => getCommand({ tool_name: 'Bash', tool_input: { command: '' } }), /command is missing or empty/);
});

test('getTargetPath throws on a Bash-shaped input', () => {
  assert.throws(() => getTargetPath(BASH_INPUT), /target path is missing or empty/);
});

// Redaction

test('redacts Password in a connection string', () => {
  const line = logAndRead(
    "export ENVANEX_CONNECTION_STRING='Server=x;Password=S3cretValue1;TrustServerCertificate=True'",
  );
  assert.ok(!line.includes('S3cretValue1'), line);
  assert.ok(line.includes('Password=***'), line);
});

test('redacts every Password occurrence', () => {
  const line = logAndRead('a Password=S3cretValue10; b Password=S3cretValue11');
  assert.ok(!line.includes('S3cretValue10'), line);
  assert.ok(!line.includes('S3cretValue11'), line);
});

test('redacts a quoted MSSQL_SA_PASSWORD assignment', () => {
  const line = logAndRead("export MSSQL_SA_PASSWORD='S3cretValue4'");
  assert.ok(!line.includes('S3cretValue4'), line);
  assert.ok(line.includes('MSSQL_SA_PASSWORD=***'), line);
});

test('redacts a double-quoted SA_PASSWORD assignment', () => {
  const line = logAndRead('SA_PASSWORD="S3cretValue8" docker compose up -d');
  assert.ok(!line.includes('S3cretValue8'), line);
  assert.ok(line.includes('docker compose up -d'), line);
});

test('redacts the sqlcmd -P value', () => {
  const line = logAndRead(
    'docker exec envanex-sql /opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P "S3cretValue7" -C -Q "SELECT 1"',
  );
  assert.ok(!line.includes('S3cretValue7'), line);
  assert.ok(line.includes('-U sa'), line);
});

test('redacts the user-secrets set value', () => {
  const line = logAndRead('dotnet user-secrets set "Jwt:SigningKey" "S3cretValue3" --project src/Envanex.Web');
  assert.ok(!line.includes('S3cretValue3'), line);
});

test('redacts user-secrets set with a --verbose flag before the key', () => {
  const line = logAndRead('dotnet user-secrets set --verbose "Jwt:SigningKey" "S3cretValue5"');
  assert.ok(!line.includes('S3cretValue5'), line);
});

test('redacts user-secrets set when options precede set', () => {
  const line = logAndRead('dotnet user-secrets --project src/Envanex.Web set "Jwt:SigningKey" "S3cretValue6"');
  assert.ok(!line.includes('S3cretValue6'), line);
});

test('redacts user-secrets set only up to the next &&', () => {
  const line = logAndRead('dotnet user-secrets set k S3cretValue9 && dotnet build');
  assert.ok(!line.includes('S3cretValue9'), line);
  assert.ok(line.includes('&& dotnet build'), line);
});

test('redacts a token or api key assignment', () => {
  const tokenLine = logAndRead('GH_TOKEN=abc123');
  assert.ok(!tokenLine.includes('abc123'), tokenLine);
  fs.rmSync(path.join(process.env.ENVANEX_HOOK_LOG_DIR, 'hooks.jsonl'));
  const keyLine = logAndRead('api_key: abc123');
  assert.ok(!keyLine.includes('abc123'), keyLine);
});

test('leaves a command without secrets unchanged', () => {
  const line = logAndRead('dotnet test --filter Category=Unit');
  assert.equal(JSON.parse(line).target, 'dotnet test --filter Category=Unit');
});

test('redacts before truncating to 300 characters', () => {
  // The quoted value starts at character 290 and its closing quote lies past 300,
  // so truncating first would leave an unterminated quote that no rule redacts.
  const prefix = 'x'.repeat(281) + 'Password=';
  assert.equal(prefix.length, 290);
  const target = `${prefix}'S3cretValue12 continues past the limit'`;
  const line = logAndRead(target);
  assert.ok(!line.includes('S3cretVal'), line);
  assert.ok(JSON.parse(line).target.length <= 300);
});

// Log

test('does not write to the project hook log when the override is set', () => {
  const projectA = makeTempDir('envanex-project-');
  const overrideB = makeTempDir('envanex-override-');
  process.env.ENVANEX_HOOK_LOG_DIR = overrideB;
  logDecision(ctxFor(projectA), {
    hook: 'guard-common-test',
    decision: 'allow',
    reason: '',
    tool_name: 'Write',
    target: 'guard-test.txt',
  });
  assert.equal(fs.existsSync(path.join(projectA, 'TestResults', 'hook-log', 'hooks.jsonl')), false);
  const content = fs.readFileSync(path.join(overrideB, 'hooks.jsonl'), 'utf8');
  assert.ok(content.includes('guard-test.txt'), content);
});

test('falls back to <projectDir>/TestResults/hook-log without the override', () => {
  const projectA = makeTempDir('envanex-project-');
  process.env.ENVANEX_HOOK_LOG_DIR = '';
  logDecision(ctxFor(projectA), {
    hook: 'guard-common-test',
    decision: 'allow',
    reason: '',
    tool_name: 'Write',
    target: 'guard-test.txt',
  });
  const content = fs.readFileSync(path.join(projectA, 'TestResults', 'hook-log', 'hooks.jsonl'), 'utf8');
  assert.ok(content.includes('guard-test.txt'), content);
});

test('logDecision swallows its own errors', () => {
  const dir = makeTempDir('envanex-file-');
  const file = path.join(dir, 'not-a-dir');
  fs.writeFileSync(file, 'x');
  process.env.ENVANEX_HOOK_LOG_DIR = file;
  assert.doesNotThrow(() =>
    logDecision(ctxFor(PROJECT), {
      hook: 'guard-common-test',
      decision: 'block',
      reason: 'test',
      tool_name: 'Write',
      target: 'x',
    }),
  );
});
