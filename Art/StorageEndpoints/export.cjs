const fs = require('node:fs');
const path = require('node:path');
const sharp = require(path.join(process.argv[2], 'sharp'));
const names = ['StorageReceiver-v1', 'StorageSupplyShelf_south-v1', 'TransportSupplier_south-v3', 'TransportSupplier_north-v3', 'TransportSupplier_east-v3'];
async function main() {
  for (const name of names) {
    const destination = path.join(__dirname, name + '.png');
    await sharp(fs.readFileSync(path.join(__dirname, name + '.svg')), { density: 192 })
      .resize(512, 512).png().toFile(destination);
    console.log(destination);
  }
}
main().catch(error => { console.error(error.message); process.exitCode = 1; });
