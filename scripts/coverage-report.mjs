#!/usr/bin/env node
// 覆盖率报告：解析 dotnet test 生成的 cobertura XML，输出总行覆盖率、按模块明细，
// 以及「未覆盖行数最多」的文件榜，用来决定下一批补测目标。
// 用法：node scripts/coverage-report.mjs [coverage.cobertura.xml 路径] [--gate 45] [--all]
//
// 门禁口径：只对「手写代码」判定，分母剔除 EF 生成的 Migrations（占全部可执行行的 59%），
// 理由见 docs/测试规范.md。
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, dirname, resolve, basename } from 'node:path';
import { fileURLToPath } from 'node:url';

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '..');

// 参数解析：--gate <百分比>、--all；第一个非选项参数是 XML 路径。
const argv = process.argv.slice(2);
let gate = null;
let reportPath = null;
let listAll = false;
for (let i = 0; i < argv.length; i += 1) {
  const arg = argv[i];
  if (arg === '--gate') {
    gate = Number(argv[++i]);
    if (!Number.isFinite(gate)) {
      console.error('--gate 后面要跟百分比数字');
      process.exit(2);
    }
  } else if (arg === '--all') {
    listAll = true;
  } else if (!arg.startsWith('--') && reportPath === null) {
    reportPath = arg;
  }
}

function findReport() {
  if (reportPath) return resolve(repoRoot, reportPath);
  const dir = join(repoRoot, '.coverage');
  const runs = readdirSync(dir)
    .map((d) => join(dir, d))
    .filter((p) => statSync(p).isDirectory())
    .flatMap((p) => readdirSync(p).filter((f) => f.endsWith('coverage.cobertura.xml')).map((f) => join(p, f)))
    .sort((a, b) => statSync(b).mtimeMs - statSync(a).mtimeMs);
  if (runs.length === 0) {
    console.error('.coverage 下没有 coverage.cobertura.xml，先跑 dotnet test --collect:"XPlat Code Coverage"');
    process.exit(1);
  }
  return runs[0];
}

const xml = readFileSync(findReport(), 'utf8');

// 按 (文件, 行号) 去重：同一行会被多个方法各报一次，取命中数的最大值。
const lines = new Map();
const classRe = /<class\s+name="([^"]*)"(?:\s+filename="([^"]*)")?[^>]*>([\s\S]*?)<\/class>/g;
const lineRe = /<line\s+number="(\d+)"\s+hits="(\d+)"/g;
let cm;
while ((cm = classRe.exec(xml))) {
  const [, className, filename, body] = cm;
  const file = filename ? filename.replace(/\\/g, '/') : className;
  let lm;
  lineRe.lastIndex = 0;
  while ((lm = lineRe.exec(body))) {
    const key = `${file}#${lm[1]}`;
    lines.set(key, Math.max(lines.get(key) ?? 0, Number(lm[2])));
  }
}

const perFile = new Map();
for (const [key, hits] of lines) {
  const file = key.slice(0, key.lastIndexOf('#'));
  const bucket = perFile.get(file) ?? { covered: 0, total: 0 };
  bucket.total += 1;
  if (hits > 0) bucket.covered += 1;
  perFile.set(file, bucket);
}

const pct = (c, t) => (t === 0 ? '   0.0%' : `${(((c / t) * 100).toFixed(1) + '%').padStart(6)}`);
const rows = [...perFile].map(([file, v]) => ({ file, ...v }));
const total = rows.reduce((s, r) => s + r.total, 0);
const covered = rows.reduce((s, r) => s + r.covered, 0);

const groupOf = (file) => {
  if (/^Migrations\//i.test(file)) return 'Migrations（EF 生成代码）';
  const m = file.match(/^Modules\/([^/]+)\//i);
  if (m) return m[1];
  if (/^Shared\//i.test(file)) return 'Shared';
  return '其他（Program.cs 等根级）';
};

const groups = new Map();
for (const r of rows) {
  const g = groupOf(r.file);
  const bucket = groups.get(g) ?? { covered: 0, total: 0 };
  bucket.covered += r.covered;
  bucket.total += r.total;
  groups.set(g, bucket);
}

console.log(`总行覆盖率 ${pct(covered, total)}  (${covered}/${total} 可执行行)\n`);
console.log('=== 按模块 ===');
for (const [g, v] of [...groups].sort((a, b) => b[1].total - a[1].total)) {
  console.log(`${pct(v.covered, v.total)}  ${String(v.total).padStart(6)} 行  ${g}`);
}

// 门禁与汇报都用这个「手写代码」口径。
const mig = groups.get('Migrations（EF 生成代码）');
const hand = mig ? { covered: covered - mig.covered, total: total - mig.total } : { covered, total };
console.log(
  `\n手写代码（剔除 Migrations）：${pct(hand.covered, hand.total)}  (${hand.covered}/${hand.total} 行)`,
);

const miss = [...rows].sort((a, b) => b.total - b.covered - (a.total - a.covered));
console.log('\n=== 未覆盖行数 TOP 30（下一批补测目标）===');
for (const r of miss.slice(0, 30)) {
  console.log(`${pct(r.covered, r.total)}  缺 ${String(r.total - r.covered).padStart(4)} 行  ${r.file}`);
}
if (listAll) {
  console.log('\n=== 全部文件 ===');
  for (const r of miss) console.log(`${pct(r.covered, r.total)}  ${r.file}`);
}

if (gate !== null) {
  const actual = hand.total === 0 ? 0 : (hand.covered / hand.total) * 100;
  if (actual < gate) {
    console.error(`\n门禁未过：手写代码行覆盖 ${actual.toFixed(1)}% < 要求 ${gate}%`);
    process.exit(1);
  }
  console.log(`\n门禁通过：手写代码行覆盖 ${actual.toFixed(1)}% ≥ 要求 ${gate}%`);
}
