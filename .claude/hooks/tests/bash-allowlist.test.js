'use strict';

const { test, after } = require('node:test');
const assert = require('node:assert/strict');
const fs = require('fs');
const path = require('path');

const { runHook: spawnHook } = require('./run-hook');

const SCRIPT = path.join(__dirname, '..', 'bash-allowlist.js');
const DEFAULT_PROJECT_DIR = 'C:\\projects\\envanex';

const logDirs = [];

function runHook(...args) {
  const result = spawnHook(...args);
  logDirs.push(result.logDir);
  return result;
}

after(() => {
  for (const dir of logDirs) {
    if (dir === undefined) {
      continue;
    }
    fs.rmSync(dir, { recursive: true, force: true });
  }
});

function bashStdin(command) {
  return JSON.stringify({ tool_name: 'Bash', tool_input: { command }, cwd: DEFAULT_PROJECT_DIR });
}

function runBash(profileArgs, command) {
  return runHook(SCRIPT, profileArgs, bashStdin(command), { CLAUDE_PROJECT_DIR: DEFAULT_PROJECT_DIR });
}

function readLogEntries(logDir) {
  return fs
    .readFileSync(path.join(logDir, 'hooks.jsonl'), 'utf8')
    .trim()
    .split('\n')
    .map((line) => JSON.parse(line));
}

function assertBlocked(result, hook, reason) {
  assert.equal(result.status, 2, `expected a block, got ${result.status}; stderr: ${result.stderr}`);
  assert.ok(result.stderr.includes(`Blocked by ${hook}:`), result.stderr);
  assert.ok(result.stderr.includes(reason), `expected reason "${reason}" in stderr: ${result.stderr}`);
}

// An allow also proves that runGuard read the target through getCommand: the default
// path extractor throws on a Bash-shaped input, which would block.
function assertAllowed(profile, command) {
  const result = runBash([profile], command);
  assert.equal(result.status, 0, `expected an allow, got ${result.status}; stderr: ${result.stderr}`);
  const entries = readLogEntries(result.logDir);
  assert.equal(entries.length, 1);
  assert.equal(entries[0].decision, 'allow');
  assert.equal(entries[0].hook, `bash-allowlist:${profile}`);
  assert.equal(entries[0].target, command);
}

function assertTesterBlocks(command, reason) {
  assertBlocked(runBash(['tester'], command), 'bash-allowlist:tester', reason);
}

function assertExplainerBlocks(command, reason) {
  assertBlocked(runBash(['explainer'], command), 'bash-allowlist:explainer', reason);
}

const NOT_IN_TESTER = 'command is not in the tester allowlist';
const NOT_IN_EXPLAINER = 'command is not in the explainer allowlist';
const REDIRECTION = 'redirection with ">" is not allowed';
const SEMICOLON = 'chaining with ";" is not allowed';

function dotnetToken(token) {
  return `dotnet token "${token}" is not allowed`;
}

// tester profile: allows

test('bash-allowlist: allows bash .claude/skills/gate/scripts/gate.sh', () => {
  assertAllowed('tester', 'bash .claude/skills/gate/scripts/gate.sh');
});

test('bash-allowlist: allows git diff --name-only main...HEAD', () => {
  assertAllowed('tester', 'git diff --name-only main...HEAD');
});

test('bash-allowlist: allows grep -rn "Fact" tests/Envanex.IntegrationTests', () => {
  assertAllowed('tester', 'grep -rn "Fact" tests/Envanex.IntegrationTests');
});

test('bash-allowlist: allows dotnet test 2>&1 | tail -20', () => {
  assertAllowed('tester', 'dotnet test 2>&1 | tail -20');
});

test('bash-allowlist: allows cat TestResults/x/gates.txt', () => {
  assertAllowed('tester', 'cat TestResults/x/gates.txt');
});

test('bash-allowlist: allows docker compose ps', () => {
  assertAllowed('tester', 'docker compose ps');
});

test('bash-allowlist: allows dotnet format --verify-no-changes', () => {
  assertAllowed('tester', 'dotnet format --verify-no-changes');
});

test('bash-allowlist: allows node --test .claude/hooks/tests/path-guard.test.js', () => {
  assertAllowed('tester', 'node --test .claude/hooks/tests/path-guard.test.js');
});

