const fs = require('node:fs');
const path = require('node:path');
const sharp = require(path.join(process.argv[2], 'sharp'));
async function main() {
  for (const direction of ['south', 'north', 'east']) {
    const stem = path.join(__dirname, `CosmicMechGestator_${direction}-v6`);
    const height = direction === 'east' ? 1536 : 1024;
    await sharp(fs.readFileSync(stem + '.svg'), {density:144})
      .resize(1536, height).png().toFile(stem + '.png');
    console.log(stem + '.png');
  }
}
main().catch(error => {console.error(error.message); process.exitCode = 1;});
