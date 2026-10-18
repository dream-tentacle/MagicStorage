// Render the vector at high resolution for the 1024px in-game core test.
const fs = require('node:fs');
const path = require('node:path');
const sharp = require(path.join(process.argv[2], 'sharp'));
async function main() {
  const source = path.join(__dirname, 'StorageCore-v10.svg');
  const destination = path.join(__dirname, 'StorageCore-v10-render2048.png');
  await sharp(fs.readFileSync(source), { density: 384 })
    .resize(2048, 2048).png().toFile(destination);
  console.log(destination);
}
main().catch(error => { console.error(error.message); process.exitCode = 1; });