test('bash-allowlist: allows node --test .claude/hooks/tests/*.test.js', () => {
  assertAllowed('tester', 'node --test .claude/hooks/tests/*.test.js');
});

test('bash-allowlist: allows dotnet build -warnaserror', () => {
  assertAllowed('tester', 'dotnet build -warnaserror');
});

test('bash-allowlist: allows dotnet test --no-restore --filter Category=Unit tests/Envanex.Domain.Tests', () => {
  assertAllowed('tester', 'dotnet test --no-restore --filter Category=Unit tests/Envanex.Domain.Tests');
});

test('bash-allowlist: allows dotnet test -v minimal', () => {
  assertAllowed('tester', 'dotnet test -v minimal');
});

// tester profile: blocks

test('bash-allowlist: blocks echo x > src/a.cs', () => {
  assertTesterBlocks('echo x > src/a.cs', REDIRECTION);
});

test('bash-allowlist: blocks cat a >> b', () => {
  assertTesterBlocks('cat a >> b', REDIRECTION);
});

test('bash-allowlist: blocks tee x', () => {
  assertTesterBlocks('tee x', NOT_IN_TESTER);
});

test('bash-allowlist: blocks sed -i s/a/b/ x', () => {
  assertTesterBlocks('sed -i s/a/b/ x', NOT_IN_TESTER);
});

test('bash-allowlist: blocks dotnet format without --verify-no-changes', () => {
  assertTesterBlocks('dotnet format', NOT_IN_TESTER);
});

test('bash-allowlist: blocks git commit -m x', () => {
  assertTesterBlocks('git commit -m x', NOT_IN_TESTER);
});

test('bash-allowlist: blocks git stash', () => {
  assertTesterBlocks('git stash', NOT_IN_TESTER);
});

test('bash-allowlist: blocks rm -rf obj', () => {
  assertTesterBlocks('rm -rf obj', NOT_IN_TESTER);
});

test('bash-allowlist: blocks cp a b', () => {
  assertTesterBlocks('cp a b', NOT_IN_TESTER);
});

test('bash-allowlist: blocks mv a b', () => {
  assertTesterBlocks('mv a b', NOT_IN_TESTER);
});

test('bash-allowlist: blocks docker compose down', () => {
  assertTesterBlocks('docker compose down', NOT_IN_TESTER);
});

test('bash-allowlist: blocks dotnet test; rm x', () => {
  assertTesterBlocks('dotnet test; rm x', SEMICOLON);
});

test('bash-allowlist: blocks dotnet test && git add .', () => {
  assertTesterBlocks('dotnet test && git add .', 'chaining with "&&" is not allowed');
});

test('bash-allowlist: blocks bash .claude/skills/gate/scripts/gate.sh; rm x', () => {
  assertTesterBlocks('bash .claude/skills/gate/scripts/gate.sh; rm x', SEMICOLON);
});

test('bash-allowlist: blocks echo `id`', () => {
  assertTesterBlocks('echo `id`', 'command substitution with a backtick is not allowed');
});

test('bash-allowlist: blocks echo $(id)', () => {
  assertTesterBlocks('echo $(id)', 'command substitution with "$(" is not allowed');
});

test('bash-allowlist: blocks bash -c "ls"', () => {
  assertTesterBlocks('bash -c "ls"', NOT_IN_TESTER);
});

test('bash-allowlist: blocks node -e "1"', () => {
  assertTesterBlocks('node -e "1"', NOT_IN_TESTER);
});

test('bash-allowlist: blocks dotnet test | tee out.txt', () => {
  assertTesterBlocks('dotnet test | tee out.txt', 'piping into "tee" is not allowed');
});

test('bash-allowlist: blocks node --test with .. traversal', () => {
  assertTesterBlocks(
    'node --test .claude/hooks/tests/../../../TestResults/x.test.js',
    'node --test argument ".claude/hooks/tests/../../../TestResults/x.test.js" is not a test file under .claude/hooks/tests',
  );
});

test('bash-allowlist: blocks node --test on a file outside .claude/hooks/tests', () => {
  assertTesterBlocks(
    'node --test TestResults/x.test.js',
    'node --test argument "TestResults/x.test.js" is not a test file under .claude/hooks/tests',
  );
});

test('bash-allowlist: blocks git diff --output=src/a.cs', () => {
  assertTesterBlocks('git diff --output=src/a.cs', 'option "--output=src/a.cs" is not allowed');
});

