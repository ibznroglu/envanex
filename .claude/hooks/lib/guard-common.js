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

function redactSecrets(s) {
  if (typeof s !== 'string') {
    return s;
  }
  let out = s;

  // Rule 1: password assignments, quoted or unquoted value.
  out = out.replace(/(password|pwd)(\s*=\s*)(?:'[^']*'|"[^"]*"|[^;'"\s]+)/gi, '$1$2***');

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

  return out;
}

function resolveLogDir(projectDir) {
  const override = process.env.ENVANEX_HOOK_LOG_DIR;
  if (typeof override === 'string' && override !== '') {
    return override;
  }
  if (typeof projectDir !== 'string' || projectDir === '') {
    throw new Error('no project dir for the hook log');
  }
  return `${projectDir}/TestResults/hook-log`;
}

function logDecision(ctx, entry) {
  try {
    const dir = resolveLogDir(ctx && ctx.projectDir);
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
    decide(ctx.input, ctx);
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
