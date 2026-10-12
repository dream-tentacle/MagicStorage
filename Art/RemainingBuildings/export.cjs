const fs = require('node:fs');
const path = require('node:path');
const sharp = require(path.join(process.argv[2], 'sharp'));
const manifest = JSON.parse(fs.readFileSync(path.join(__dirname, 'manifest.json'), 'utf8'));
const requested = new Set(process.argv.slice(3));
async function main() {
  for (const asset of manifest) {
    if (requested.size && !requested.has(asset.stem)) continue;
    const file = path.join(__dirname, asset.stem);
    await sharp(fs.readFileSync(file + '.svg'), { density: 96 })
      .resize(asset.width, asset.height).png().toFile(file + '.png');
    console.log(asset.stem + ': ' + asset.width + ' × ' + asset.height);
  }
}
main().catch(error => { console.error(error.message); process.exitCode = 1; });
