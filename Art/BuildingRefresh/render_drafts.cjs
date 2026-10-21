const fs = require('fs');
const path = require('path');
const sharp = require('C:/Users/x1353/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const manifestPath = path.join(__dirname,fs.existsSync(path.join(__dirname,'outline-manifest.json')) ? 'outline-manifest.json' : 'manifest.json');
const all = JSON.parse(fs.readFileSync(manifestPath,'utf8'));
const manifest = process.argv[2] ? all.filter(item => item.name === process.argv[2]) : all;
if (!manifest.length) throw new Error('Unknown draft name');
(async () => {
  for (const item of manifest) {
    await sharp(path.join(__dirname,item.svg)).png().toFile(path.join(__dirname,item.png));
  }
  console.log(`Rendered ${manifest.length} transparent PNG drafts.`);
})().catch(err=>{console.error(err);process.exit(1)});
