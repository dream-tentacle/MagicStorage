// Export the SVG sources to game textures. Pass the bundled Node package directory as argv[2].
const fs = require('node:fs');
const path = require('node:path');
const sharp = require(path.join(process.argv[2], 'sharp'));

async function main() {
  const destination = path.resolve(__dirname, '../../Textures/UI/Gizmos/MagicStorage');
  fs.mkdirSync(destination, { recursive: true });
  for (const file of fs.readdirSync(__dirname).filter(name => name.endsWith('.svg')).sort()) {
    const output = path.join(destination, path.basename(file, '.svg') + '.png');
    await sharp(fs.readFileSync(path.join(__dirname, file)), { density: 192 })
      .resize(128, 128)
      .png()
      .toFile(output);
    console.log(path.basename(output));
  }
}

main().catch(error => { console.error(error.message); process.exitCode = 1; });
