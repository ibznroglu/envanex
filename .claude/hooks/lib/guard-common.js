'use strict';

// Shared helpers for the PreToolUse guard hooks.
// CommonJS, Node built-ins only. Every guard fails closed: any error becomes a block.

const fs = require('fs');

const TARGET_LOG_LIMIT = 300;

function readHookInput() {
  return new Promise((resolve, reject) => {
    let data = '';
    process.stdin.setEncoding('utf8');
    process.stdin.on('data', (chunk) => {
      data += chunk;
    });
    process.stdin.on('error', reject);
    process.stdin.on('end', () => {
      if (data.trim() === '') {
        reject(new Error('empty hook input'));
        return;
      }
      try {
        resolve(JSON.parse(data));
      } catch (err) {
        reject(new Error(`invalid hook input: ${err.message}`));
      }
    });
  });
}

function getProjectDir(input) {
  const fromEnv = process.env.CLAUDE_PROJECT_DIR;
  if (typeof fromEnv === 'string' && fromEnv !== '') {
    return fromEnv;
  }
  if (input && typeof input.cwd === 'string' && input.cwd !== '') {
    return input.cwd;
  }
  throw new Error('no project dir: CLAUDE_PROJECT_DIR is unset and the input has no cwd');
}

// Steps 1 and 2 of the algorithm: slashes and the git-bash drive form.
function toSlashForm(rawPath) {
  return rawPath.replace(/\\/g, '/').replace(/^\/([A-Za-z])(\/|$)/, '$1:/');
}

function isAbsoluteSlashForm(p) {
  return /^[A-Za-z]:\//.test(p) || p.startsWith('/');
}

// Steps 4 to 6 of the algorithm, on an absolute slash-form path.
function collapseAbsolute(p) {
  let root;
  let rest;
  const drive = /^([A-Za-z]:)\//.exec(p);
  if (drive) {
    root = drive[1];
    rest = p.slice(drive[0].length);
  } else {
    root = '';
    rest = p.slice(1);
  }
  const stack = [];
  for (const segment of rest.split('/')) {
    if (segment === '' || segment === '.') {
      continue;
    }
    if (segment === '..') {
      stack.pop();
      continue;
    }
    stack.push(segment);
  }
  return `${root}/${stack.join('/')}`.toLowerCase();
}

function normalizeProjectDir(raw) {
  if (typeof raw !== 'string' || raw === '') {
    throw new Error('project dir is empty');
  }
  const p = toSlashForm(raw);
  if (!isAbsoluteSlashForm(p)) {
    throw new Error(`project dir is not absolute: ${raw}`);
  }
  return collapseAbsolute(p);
}

function normalizePath(rawPath, normalizedProjectDir) {
  if (typeof rawPath !== 'string' || rawPath === '') {
    throw new Error('path is empty');
  }
  let p = toSlashForm(rawPath);
  if (!isAbsoluteSlashForm(p)) {
    p = `${normalizedProjectDir}/${p}`;
  }
  return collapseAbsolute(p);
}

function getTargetPath(input) {
  const toolInput = input && input.tool_input;
  if (!toolInput || typeof toolInput !== 'object') {
    throw new Error('tool_input is missing');
  }
  const target = toolInput.file_path ?? toolInput.path ?? toolInput.notebook_path;
  if (typeof target !== 'string' || target === '') {
    throw new Error('target path is missing or empty');
  }
  return target;
}

function getCommand(input) {
  const toolInput = input && input.tool_input;
  if (!toolInput || typeof toolInput !== 'object') {
    throw new Error('tool_input is missing');
  }
  const command = toolInput.command;
  if (typeof command !== 'string' || command === '') {
    throw new Error('command is missing or empty');
  }
  return command;
}

// Key words that mark a value as secret (rules 5, 6 and 7).
const SENSITIVE =
  'password|passwd|pwd|secret|token|api[_-]?key|signing[_-]?key|access[_-]?key|private[_-]?key|credential';

// One or more of: a single-quoted run (closed or unterminated), a double-quoted run with
// backslash escapes (closed or unterminated), or one unquoted character other than `;`,
// whitespace or a quote. The alternatives start with different characters, so backtracking
// stays linear. A quote right after a value extends the redaction (errs toward redacting more).
const VALUE = String.raw`(?:'[^']*(?:'|$)|"(?:[^"\\]|\\.)*(?:"|$)|[^;\s'"])+`;

