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
  resolveLogDir,
  logDecision,
} = require('../lib/guard-common');
const { runHook } = require('./run-hook');

const PROJECT = 'c:/projects/envanex';
const ASYNC_FIXTURE = path.join(__dirname, 'fixtures', 'async-decide-guard.js');

let tempDirs = [];
let savedOverride;
let savedProjectDir;

function makeTempDir(prefix) {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), prefix));
  tempDirs.push(dir);
  return dir;
}

function restoreEnv(name, saved) {
  if (saved === undefined) {
    delete process.env[name];
  } else {
    process.env[name] = saved;
  }
}

beforeEach(() => {
  savedOverride = process.env.ENVANEX_HOOK_LOG_DIR;
  savedProjectDir = process.env.CLAUDE_PROJECT_DIR;
  process.env.ENVANEX_HOOK_LOG_DIR = makeTempDir('envanex-hooklog-');
});

afterEach(() => {
  restoreEnv('ENVANEX_HOOK_LOG_DIR', savedOverride);
  restoreEnv('CLAUDE_PROJECT_DIR', savedProjectDir);
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

// runGuard with an async decide

function runAsyncFixture(mode) {
  const stdin = JSON.stringify({ tool_name: 'Write', tool_input: { file_path: 'docs/x.md' } });
  const result = runHook(ASYNC_FIXTURE, [mode], stdin, { CLAUDE_PROJECT_DIR: 'C:\\projects\\envanex' });
  tempDirs.push(result.logDir);
  return result;
}

test('runGuard blocks when an async decide rejects', () => {
  const result = runAsyncFixture('reject');
  assert.equal(result.status, 2, result.stderr);
  assert.ok(result.stderr.includes('guard error: async decide rejected'), result.stderr);
});

test('runGuard waits for an async decide that blocks', () => {
  const result = runAsyncFixture('block');
  assert.equal(result.status, 2, result.stderr);
  assert.ok(result.stderr.includes('async decide blocked'), result.stderr);
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

function logAndReadTarget(target) {
  return JSON.parse(logAndRead(target)).target;
}

test('joins a backslash line continuation before redacting', () => {
  const target = logAndReadTarget('dotnet user-secrets set k \\\nS3cretValue13');
  assert.ok(!target.includes('S3cretValue13'), target);
});

test('joins a PowerShell backtick continuation before redacting', () => {
  const target = logAndReadTarget('dotnet user-secrets set k `\r\nS3cretValue14');
  assert.ok(!target.includes('S3cretValue14'), target);
});

test('redacts an unterminated quoted Password value', () => {
  const target = logAndReadTarget("Password='S3cretValue15 and the rest");
  assert.ok(!target.includes('S3cretValue15'), target);
});

test('redacts a Password value with a mid-value quote', () => {
  const target = logAndReadTarget("Password=ab'S3cretValue16");
  assert.ok(!target.includes('S3cretValue16'), target);
});

test('redacts a Password value with an escaped double quote', () => {
  const target = logAndReadTarget('Password="ab\\" S3cretValue17"');
  assert.ok(!target.includes('S3cretValue17'), target);
});

test('redacts AWS_SECRET_ACCESS_KEY', () => {
  const target = logAndReadTarget('AWS_SECRET_ACCESS_KEY=S3cretValue18 aws s3 ls');
  assert.ok(!target.includes('S3cretValue18'), target);
  assert.ok(target.includes('aws s3 ls'), target);
});

test('redacts a Jwt__SigningKey assignment', () => {
  const target = logAndReadTarget("export Jwt__SigningKey='S3cretValue19'");
  assert.ok(!target.includes('S3cretValue19'), target);
});

test('redacts a --Jwt:SigningKey= argument', () => {
  const target = logAndReadTarget('dotnet run --project src/Envanex.Web --Jwt:SigningKey=S3cretValue20');
  assert.ok(!target.includes('S3cretValue20'), target);
  assert.ok(target.includes('--project src/Envanex.Web'), target);
});

test('redacts a Jwt:SigningKey= assignment', () => {
  const target = logAndReadTarget('Jwt:SigningKey=S3cretValue21');
  assert.ok(!target.includes('S3cretValue21'), target);
});

test('redacts a JSON Password member', () => {
  const target = logAndReadTarget('{"Password": "S3cretValue22"}');
  assert.ok(!target.includes('S3cretValue22'), target);
  assert.ok(target.includes('"Password": "***"'), target);
});

test('redacts a JSON SigningKey member', () => {
  const target = logAndReadTarget('{"Jwt": {"SigningKey": "S3cretValue23"}}');
  assert.ok(!target.includes('S3cretValue23'), target);
});

test('redacts a space-separated --password value', () => {
  const target = logAndReadTarget('tool --password S3cretValue24 --verbose');
  assert.ok(!target.includes('S3cretValue24'), target);
  assert.ok(target.includes('--verbose'), target);
});

test('redacts an Authorization Bearer header', () => {
  const target = logAndReadTarget('curl -H "Authorization: Bearer S3cretValue25" https://example.test/x');
  assert.ok(!target.includes('S3cretValue25'), target);
  assert.ok(target.includes('https://example.test/x'), target);
});

test('redacts URL userinfo', () => {
  const target = logAndReadTarget('git clone https://user:S3cretValue26@example.test/x.git');
  assert.ok(!target.includes('S3cretValue26'), target);
  assert.ok(target.includes('@example.test/x.git'), target);
});

test('leaves a URL without userinfo unchanged', () => {
  assert.equal(logAndReadTarget('git clone https://example.test/x.git'), 'git clone https://example.test/x.git');
});

test('leaves git show HEAD:path unchanged', () => {
  assert.equal(
    logAndReadTarget('git show HEAD:src/Envanex.Web/Program.cs'),
    'git show HEAD:src/Envanex.Web/Program.cs',
  );
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

test("resolveLogDir keeps the raw project dir's case in slash form", () => {
  process.env.ENVANEX_HOOK_LOG_DIR = '';
  assert.equal(resolveLogDir('C:\\Projects\\Envanex'), 'C:/Projects/Envanex/TestResults/hook-log');
  assert.equal(resolveLogDir('/c/Projects/Envanex'), 'c:/Projects/Envanex/TestResults/hook-log');
});

test('resolveLogDir falls back to CLAUDE_PROJECT_DIR when no project dir is given', () => {
  process.env.ENVANEX_HOOK_LOG_DIR = '';
  process.env.CLAUDE_PROJECT_DIR = 'C:\\Temp\\Envanex-X';
  assert.equal(resolveLogDir(undefined), 'C:/Temp/Envanex-X/TestResults/hook-log');
});

test('resolveLogDir throws on a relative project dir', () => {
  process.env.ENVANEX_HOOK_LOG_DIR = '';
  delete process.env.CLAUDE_PROJECT_DIR;
  assert.throws(() => resolveLogDir('projects/envanex'), /not absolute/);
});

test('logDecision builds the fallback log dir from projectDirRaw', () => {
  const projectA = makeTempDir('envanex-project-');
  const projectB = makeTempDir('envanex-project-').replace(/\\/g, '/');
  process.env.ENVANEX_HOOK_LOG_DIR = '';
  logDecision(
    { hook: 'guard-common-test', projectDirRaw: projectA, projectDir: projectB },
    {
      hook: 'guard-common-test',
      decision: 'allow',
      reason: '',
      tool_name: 'Write',
      target: 'guard-test.txt',
    },
  );
  const content = fs.readFileSync(path.join(projectA, 'TestResults', 'hook-log', 'hooks.jsonl'), 'utf8');
  assert.ok(content.includes('guard-test.txt'), content);
  assert.equal(fs.existsSync(path.join(projectB, 'TestResults')), false);
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
