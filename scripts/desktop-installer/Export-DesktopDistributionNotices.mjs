// Read existing approved build output. No build, download or installer command.
import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {gunzipSync} from 'node:zlib';
import {createHash} from 'node:crypto';

const options = Object.fromEntries(process.argv.slice(2).reduce((pairs, value, i, all) => {
  if (i % 2 === 0) pairs.push([value, all[i + 1]]);
  return pairs;
}, []));
const run = options['--approved-run'];
const out = options['--output'];
if (!run || !out) throw new Error('Use --approved-run <existing approved run> --output <empty documentation directory>');
const json = p => JSON.parse(fs.readFileSync(p, 'utf8').replace(/^\uFEFF/, ''));
const sha = bytes => createHash('sha256').update(bytes).digest('hex').toUpperCase();
const evidence = json(path.join(run, 'local-build-evidence.json'));
if (evidence.Result !== 'PASS') throw new Error('Expected approved successful build evidence');
if (fs.existsSync(out) && fs.readdirSync(out).length) throw new Error('Output must be empty; preserve earlier documentation');
fs.mkdirSync(out, {recursive: true});
const desktop = path.join(run, 'source/desktop');
const release = path.dirname(evidence.Executable.Path);
const config = json(path.join(desktop, '.dart_tool/package_config.json'));
const graph = json(path.join(desktop, '.dart_tool/package_graph.json'));
const versions = new Map(graph.packages.map(p => [p.name, p.version]));
function walk(dir) {
  return fs.readdirSync(dir, {withFileTypes: true}).flatMap(e => e.isDirectory() ? walk(path.join(dir, e.name)) : [path.join(dir, e.name)]);
}
const depfiles = walk(path.join(desktop, '.dart_tool/flutter_build')).filter(p => path.basename(p) === 'kernel_snapshot_program.d');
if (depfiles.length !== 1) throw new Error('Expected one approved compiler input depfile');
const inputs = fs.readFileSync(depfiles[0], 'utf8').replaceAll('\\\\', '\\').toLowerCase();
const native = new Map([
  ['jni', 'dartjni.dll'], ['file_selector_windows', 'file_selector_windows_plugin.dll'],
  ['flutter_secure_storage_windows', 'flutter_secure_storage_windows_plugin.dll'], ['url_launcher_windows', 'url_launcher_windows_plugin.dll'],
]);
const packages = [];
const originals = [];
for (const pkg of config.packages) {
  if (!pkg.rootUri.startsWith('file:')) continue;
  const root = fileURLToPath(pkg.rootUri).replace(/[\\/]$/, '');
  const count = inputs.split(root.toLowerCase() + '\\').length - 1;
  const asset = pkg.name === 'cupertino_icons';
  if (!count && !native.has(pkg.name) && !asset) continue;
  let files = fs.readdirSync(root, {withFileTypes: true}).filter(e => e.isFile() && /^(license|copying|notice)/i.test(e.name)).map(e => path.join(root, e.name)).sort();
  if (!files.length && pkg.name.startsWith('flutter')) files = ['C:/flutter/LICENSE'];
  const refs = files.map(file => {
    const bytes = fs.readFileSync(file);
    const label = `${pkg.name}/${path.basename(file)}`;
    originals.push({label, bytes});
    return {original: label, sha256: sha(bytes)};
  });
  packages.push({package: pkg.name, version: versions.get(pkg.name) ?? `SDK ${evidence.Flutter.frameworkVersion}`,
    compiler_input_occurrences: count, shipped_native_library: native.get(pkg.name) ?? null,
    shipped_asset: asset, original_license_files: refs});
  if (pkg.name === 'jni') {
    const bytes = fs.readFileSync(path.join(root, 'third_party/jni.h'));
    originals.push({label: 'jni/third_party/jni.h original attribution', bytes: bytes.subarray(0, bytes.indexOf('*/') + 2)});
  }
}
originals.push({label: 'Dart SDK/LICENSE', bytes: fs.readFileSync('C:/flutter/bin/cache/dart-sdk/LICENSE')});
const engine = fs.readFileSync('C:/flutter/bin/cache/pkg/sky_engine/LICENSE');
fs.writeFileSync(path.join(out, 'FLUTTER-ENGINE-LICENSE.txt'), engine);
const noticeBytes = gunzipSync(fs.readFileSync(path.join(release, 'data/flutter_assets/NOTICES.Z')));
// This file is a byte-for-byte decompression of the bundled original notice.
fs.writeFileSync(path.join(out, 'THIRD-PARTY-NOTICES.txt'), noticeBytes);
const parts = [Buffer.from(`Avelri Gestao Desktop 1.0.1+2 - original component license texts\nSourceCommit=${evidence.SourceCommit}\n\n`)];
for (const {label, bytes} of originals) parts.push(Buffer.from(`\n${'='.repeat(80)}\n${label}\n\n`), bytes);
fs.writeFileSync(path.join(out, 'COMPONENT-LICENSES.txt'), Buffer.concat(parts));
const blocks = noticeBytes.toString('utf8').split(/^-{80}\r?$/m);
const attributions = blocks.map((block, i) => ({block: i + 1, attribution: block.trim().split('\n\n', 1)[0], sha256: sha(Buffer.from(block))}));
const files = json(path.join(run, 'release-file-inventory.json'));
const inventory = {
  source_commit: evidence.SourceCommit, desktop_version: '1.0.1+2', status: 'DOCUMENTATION_FOR_REVIEW_NOT_COMMERCIAL_RELEASE',
  scope: 'All approved Release files, native/asset dependencies, compiler-input packages and all bundled notice blocks. Compiler input does not assert retention of every source function after tree shaking. Build/dev-only resolved packages excluded from compiler package inventory.',
  approved_executable_sha256: evidence.Executable.SHA256, files, compiler_and_native_packages: packages,
  flutter_engine_revision: evidence.Flutter.engineRevision, embedded_engine_fonts_icu_and_other_attributions: attributions,
  external_system_requirements: ['Windows x64-compatible', 'Microsoft Visual C++ x64 runtime; approved package 14.52.37029.0, licensing gate unchanged', 'Windows API and UCRT system libraries'],
  jni_note: 'dartjni.dll imports jvm.dll, but the application has no direct import and NativeAssetsManifest has no entries. JVM is not bundled; no Java runtime permission or requirement is inferred.',
  access_agent_in_desktop_release_directory: false,
  branding_assets: {status: 'Existing product assets; no new rights attestation', files: files.filter(f => /branding/.test(f.Path)).map(f => f.Path)},
  original_text_hashes: originals.map(o => ({source: o.label, sha256: sha(o.bytes)})),
  generated_notice_files: Object.fromEntries(fs.readdirSync(out).filter(n => n.endsWith('.txt')).map(n => [n, sha(fs.readFileSync(path.join(out, n)))])),
};
fs.writeFileSync(path.join(out, 'inventory.json'), JSON.stringify(inventory, null, 2) + '\n');
console.log(`SHIPPED_FILES=${files.length}\nCOMPILER_NATIVE_ASSET_PACKAGES=${packages.length}\nORIGINAL_NOTICE_BLOCKS=${blocks.length}\nORIGINAL_NOTICE_BYTES_PRESERVED=${fs.readFileSync(path.join(out, 'THIRD-PARTY-NOTICES.txt')).equals(noticeBytes)}\nOUTPUT=${out}`);