const LINE_CONTINUATION = /[\\`]\r?\n/g;
const PASSWORD_ASSIGNMENT = new RegExp(String.raw`(password|pwd)(\s*=\s*)${VALUE}`, 'gi');
const SENSITIVE_KEY_ASSIGNMENT = new RegExp(
  String.raw`([A-Za-z0-9_.:-]*(?:${SENSITIVE})[A-Za-z0-9_.:-]*)(\s*[=:]\s*)${VALUE}`,
  'gi',
);
const SENSITIVE_JSON_MEMBER = new RegExp(
  String.raw`("[^"]*(?:${SENSITIVE})[^"]*"\s*:\s*)(?:"(?:[^"\\]|\\.)*"|[^,}\s]+)`,
  'gi',
);
const SENSITIVE_OPTION = new RegExp(
  String.raw`((?:^|\s)--?[A-Za-z0-9_-]*(?:${SENSITIVE})[A-Za-z0-9_-]*)(\s+)(?:'[^']*(?:'|$)|"(?:[^"\\]|\\.)*(?:"|$)|\S+)`,
  'gi',
);
const AUTHORIZATION_HEADER = /(authorization\s*[:=]\s*(?:(?:bearer|basic|token|digest)\s+)?)[^\s'"]+/gi;
const URL_USERINFO = /([a-z][a-z0-9+.-]*:\/\/[^\s/@:'"]*:)[^\s/@'"]+@/gi;

function redactSecrets(s) {
  if (typeof s !== 'string') {
    return s;
  }
  let out = s;

  // Rule 0: join line continuations (bash `\`, PowerShell backtick) before any other rule.
  out = out.replace(LINE_CONTINUATION, ' ');

  // Rule 1: password assignments, quoted, unterminated or unquoted value.
  out = out.replace(PASSWORD_ASSIGNMENT, '$1$2***');

  // Rule 2: sqlcmd -P. The -P itself is case-sensitive; sqlcmd's -p is another option.
  if (/sqlcmd/i.test(out)) {
    out = out.replace(/(\s-P\s*)(?:'[^']*'|"[^"]*"|\S+)/g, '$1***');
  }

  // Rule 3: user-secrets. Everything after the first `set` token, up to &&, ;, |, newline or end.
  if (/user-secrets/i.test(out)) {
    out = out.replace(
      /(user-secrets(?:(?!&&)[^;|\n])*?\sset)(?=\s|$|&&|[;|\n])(?:(?!&&)[^;|\n])*?(\s*)(?=&&|[;|\n]|$)/gi,
      '$1 ***$2',
    );
  }

  // Rule 4: tokens and keys.
  out = out.replace(/(secret|token|api[_-]?key)(\s*[=:]\s*)\S+/gi, '$1$2***');

  // Rule 5: assignments to a key that contains a sensitive word (env vars, config keys, --key=).
  out = out.replace(SENSITIVE_KEY_ASSIGNMENT, '$1$2***');

  // Rule 6: JSON members whose name contains a sensitive word.
  out = out.replace(SENSITIVE_JSON_MEMBER, '$1"***"');

  // Rule 7: space-separated options whose name contains a sensitive word.
  out = out.replace(SENSITIVE_OPTION, '$1$2***');

  // Rule 8: Authorization headers, with or without a scheme.
  out = out.replace(AUTHORIZATION_HEADER, '$1***');

  // Rule 9: URL userinfo passwords.
  out = out.replace(URL_USERINFO, '$1***@');

  return out;
}

// The raw project dir (or CLAUDE_PROJECT_DIR) in slash form, keeping its case.
// Early guard errors have no ctx.projectDirRaw yet, so they still log under CLAUDE_PROJECT_DIR.
function resolveLogDir(projectDirRaw) {
  const override = process.env.ENVANEX_HOOK_LOG_DIR;
  if (typeof override === 'string' && override !== '') {
    return override;
  }
  const raw =
    typeof projectDirRaw === 'string' && projectDirRaw !== '' ? projectDirRaw : process.env.CLAUDE_PROJECT_DIR;
  if (typeof raw !== 'string' || raw === '') {
    throw new Error('no project dir for the hook log');
  }
  const base = toSlashForm(raw);
  if (!isAbsoluteSlashForm(base)) {
    throw new Error(`project dir for the hook log is not absolute: ${raw}`);
  }
  return `${base.replace(/\/$/, '')}/TestResults/hook-log`;
}

function logDecision(ctx, entry) {
  try {
    const dir = resolveLogDir(ctx && ctx.projectDirRaw);
    const target = redactSecrets(String(entry.target ?? '')).slice(0, TARGET_LOG_LIMIT);
    const line = {
      ts: new Date().toISOString(),
      hook: entry.hook,
      decision: entry.decision,
      reason: redactSecrets(String(entry.reason ?? '')),
      tool_name: entry.tool_name,
      target,
      agent_type: entry.agent_type,
      agent_id: entry.agent_id,
      session_id: entry.session_id,
      project_dir_raw: ctx && ctx.projectDirRaw,
    };
    fs.mkdirSync(dir, { recursive: true });
    fs.appendFileSync(`${dir}/hooks.jsonl`, `${JSON.stringify(line)}\n`);
  } catch {
    // Logging must never change a decision.
  }
}

function entryFor(ctx, decision, reason) {
  const input = (ctx && ctx.input) || {};
  return {
    hook: ctx.hook,
    decision,
    reason,
    tool_name: input.tool_name,
    target: ctx.target,
    agent_type: input.agent_type,
    agent_id: input.agent_id,
    session_id: input.session_id,
  };
}

function block(ctx, reason) {
  logDecision(ctx, entryFor(ctx, 'block', reason));
  const message = redactSecrets(`Blocked by ${ctx.hook}: ${reason} -> ${ctx.target ?? ''}\n`);
  try {
    fs.writeSync(2, message);
  } catch {
    // The exit code alone still blocks.
  }
  process.exit(2);
}

function allow(ctx) {
  logDecision(ctx, entryFor(ctx, 'allow', ''));
  process.exit(0);
}

function runGuard(hookName, decide, extractTarget = getTargetPath) {
  const ctx = {
    hook: hookName,
    projectDirRaw: undefined,
    projectDir: undefined,
    input: undefined,
    target: '',
  };
  const run = async () => {
    ctx.input = await readHookInput();
    ctx.projectDirRaw = getProjectDir(ctx.input);
    ctx.projectDir = normalizeProjectDir(ctx.projectDirRaw);
    ctx.target = extractTarget(ctx.input);
    // decide may be async: a rejection becomes a block, and an async block exits before allow.
    await decide(ctx.input, ctx);
    allow(ctx);
  };
  run().catch((err) => {
    const message = err && err.message ? err.message : String(err);
    block(ctx, `guard error: ${message}`);
  });
}

module.exports = {
  readHookInput,
  getProjectDir,
  normalizeProjectDir,
  normalizePath,
  getTargetPath,
  getCommand,
  redactSecrets,
  resolveLogDir,
  logDecision,
  block,
  allow,
  runGuard,
};
