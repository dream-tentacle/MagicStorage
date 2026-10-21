const fs = require('node:fs');
const path = require('node:path');
const sharp = require(path.join(process.argv[2], 'sharp'));
async function main() {
  for (const direction of ['south','north','east']) {
    for (const layer of ['Base','Glass','Top']) {
      const stem = path.join(__dirname,'GameLayers',`${layer}_${direction}`);
      await sharp(fs.readFileSync(stem+'.svg'),{density:144})
        .resize(1536,direction==='east'?1536:1024).png().toFile(stem+'.png');
    }
  }
  console.log('Rendered 9 transparent layer PNGs.');
}
main().catch(error=>{console.error(error.message);process.exitCode=1;});
