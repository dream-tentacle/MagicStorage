const fs = require('node:fs');
const path = require('node:path');
const sharp = require(path.join(process.argv[2], 'sharp'));
const manifest = JSON.parse(fs.readFileSync(path.join(__dirname, 'directions-manifest.json'), 'utf8'));
async function main() {
  for (const asset of manifest.filter(asset => !asset.stem.includes('_south'))) {
    const file = path.join(__dirname, asset.stem);
    await sharp(fs.readFileSync(file + '.svg'), { density: 192 })
      .resize(asset.width, asset.height).png().toFile(file + '.png');
    console.log(asset.stem);
  }
}
main().catch(error => { console.error(error.message); process.exitCode = 1; });
