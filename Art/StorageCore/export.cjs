// Render the editable SVG. Pass the bundled Node package directory as argv[2].
const fs = require('node:fs');
const path = require('node:path');
const sharp = require(path.join(process.argv[2], 'sharp'));

async function main() {
  const source = path.join(__dirname, 'StorageCore-v10.svg');
  const destination = path.join(__dirname, 'StorageCore-v10.png');
  await sharp(fs.readFileSync(source), { density: 192 })
    .resize(512, 512)
    .png()
    .toFile(destination);
  console.log(destination);
}
main().catch(error => { console.error(error.message); process.exitCode = 1; });