test('bash-allowlist: blocks git log --output x', () => {
  assertTesterBlocks('git log --output x', 'option "--output" is not allowed');
});

test('bash-allowlist: blocks git difftool', () => {
  assertTesterBlocks('git difftool', NOT_IN_TESTER);
});

test('bash-allowlist: blocks bash .claude/skills/gate/scripts/gate.sh --extra', () => {
  assertTesterBlocks(
    'bash .claude/skills/gate/scripts/gate.sh --extra',
    'exact entry "bash .claude/skills/gate/scripts/gate.sh" takes no further tokens',
  );
});

test('bash-allowlist: blocks grep -E "Passed!|Failed!" (documented quote-unaware split)', () => {
  assertTesterBlocks('grep -E "Passed!|Failed!"', 'piping into "Failed!"" is not allowed');
});

// tester profile: dotnet fixed-token entries

test('bash-allowlist: blocks dotnet build -p:PreBuildEvent=x', () => {
  assertTesterBlocks('dotnet build -p:PreBuildEvent=x', dotnetToken('-p:PreBuildEvent=x'));
});

test('bash-allowlist: blocks dotnet test --results-directory src', () => {
  assertTesterBlocks('dotnet test --results-directory src', dotnetToken('--results-directory'));
});

test('bash-allowlist: blocks dotnet format --verify-no-changes --report x', () => {
  assertTesterBlocks('dotnet format --verify-no-changes --report x', dotnetToken('--report'));
});

test('bash-allowlist: blocks dotnet build -o src/x', () => {
  assertTesterBlocks('dotnet build -o src/x', dotnetToken('-o'));
});

test('bash-allowlist: blocks dotnet build -bl', () => {
  assertTesterBlocks('dotnet build -bl', dotnetToken('-bl'));
});

test('bash-allowlist: blocks dotnet test --no-build', () => {
  assertTesterBlocks('dotnet test --no-build', dotnetToken('--no-build'));
});

test('bash-allowlist: blocks dotnet test --filter -p:PreBuildEvent=x', () => {
  assertTesterBlocks(
    'dotnet test --filter -p:PreBuildEvent=x',
    '--filter needs exactly one argument that does not start with - or /',
  );
});

test('bash-allowlist: blocks dotnet test --filter /p:PreBuildEvent=x', () => {
  assertTesterBlocks(
    'dotnet test --filter /p:PreBuildEvent=x',
    '--filter needs exactly one argument that does not start with - or /',
  );
});

test('bash-allowlist: blocks dotnet build src/../../x.csproj', () => {
  assertTesterBlocks('dotnet build src/../../x.csproj', 'dotnet project path "src/../../x.csproj" has a .. segment');
});

// explainer profile

test('bash-allowlist: allows git diff main...HEAD --stat for the explainer', () => {
  assertAllowed('explainer', 'git diff main...HEAD --stat');
});

test('bash-allowlist: allows git log --oneline for the explainer', () => {
  assertAllowed('explainer', 'git log --oneline');
});

test('bash-allowlist: allows dotnet list package for the explainer', () => {
  assertAllowed('explainer', 'dotnet list package');
});

test('bash-allowlist: blocks dotnet build for the explainer', () => {
  assertExplainerBlocks('dotnet build', NOT_IN_EXPLAINER);
});

test('bash-allowlist: blocks echo x > docs/journal/x.md for the explainer', () => {
  assertExplainerBlocks('echo x > docs/journal/x.md', REDIRECTION);
});

test('bash-allowlist: blocks git add . for the explainer', () => {
  assertExplainerBlocks('git add .', NOT_IN_EXPLAINER);
});

// General

test('bash-allowlist: blocks an unknown profile', () => {
  assertBlocked(runBash(['nobody'], 'git status'), 'bash-allowlist:nobody', 'unknown profile "nobody"');
});

test('bash-allowlist: blocks a missing profile', () => {
  assertBlocked(runBash([], 'git status'), 'bash-allowlist:', 'missing profile argument');
});

test('bash-allowlist: blocks on malformed JSON', () => {
  const result = runHook(SCRIPT, ['tester'], '{not json', { CLAUDE_PROJECT_DIR: DEFAULT_PROJECT_DIR });
  assertBlocked(result, 'bash-allowlist:tester', 'guard error');
  assert.ok(result.stderr.includes('invalid hook input'), result.stderr);
});
